// <auto-generated/>
#pragma warning disable
using Microsoft.Extensions.DependencyInjection;

namespace Internal.Generated.WolverineHandlers
{
    // START: CreateItemHandler1945924936
    public class CreateItemHandler1945924936 : Wolverine.Runtime.Handlers.MessageHandler
    {
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _serviceScopeFactory;

        public CreateItemHandler1945924936(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }



        public override async System.Threading.Tasks.Task HandleAsync(Wolverine.Runtime.MessageContext context, System.Threading.CancellationToken cancellation)
        {
            using var serviceScope = _serviceScopeFactory.CreateScope();
            
            /*
            * Dependency: Descriptor: ServiceType: Microsoft.EntityFrameworkCore.DbContextOptions"1[EfCoreTests.SampleDbContext] Lifetime: Scoped ImplementationFactory: Microsoft.EntityFrameworkCore.DbContextOptions"1[EfCoreTests.SampleDbContext] CreateDbContextOptions[SampleDbContext](System.IServiceProvider)
            * The service registration for Microsoft.EntityFrameworkCore.DbContextOptions<EfCoreTests.SampleDbContext> is an 'opaque' lambda factory with the Scoped lifetime and requires service location
            */
            var sampleDbContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<EfCoreTests.SampleDbContext>(serviceScope.ServiceProvider);
            // The actual message body
            var createItem = (EfCoreTests.CreateItem)context.Envelope.Message;

            System.Diagnostics.Activity.Current?.SetTag("message.handler", "EfCoreTests.CreateItemHandler");
            var createItemHandler = new EfCoreTests.CreateItemHandler();
            
            // The actual message execution
            await createItemHandler.Handle(createItem, sampleDbContext).ConfigureAwait(false);

        }

    }

    // END: CreateItemHandler1945924936
    
    
}

