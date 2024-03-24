﻿using System.Data.Common;
using Weasel.Core;
using Wolverine.Persistence.Durability;
using Wolverine.Runtime.Serialization;
using Wolverine.Transports;

namespace Wolverine.RDBMS;

public abstract partial class MessageDatabase<T>
{
    private readonly string _deleteIncomingEnvelopeById;
    private readonly string _incrementIncominEnvelopeAttempts;

    public abstract Task<IReadOnlyList<Envelope>> LoadPageOfGloballyOwnedIncomingAsync(Uri listenerAddress, int limit);
    public abstract Task ReassignIncomingAsync(int ownerId, IReadOnlyList<Envelope> incoming);

    public Task StoreIncomingAsync(DbTransaction tx, Envelope[] envelopes)
    {
        var cmd = DatabasePersistence.BuildIncomingStorageCommand(envelopes, this);

        cmd.Transaction = tx;
        cmd.Connection = tx.Connection;

        return cmd.ExecuteNonQueryAsync(_cancellation);
    }

    public async Task<ErrorReport?> LoadDeadLetterEnvelopeAsync(Guid id)
    {
        await using var reader = await CreateCommand(
            $"select {DatabaseConstants.DeadLetterFields} from {SchemaName}.{DatabaseConstants.DeadLetterTable} where id = @id")
            .With("id", id)
            .ExecuteReaderAsync(_cancellation);
        
        if (!await reader.ReadAsync(_cancellation))
        {
            await reader.CloseAsync();
            return null;
        }

        var body = await reader.GetFieldValueAsync<byte[]>(2, _cancellation);
        var envelope = EnvelopeSerializer.Deserialize(body);

        var report = new ErrorReport(envelope)
        {
            ExceptionType = await reader.GetFieldValueAsync<string>(6, _cancellation),
            ExceptionMessage = await reader.GetFieldValueAsync<string>(7, _cancellation)
        };

        await reader.CloseAsync();

        return report;
    }

    public abstract Task MoveToDeadLetterStorageAsync(Envelope envelope, Exception? exception);

    public Task MarkIncomingEnvelopeAsHandledAsync(Envelope envelope)
    {
        if (HasDisposed) return Task.CompletedTask;
        return CreateCommand(_deleteIncomingEnvelopeById)
            .With("id", envelope.Id)
            .With("keepUntil", DateTimeOffset.UtcNow.Add(Durability.KeepAfterMessageHandling))
            .ExecuteNonQueryAsync(_cancellation);
    }

    public Task IncrementIncomingEnvelopeAttemptsAsync(Envelope envelope)
    {
        if (HasDisposed) return Task.CompletedTask;
        return CreateCommand(_incrementIncominEnvelopeAttempts)
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