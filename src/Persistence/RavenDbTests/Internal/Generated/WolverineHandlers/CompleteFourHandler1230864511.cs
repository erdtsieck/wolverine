// <auto-generated/>
#pragma warning disable
using Raven.Client.Documents;

namespace Internal.Generated.WolverineHandlers
{
    // START: CompleteFourHandler1230864511
    public class CompleteFourHandler1230864511 : Wolverine.Runtime.Handlers.MessageHandler
    {
        private readonly Raven.Client.Documents.IDocumentStore _documentStore;

        public CompleteFourHandler1230864511(Raven.Client.Documents.IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }



        public override async System.Threading.Tasks.Task HandleAsync(Wolverine.Runtime.MessageContext context, System.Threading.CancellationToken cancellation)
        {
            // The actual message body
            var completeFour = (Wolverine.ComplianceTests.Sagas.CompleteFour)context.Envelope.Message;


            // Open a new document session 
            // message context to support the outbox functionality
            using var asyncDocumentSession = _documentStore.OpenAsyncSession();
            context.EnlistInOutbox(new Wolverine.RavenDb.Internals.RavenDbEnvelopeTransaction(asyncDocumentSession, context));
            // Use optimistic concurrency for sagas
            asyncDocumentSession.Advanced.UseOptimisticConcurrency = true;
            var sagaId = context.Envelope.SagaId;
            if (string.IsNullOrEmpty(sagaId)) throw new Wolverine.Persistence.Sagas.IndeterminateSagaStateIdException(context.Envelope);
            
            // Try to load the existing saga document
            var stringBasicWorkflow = await asyncDocumentSession.LoadAsync<Wolverine.ComplianceTests.Sagas.StringBasicWorkflow>(sagaId, cancellation).ConfigureAwait(false);
            if (stringBasicWorkflow == null)
            {
                throw new Wolverine.Persistence.Sagas.UnknownSagaException(typeof(Wolverine.ComplianceTests.Sagas.StringBasicWorkflow), sagaId);
            }

            else
            {
                
                // The actual message execution
                stringBasicWorkflow.Handle(completeFour);

                // Delete the saga if completed, otherwise update it
                if (stringBasicWorkflow.IsCompleted())
                {
                    asyncDocumentSession.Delete(stringBasicWorkflow);
                }

                else
                {
                    await asyncDocumentSession.StoreAsync(stringBasicWorkflow, cancellation).ConfigureAwait(false);
                }

                
                // Commit all pending changes
                await asyncDocumentSession.SaveChangesAsync(cancellation).ConfigureAwait(false);

            }

        }

    }

    // END: CompleteFourHandler1230864511
    
    
}

