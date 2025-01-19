// <auto-generated/>
#pragma warning disable
using Raven.Client.Documents;

namespace Internal.Generated.WolverineHandlers
{
    // START: CompleteTwoHandler402398939
    public class CompleteTwoHandler402398939 : Wolverine.Runtime.Handlers.MessageHandler
    {
        private readonly Raven.Client.Documents.IDocumentStore _documentStore;

        public CompleteTwoHandler402398939(Raven.Client.Documents.IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }



        public override async System.Threading.Tasks.Task HandleAsync(Wolverine.Runtime.MessageContext context, System.Threading.CancellationToken cancellation)
        {
            using var asyncDocumentSession = _documentStore.OpenAsyncSession();
            // The actual message body
            var completeTwo = (Wolverine.ComplianceTests.Sagas.CompleteTwo)context.Envelope.Message;

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
                stringBasicWorkflow.Handle(completeTwo);

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

    // END: CompleteTwoHandler402398939
    
    
}

