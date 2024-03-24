using System.Threading.Tasks.Dataflow;
using JasperFx.Core;
using Microsoft.Extensions.Logging;
using Wolverine.Util.Dataflow;

namespace Wolverine.Runtime.Agents;

internal interface IInternalMessage
{
}

public record StartLocalAgentProcessing(WolverineOptions Options) : IInternalMessage;

public record EvaluateAssignments : IInternalMessage;

public partial class NodeAgentController
{
    public static readonly Uri LeaderUri = new("wolverine://leader");

    private readonly Dictionary<string, IAgentFamily>
        _agentFamilies = new();

    private readonly ActionBlock<EvaluateAssignments[]> _assignmentBlock;
    private readonly BatchingBlock<EvaluateAssignments> _assignmentBufferBlock;
    private readonly CancellationToken _cancellation;
    private readonly ILogger _logger;
    private readonly INodeAgentPersistence _persistence;

    private readonly IWolverineRuntime _runtime;
    private readonly INodeStateTracker _tracker;

    private ImHashMap<Uri, IAgent> _agents = ImHashMap<Uri, IAgent>.Empty;

    internal NodeAgentController(IWolverineRuntime runtime, INodeStateTracker tracker,
        INodeAgentPersistence persistence,
        IEnumerable<IAgentFamily> agentControllers, ILogger logger, CancellationToken cancellation)
    {
        _runtime = runtime;
        _tracker = tracker;
        _persistence = persistence;
        foreach (var agentController in agentControllers) _agentFamilies[agentController.Scheme] = agentController;

        if (runtime.Storage is IAgentFamily agentFamily && runtime.Options.Durability.DurabilityAgentEnabled)
        {
            _agentFamilies[agentFamily.Scheme] = agentFamily;
        }

        _cancellation = cancellation;
        _logger = logger;

        _assignmentBlock = new ActionBlock<EvaluateAssignments[]>(
            async _ =>
            {
                await new MessageBus(runtime).PublishAsync(new EvaluateAssignments());
            },
            new ExecutionDataflowBlockOptions { CancellationToken = runtime.Cancellation });

        _assignmentBufferBlock =
            new BatchingBlock<EvaluateAssignments>(runtime.Options.Durability.EvaluateAssignmentBufferTime,
                _assignmentBlock);
    }

    public bool HasStartedInSoloMode { get; private set; }


    internal void AddHandlers(WolverineRuntime runtime)
    {
        var handlers = runtime.Handlers;
        handlers.AddMessageHandler(typeof(NodeEvent), new InternalMessageHandler<NodeEvent>(this));
        handlers.AddMessageHandler(typeof(StartLocalAgentProcessing),
            new InternalMessageHandler<StartLocalAgentProcessing>(this));
        handlers.AddMessageHandler(typeof(EvaluateAssignments), new InternalMessageHandler<EvaluateAssignments>(this));
        handlers.AddMessageHandler(typeof(TryAssumeLeadership), new InternalMessageHandler<TryAssumeLeadership>(this));
        handlers.AddMessageHandler(typeof(CheckAgentHealth), new InternalMessageHandler<CheckAgentHealth>(this));
        handlers.AddMessageHandler(typeof(VerifyAssignments), new InternalMessageHandler<VerifyAssignments>(this));

        handlers.RegisterMessageType(typeof(StartAgent));
        handlers.RegisterMessageType(typeof(StartAgents));
        handlers.RegisterMessageType(typeof(AgentsStarted));
        handlers.RegisterMessageType(typeof(AgentsStopped));
        handlers.RegisterMessageType(typeof(StopAgent));
        handlers.RegisterMessageType(typeof(StopAgents));
        handlers.RegisterMessageType(typeof(QueryAgents));
        handlers.RegisterMessageType(typeof(RunningAgents));
    }

    public async Task StopAsync(IMessageBus messageBus)
    {
        await stopAllAgentsAsync();

        if (_runtime.Options.Durability.Mode == DurabilityMode.Balanced)
        {
            await informOtherNodesAboutExitingAsync(messageBus);
        }

        try
        {
            await _persistence.DeleteAsync(_runtime.Options.UniqueNodeId);
            await _persistence.LogRecordsAsync(NodeRecord.For(_runtime.Options, NodeRecordType.NodeStopped));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error trying to delete the exiting node from node persistence");
        }
    }

    private async Task informOtherNodesAboutExitingAsync(IMessageBus messageBus)
    {
        try
        {
            if (_tracker.Self!.IsLeader())
            {
                // notify everyone
                // Don't trust the in memory storage of nodes, fetch from storage
                var controlUris = await _persistence.LoadAllOtherNodeControlUrisAsync(_tracker.Self.Id);
                foreach (var uri in controlUris)
                    await messageBus.EndpointFor(uri).SendAsync(new NodeEvent(_tracker.Self, NodeEventType.Exiting));
            }
            else
            {
                // Don't trust the in memory storage of nodes, fetch from storage
                // ONLY notify the leader. Makes tests work better:)
                var controlUri = await _persistence.FindLeaderControlUriAsync(_tracker.Self.Id);

                if (controlUri != null)
                {
                    await messageBus.EndpointFor(controlUri)
                        .SendAsync(new NodeEvent(_tracker.Self, NodeEventType.Exiting));
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error trying to notify other nodes about this node exiting");
        }
    }

    private async Task stopAllAgentsAsync()
    {
        foreach (var entry in _agents.Enumerate())
        {
            try
            {
                await entry.Value.StopAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error trying to stop agent {AgentUri}", entry.Value.Uri);
            }
        }
    }

    private ValueTask<IAgent> findAgentAsync(Uri uri)
    {
        if (_agentFamilies.TryGetValue(uri.Scheme, out var controller))
        {
            return controller.BuildAgentAsync(uri, _runtime);
        }

        throw new ArgumentOutOfRangeException(nameof(uri), $"Unrecognized agent scheme '{uri.Scheme}'");
    }

    public async Task StartAgentAsync(Uri agentUri)
    {
        if (_agents.Contains(agentUri))
        {
            return;
        }

        var agent = await findAgentAsync(agentUri);
        try
        {
            await agent.StartAsync(_cancellation);
            await _persistence.LogRecordsAsync(NodeRecord.For(_runtime.Options, NodeRecordType.AgentStarted,
                agentUri));

            // Need to update the current node
            _tracker.Publish(new AgentStarted(_runtime.Options.UniqueNodeId, agentUri));

            _logger.LogInformation("Successfully started agent {AgentUri} on Node {NodeNumber}", agentUri, _runtime.Options.Durability.AssignedNodeNumber);
        }
        catch (Exception e)
        {
            throw new AgentStartingException(agentUri, _runtime.Options.UniqueNodeId, e);
        }

        _agents = _agents.AddOrUpdate(agentUri, agent);

        try
        {
            await _persistence.AddAssignmentAsync(_runtime.Options.UniqueNodeId, agentUri, _cancellation);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error trying to persist the assignment of agent {AgentUri} to Node {NodeId}", agentUri,
                _runtime.Options.UniqueNodeId);
        }
    }

    public async Task StopAgentAsync(Uri agentUri)
    {
        _assignmentBufferBlock.Complete();
        _assignmentBlock.Complete();

        if (_agents.TryFind(agentUri, out var agent))
        {
            try
            {
                await agent.StopAsync(_cancellation);
                _logger.LogInformation("Successfully stopped agent {AgentUri} on node {NodeNumber}", agentUri, _runtime.Options.Durability.AssignedNodeNumber);
                await _persistence.LogRecordsAsync(NodeRecord.For(_runtime.Options, NodeRecordType.AgentStopped,
                    agentUri));
            }
            catch (Exception e)
            {
                throw new AgentStoppingException(agentUri, _runtime.Options.UniqueNodeId, e);
            }

            _agents = _agents.Remove(agentUri);
        }

        try
        {
            await _persistence.RemoveAssignmentAsync(_runtime.Options.UniqueNodeId, agentUri, _cancellation);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Error trying to remove the assignment of agent {AgentUri} to Node {NodeId} in persistence", agentUri,
                _runtime.Options.UniqueNodeId);
        }
    }


    public Uri[] AllRunningAgentUris()
    {
        return _agents.Enumerate().Select(x => x.Key).ToArray();
    }

    /// <summary>
    ///     THIS IS STRICTLY FOR TESTING
    /// </summary>
    internal async Task DisableAgentsAsync()
    {
        var agents = _agents.Enumerate().Select(x => x.Value).ToArray();
        foreach (var agent in agents) await agent.StopAsync(CancellationToken.None);

        _agents = ImHashMap<Uri, IAgent>.Empty;
    }


}

public class AgentStartingException : Exception
{
    public AgentStartingException(Uri agentUri, Guid nodeId, Exception? innerException) : base(
        $"Failed trying to start agent {agentUri} on node {nodeId}", innerException)
    {
    }
}

public class AgentStoppingException : Exception
{
    public AgentStoppingException(Uri agentUri, Guid nodeId, Exception? innerException) : base(
        $"Failed trying to stop agent {agentUri} on node {nodeId}", innerException)
    {
    }
}