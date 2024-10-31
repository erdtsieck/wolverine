using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wolverine.ComplianceTests;
using Wolverine.RabbitMQ.Internal;
using Wolverine.Runtime;
using Wolverine.Runtime.Routing;

namespace Wolverine.RabbitMQ.Tests.ConventionalRouting;

public static class ConventionalRoutingTestDefaults
{
    public static bool RoutingMessageOnly(Type type) => type == typeof(ConventionallyRoutedMessage);
}


public abstract class ConventionalRoutingContext : IDisposable
{
    private IHost _host;
    
    internal bool DisableListenerDiscovery { get; set; }
    
    internal IWolverineRuntime theRuntime
    {
        get
        {
            if (_host == null)
            {
                _host = WolverineHost.For(opts =>
                {
                    opts.UseRabbitMq().UseConventionalRouting().AutoProvision().AutoPurgeOnStartup();

                    if (DisableListenerDiscovery)
                    {
                        opts.Discovery.DisableConventionalDiscovery();
                    }
                });
            }

            return _host.Services.GetRequiredService<IWolverineRuntime>();
        }
    }

    internal RabbitMqTransport theTransport
    {
        get
        {
            if (_host == null)
            {
                _host = WolverineHost.For(opts => opts.UseRabbitMq().UseConventionalRouting());
            }

            var options = _host.Services.GetRequiredService<IWolverineRuntime>().Options;

            return options.RabbitMqTransport();
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
    }

    internal void ConfigureConventions(Action<RabbitMqMessageRoutingConvention> configure)
    {
        _host = WolverineHost.For(opts =>
        {
            if (DisableListenerDiscovery)
            {
                opts.Discovery.DisableConventionalDiscovery();
            }
            
            opts.UseRabbitMq().UseConventionalRouting(configure).AutoProvision().AutoPurgeOnStartup();
        });
    }

    internal IMessageRouter RoutingFor<T>()
    {
        return theRuntime.RoutingFor(typeof(T));
    }

    internal void AssertNoRoutes<T>()
    {
        RoutingFor<T>().ShouldBeOfType<EmptyMessageRouter<T>>();
    }

    internal IMessageRoute[] PublishingRoutesFor<T>()
    {
        return RoutingFor<T>().ShouldBeOfType<MessageRouter<T>>().Routes;
    }
}