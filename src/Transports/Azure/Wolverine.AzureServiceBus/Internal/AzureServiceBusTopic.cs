using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Transports;
using Wolverine.Transports.Sending;

namespace Wolverine.AzureServiceBus.Internal;

public class AzureServiceBusTopic : AzureServiceBusEndpoint
{
    private bool _hasInitialized;

    public AzureServiceBusTopic(AzureServiceBusTransport parent, string topicName) : base(parent,
        new Uri($"{AzureServiceBusTransport.ProtocolName}://topic/{topicName}"), EndpointRole.Application)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        TopicName = EndpointName = topicName ?? throw new ArgumentNullException(nameof(topicName));
        Options = new CreateTopicOptions(TopicName);
    }

    public override Task<ServiceBusSessionReceiver> AcceptNextSessionAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public string TopicName { get; }

    public override ValueTask<IListener> BuildListenerAsync(IWolverineRuntime runtime, IReceiver receiver)
    {
        throw new NotSupportedException();
    }

    protected override ISender CreateSender(IWolverineRuntime runtime)
    {
        var mapper = BuildMapper(runtime);
        var sender = Parent.BusClient.CreateSender(TopicName);
        
        if (Mode == EndpointMode.Inline)
        {
            var inlineSender = new InlineAzureServiceBusSender(this, mapper, sender,
                runtime.LoggerFactory.CreateLogger<InlineAzureServiceBusSender>(), runtime.Cancellation);

            return inlineSender;
        }

        var protocol = new AzureServiceBusSenderProtocol(runtime, this, mapper, sender);

        return new BatchedSender(Uri, protocol, runtime.DurabilitySettings.Cancellation, runtime.LoggerFactory.CreateLogger<AzureServiceBusSenderProtocol>());
    }

    internal ISender BuildInlineSender(IWolverineRuntime runtime)
    {
        var mapper = BuildMapper(runtime);
        var sender = Parent.BusClient.CreateSender(TopicName);
        return new InlineAzureServiceBusSender(this, mapper, sender,
            runtime.LoggerFactory.CreateLogger<InlineAzureServiceBusSender>(), runtime.Cancellation);

    }

    public override async ValueTask<bool> CheckAsync()
    {
        var client = Parent.ManagementClient;

        return (await client.TopicExistsAsync(TopicName)).Value;
    }

    public override ValueTask TeardownAsync(ILogger logger)
    {
        var task = Parent.ManagementClient.DeleteTopicAsync(TopicName);
        return new ValueTask(task);
    }
    
    public CreateTopicOptions Options { get; }

    public override ValueTask SetupAsync(ILogger logger)
    {
        var client = Parent.ManagementClient;
        return SetupAsync(client, logger);
    }

    internal async ValueTask SetupAsync(ServiceBusAdministrationClient client, ILogger logger)
    {
        var exists = await client.TopicExistsAsync(TopicName, CancellationToken.None);
        if (!exists)
        {
            Options.Name = TopicName;

            try
            {
                await client.CreateTopicAsync(Options);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error trying to initialize topic {Name}", TopicName);
            }
        }
    }

    public override async ValueTask InitializeAsync(ILogger logger)
    {
        if (_hasInitialized)
        {
            return;
        }

        var client = Parent.ManagementClient;
        await InitializeAsync(client, logger);

        _hasInitialized = true;
    }

    internal ValueTask InitializeAsync(ServiceBusAdministrationClient client, ILogger logger)
    {
        if (Parent.AutoProvision)
        {
            return SetupAsync(client, logger);
        }

        return ValueTask.CompletedTask;
    }

    public AzureServiceBusSubscription FindOrCreateSubscription(string subscriptionName)
    {
        var existing =
            Parent.Subscriptions.FirstOrDefault(x => x.SubscriptionName == subscriptionName && x.Topic == this);

        if (existing != null)
        {
            return existing;
        }

        var subscription = new AzureServiceBusSubscription(Parent, this, subscriptionName);
        Parent.Subscriptions.Add(subscription);

        return subscription;
    }
}