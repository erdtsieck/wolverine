// <auto-generated/>
#pragma warning disable
using Microsoft.Extensions.DependencyInjection;

namespace Internal.Generated.WolverineHandlers
{
    // START: WidgetUsingMessage2Handler1317104740
    public class WidgetUsingMessage2Handler1317104740 : Wolverine.Runtime.Handlers.MessageHandler
    {
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _serviceScopeFactory;

        public WidgetUsingMessage2Handler1317104740(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }



        public override System.Threading.Tasks.Task HandleAsync(Wolverine.Runtime.MessageContext context, System.Threading.CancellationToken cancellation)
        {
            using var serviceScope = _serviceScopeFactory.CreateScope();
            
            /*
            * Concrete type System.Collections.Generic.IEnumerable<CoreTests.Codegen.IWidget> is not public, so requires service location
            */
            var widgetIEnumerable = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<System.Collections.Generic.IEnumerable<CoreTests.Codegen.IWidget>>(serviceScope.ServiceProvider);
            // The actual message body
            var widgetUsingMessage2 = (CoreTests.Codegen.WidgetUsingMessage2)context.Envelope.Message;

            var widgetUsingMessage2Handler = new CoreTests.Codegen.WidgetUsingMessage2Handler(widgetIEnumerable);
            
            // The actual message execution
            widgetUsingMessage2Handler.Handle(widgetUsingMessage2);

            return System.Threading.Tasks.Task.CompletedTask;
        }

    }

    // END: WidgetUsingMessage2Handler1317104740
    
    
}

