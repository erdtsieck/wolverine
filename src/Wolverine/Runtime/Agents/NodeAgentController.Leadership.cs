using Microsoft.Extensions.Logging;

namespace Wolverine.Runtime.Agents;

public partial class NodeAgentController : IInternalHandler<TryAssumeLeadership>
{
    public async IAsyncEnumerable<object> HandleAsync(TryAssumeLeadership command)
    {
        if (_tracker.Self!.IsLeader())
        {
            _logger.LogInformation("Already the current leader ({NodeId}), ignoring the request to assume leadership",
                _tracker.Self.AssignedNodeId);
            yield break;
        }

        await _persistence.LogRecordsAsync(NodeRecord.For(_runtime.Options, NodeRecordType.ElectionRequested));

        var assigned = await _persistence.MarkNodeAsLeaderAsync(command.CurrentLeaderId, _tracker.Self!.Id);

        if (assigned.HasValue)
        {
            if (assigned == _tracker.Self.Id)
            {
                _logger.LogInformation("Node {NodeNumber} successfully assumed leadership", _tracker.Self.AssignedNodeId);
                await _persistence.LogRecordsAsync(NodeRecord.For(_runtime.Options,
                    NodeRecordType.LeadershipAssumed, LeaderUri));

                var all = await _persistence.LoadAllNodesAsync(_cancellation);
                var others = all.Where(x => x.Id != _tracker.Self.Id).ToArray();
                foreach (var other in others)
                {
                    _tracker.Add(other);
                    yield return new NodeEvent(_tracker.Self, NodeEventType.LeadershipAssumed).ToNode(other);
                }

                _tracker.Publish(new NodeEvent(_tracker.Self, NodeEventType.LeadershipAssumed));

                foreach (var controller in _agentFamilies.Values)
                {
                    var agents = await controller.AllKnownAgentsAsync();
                    _tracker.RegisterAgents(agents);
                }

                await requestAssignmentEvaluationAsync();
            }
            else
            {
                var leader = await _persistence.LoadNodeAsync(assigned.Value, _cancellation);

                if (leader != null)
                {
                    _logger.LogInformation(
                        "Tried to assume leadership at node {NodeNumber}, but another node {LeaderId} has assumed leadership beforehand",
                        _tracker.Self.AssignedNodeId, assigned.Value);
                    _tracker.Publish(new NodeEvent(leader, NodeEventType.LeadershipAssumed));
                }
                else
                {
                    // The referenced leader doesn't exist -- which shouldn't happen, but real life, so try again...
                    yield return new TryAssumeLeadership();
                }
            }

            yield break;
        }

        _logger.LogInformation("Node {NodeNumber} was unable to assume leadership, and no leader was found",
            _tracker.Self.AssignedNodeId);

        // Try it again
        yield return new TryAssumeLeadership();
    }
}