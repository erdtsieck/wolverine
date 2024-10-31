﻿using System.Data.Common;
using Weasel.Core;
using Wolverine.Persistence.Durability;
using Wolverine.Transports;

namespace Wolverine.RDBMS;

public abstract partial class MessageDatabase<T>
{
    private readonly string _markEnvelopeAsHandledById;
    private readonly string _incrementIncomingEnvelopeAttempts;

    public abstract Task<IReadOnlyList<Envelope>> LoadPageOfGloballyOwnedIncomingAsync(Uri listenerAddress, int limit);
    public abstract Task ReassignIncomingAsync(int ownerId, IReadOnlyList<Envelope> incoming);

    public Task StoreIncomingAsync(DbTransaction tx, Envelope[] envelopes)
    {
        var cmd = DatabasePersistence.BuildIncomingStorageCommand(envelopes, this);

        cmd.Transaction = tx;
        cmd.Connection = tx.Connection;

        return cmd.ExecuteNonQueryAsync(_cancellation);
    }

    public abstract Task MoveToDeadLetterStorageAsync(Envelope envelope, Exception? exception);

    public Task MarkIncomingEnvelopeAsHandledAsync(Envelope envelope)
    {
        if (HasDisposed) return Task.CompletedTask;
        var keepUntil = DateTimeOffset.UtcNow.Add(Durability.KeepAfterMessageHandling);
        return CreateCommand(_markEnvelopeAsHandledById)
            .With("id", envelope.Id)
            .With("keepUntil", keepUntil)
            .ExecuteNonQueryAsync(_cancellation);
    }

    public async Task MarkIncomingEnvelopeAsHandledAsync(IReadOnlyList<Envelope> envelopes)
    {
        if (HasDisposed) return;
        var keepUntil = DateTimeOffset.UtcNow.Add(Durability.KeepAfterMessageHandling);
        
        var builder = ToCommandBuilder();
        builder.AddNamedParameter("keepUntil", keepUntil);

        foreach (var envelope in envelopes)
        {
            builder.Append($"update {SchemaName}.{DatabaseConstants.IncomingTable} set {DatabaseConstants.Status} = '{EnvelopeStatus.Handled}', {DatabaseConstants.KeepUntil} = @keepUntil where id = ");
            builder.AppendParameter(envelope.Id);
            builder.Append(";");
        }

        var cmd = builder.Compile();

        await using var conn = await DataSource.OpenConnectionAsync(_cancellation);
        try
        {
            cmd.Connection = conn;
            await cmd.ExecuteNonQueryAsync(_cancellation).ConfigureAwait(false);
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    public Task IncrementIncomingEnvelopeAttemptsAsync(Envelope envelope)
    {
        if (HasDisposed) return Task.CompletedTask;
        return CreateCommand(_incrementIncomingEnvelopeAttempts)
            .With("attempts", envelope.Attempts)
            .With("id", envelope.Id)
            .ExecuteNonQueryAsync(_cancellation);
    }

    public async Task StoreIncomingAsync(Envelope envelope)
    {
        if (HasDisposed) return;

        if (envelope.OwnerId == TransportConstants.AnyNode && envelope.Status == EnvelopeStatus.Incoming)
        {
            throw new ArgumentOutOfRangeException(nameof(Envelope),
                "Erroneous persistence of an incoming envelope to 'any' node");
        }

        var builder = ToCommandBuilder();
        DatabasePersistence.BuildIncomingStorageCommand(this, builder, envelope);

        var cmd = builder.Compile();
        try
        {
            await using var conn = await DataSource.OpenConnectionAsync(_cancellation);
            try
            {
                cmd.Connection = conn;
                await cmd.ExecuteNonQueryAsync(_cancellation).ConfigureAwait(false);
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
        catch (Exception e)
        {
            if (isExceptionFromDuplicateEnvelope(e))
            {
                throw new DuplicateIncomingEnvelopeException(envelope.Id);
            }

            throw;
        }
    }

    public async Task StoreIncomingAsync(IReadOnlyList<Envelope> envelopes)
    {
        var cmd = DatabasePersistence.BuildIncomingStorageCommand(envelopes, this);

        await using var conn = await _dataSource.OpenConnectionAsync(_cancellation);
        try
        {

            cmd.Connection = conn;

            await cmd.ExecuteNonQueryAsync(_cancellation);
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    protected abstract bool isExceptionFromDuplicateEnvelope(Exception ex);
}