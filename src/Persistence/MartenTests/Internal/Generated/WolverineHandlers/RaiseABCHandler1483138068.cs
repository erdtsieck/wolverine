// <auto-generated/>
#pragma warning disable
using Wolverine.Marten.Publishing;

namespace Internal.Generated.WolverineHandlers
{
    // START: RaiseABCHandler1483138068
    public class RaiseABCHandler1483138068 : Wolverine.Runtime.Handlers.MessageHandler
    {
        private readonly Wolverine.Marten.Publishing.OutboxedSessionFactory _outboxedSessionFactory;

        public RaiseABCHandler1483138068(Wolverine.Marten.Publishing.OutboxedSessionFactory outboxedSessionFactory)
        {
            _outboxedSessionFactory = outboxedSessionFactory;
        }



        public override async System.Threading.Tasks.Task HandleAsync(Wolverine.Runtime.MessageContext context, System.Threading.CancellationToken cancellation)
        {
            // The actual message body
            var raiseABC = (MartenTests.RaiseABC)context.Envelope.Message;

            await using var documentSession = _outboxedSessionFactory.OpenSession(context);
            var eventStore = documentSession.Events;
            
            // Loading Marten aggregate
            var eventStream = await eventStore.FetchForWriting<MartenTests.LetterAggregate>(raiseABC.LetterAggregateId, cancellation).ConfigureAwait(false);

            
            // The actual message execution
            (var outgoing1, var outgoing2) = MartenTests.RaiseLetterHandler.Handle(raiseABC, eventStream.Aggregate);

            if (outgoing1 != null)
            {
                
                // Capturing any possible events returned from the command handlers
                eventStream.AppendMany(outgoing1);

            }

            
            // Outgoing, cascaded message
            await context.EnqueueCascadingAsync(outgoing2).ConfigureAwait(false);

            await documentSession.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

    }

    // END: RaiseABCHandler1483138068
    
    
}
