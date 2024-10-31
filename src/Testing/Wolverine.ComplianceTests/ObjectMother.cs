﻿using Wolverine;
using Wolverine.Transports;

namespace Wolverine.ComplianceTests;

public static class ObjectMother
{
    public static Envelope Envelope()
    {
        return new Envelope
        {
            Id = Guid.NewGuid(),
            Data = [1, 2, 3, 4],
            MessageType = "Something",
            Destination = TransportConstants.RepliesUri,
            ContentType = EnvelopeConstants.JsonContentType,
            Source = "SomeApp",
            DeliverBy = new DateTimeOffset(DateTime.Today.AddHours(28)),
            OwnerId = 567,
            Attempts = 1,
            SentAt = new DateTimeOffset(DateTime.Today.AddHours(1)),
            Status = EnvelopeStatus.Incoming
        };
    }
}