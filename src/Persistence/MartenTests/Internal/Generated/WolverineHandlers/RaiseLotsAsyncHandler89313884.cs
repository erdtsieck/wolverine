// <auto-generated/>
#pragma warning disable
using Wolverine.Marten.Publishing;

namespace Internal.Generated.WolverineHandlers
{
    // START: RaiseLotsAsyncHandler89313884
    public class RaiseLotsAsyncHandler89313884 : Wolverine.Runtime.Handlers.MessageHandler
    {
        private readonly Wolverine.Marten.Publishing.OutboxedSessionFactory _outboxedSessionFactory;

        public RaiseLotsAsyncHandler89313884(Wolverine.Marten.Publishing.OutboxedSessionFactory outboxedSessionFactory)
        {
            _outboxedSessionFactory = outboxedSessionFactory;
        }



        public override async System.Threading.Tasks.Task HandleAsync(Wolverine.Runtime.MessageContext context, System.Threading.CancellationToken cancellation)
        {
            // The actual message body
            var raiseLotsAsync = (MartenTests.RaiseLotsAsync)context.Envelope.Message;

            await using var documentSession = _outboxedSessionFactory.OpenSession(context);
            var eventStore = documentSession.Events;
            
            // Loading Marten aggregate
            var eventStream = await eventStore.FetchForWriting<MartenTests.LetterAggregate>(raiseLotsAsync.LetterAggregateId, cancellation).ConfigureAwait(false);

            
            // The actual message execution
            var outgoing1 = MartenTests.RaiseLetterHandler.Handle(raiseLotsAsync, eventStream.Aggregate);

            // Apply events to Marten event stream
            await foreach (var letterAggregateEvent in outgoing1) eventStream.AppendOne(letterAggregateEvent);
            await documentSession.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

    }

    // END: RaiseLotsAsyncHandler89313884
    
    
}

