using IntegrationTests;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using JasperFx.MultiTenancy;
using JasperFx.Resources;
using Marten;
using Marten.Events.Aggregation;
using Marten.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Marten;

namespace MartenTests.Bugs;

/// <summary>
/// Reproduction for https://github.com/JasperFx/marten/issues/4268
///
/// Root cause: mt_doc_envelope existed in the database as a multi-tenanted +
/// hash-partitioned table (from a previous config where Envelope was registered
/// with MultiTenantedWithPartitioning). After switching projections from Async to
/// Inline and enabling EnableSideEffectsOnInlineProjections, the explicit
/// MultiTenantedWithPartitioning config for Envelope is removed. Marten falls back
/// to its default (single-tenant) for Envelope and generates DDL to remove
/// tenant_id. PostgreSQL rejects this because tenant_id is the partition key:
/// "unique constraint on partitioned table must include all partitioning columns".
///
/// Reproduction steps:
/// 1. Phase 1: register Envelope as multi-tenanted + hash-partitioned, run with
///    Async projections → mt_doc_envelope created with (tenant_id, id) PK
/// 2. Phase 2: remove the explicit Envelope partitioning config (no explicit
///    SingleTenanted() call — Marten defaults to single-tenant), switch to Inline +
///    EnableSideEffectsOnInlineProjections = true → DDL migration fails
/// </summary>
public class Bug_marten_4268_async_to_inline_with_conjoined_tenancy
{
    private const string SchemaName = "gh_marten_4268";

    // This test currently FAILS, reproducing the bug.
    // Remove the [Fact] attribute or mark as [Fact(Skip="...")]  once the bug is fixed.
    [Fact]
    public async Task switching_projection_from_async_to_inline_should_not_fail_ddl_migration()
    {
        // Phase 1: Async projection + Envelope registered as multi-tenanted with hash
        // partitioning (simulates the previous production config that created the
        // mt_doc_envelope table with tenant_id + partitioning)
        using (var host = await BuildHost(phase2: false))
        {
            var store = host.Services.GetRequiredService<IDocumentStore>();
            await store.Advanced.Clean.DeleteAllDocumentsAsync();
            await store.Advanced.Clean.DeleteAllEventDataAsync();

            await using var session = store.LightweightSession("tenant-a");
            var streamId = Guid.NewGuid().ToString();
            session.Events.StartStream<OrderSummary>(streamId, new OrderPlaced(streamId, 100m));
            await session.SaveChangesAsync();

            await host.StopAsync();
        }

        // Phase 2: Inline projection + EnableSideEffectsOnInlineProjections = true,
        // WITHOUT the explicit Envelope multi-tenanted config.
        // Marten will detect that the expected Envelope schema (non-tenanted) differs
        // from the actual schema (tenanted + hash-partitioned) and try to migrate it.
        // This should fail with:
        // "unique constraint on partitioned table must include all partitioning columns"
        using var host2 = await BuildHost(phase2: true);

        // Trigger the schema migration by saving events with an inline projection
        var store2 = host2.Services.GetRequiredService<IDocumentStore>();
        await using var session2 = store2.LightweightSession("tenant-a");
        var streamId2 = Guid.NewGuid().ToString();
        session2.Events.StartStream<OrderSummary>(streamId2, new OrderPlaced(streamId2, 200m));
        await session2.SaveChangesAsync();
    }

    private static Task<IHost> BuildHost(bool phase2) =>
        Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Durability.EnableInboxPartitioning = true;

                opts.Services.AddMarten(m =>
                {
                    m.Connection(Servers.PostgresConnectionString);
                    m.DatabaseSchemaName = SchemaName;
                    m.DisableNpgsqlLogging = true;

                    m.TenantIdStyle = TenantIdStyle.ForceLowerCase;
                    m.Events.TenancyStyle = TenancyStyle.Conjoined;
                    m.Events.StreamIdentity = StreamIdentity.AsString;
                    m.Events.AppendMode = EventAppendMode.Quick;
                    m.Events.UseArchivedStreamPartitioning = true;
                    m.Events.UseMandatoryStreamTypeDeclaration = true;
                    m.Advanced.DefaultTenantUsageEnabled = false;

                    if (phase2)
                    {
                        // Phase 2: Inline + side effects enabled.
                        // The explicit MultiTenantedWithPartitioning config for Envelope is gone —
                        // no SingleTenanted() call either, just like in the real production switch.
                        // Marten defaults to single-tenant for Envelope (Envelope implements
                        // IHasTenantId but NOT Marten's ITenanted, so no automatic conjoined tenancy).
                        // This conflicts with the existing mt_doc_envelope table (tenanted + hash-partitioned)
                        // and triggers the failing DDL migration.
                        m.Events.EnableSideEffectsOnInlineProjections = true;
                        m.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Inline);
                    }
                    else
                    {
                        // Phase 1: Async, no explicit Envelope config — just like the real production setup
                        m.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Async);
                    }

                    m.Schema.For<OrderSummary>()
                        .StartIndexesByTenantId()
                        .MultiTenantedWithPartitioning(x => x.ByHash("h000", "h001", "h002", "h003"));
                })
                .IntegrateWithWolverine()
                .UseLightweightSessions()
                .AddAsyncDaemon(DaemonMode.Solo);

                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
}

public record OrderPlaced(string OrderId, decimal Amount);
public record OrderShipped(string OrderId);

public class OrderSummary
{
    public string Id { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool Shipped { get; set; }
}

public class OrderSummaryProjection : SingleStreamProjection<OrderSummary, string>
{
    public OrderSummary Create(OrderPlaced e) => new() { Id = e.OrderId, Amount = e.Amount };

    public void Apply(OrderShipped e, OrderSummary summary) => summary.Shipped = true;
}

public class Bug_marten_4268_envelope_schema_only
{
    private const string SchemaName = "gh_marten_4268_envelope";

    [Fact]
    public async Task changing_envelope_from_multitenananted_partitioned_to_single_tenanted_should_not_fail()
    {
        // Phase 1: Envelope as multi-tenanted + hash-partitioned
        using (var host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Services.AddMarten(m =>
                {
                    m.Connection(Servers.PostgresConnectionString);
                    m.DatabaseSchemaName = SchemaName;
                    m.DisableNpgsqlLogging = true;
                    m.Schema.For<Envelope>()
                        .MultiTenantedWithPartitioning(x => x.ByHash("h000", "h001"));
                })
                .IntegrateWithWolverine()
                .UseLightweightSessions();
                opts.Services.AddResourceSetupOnStartup();
            }).StartAsync())
        {
            await host.StopAsync();
        }

        // Phase 2: Envelope with no explicit tenancy config at all (no inline, no EnableSideEffectsOnInlineProjections).
        // Marten defaults to single-tenant for Envelope, conflicting with the existing
        // hash-partitioned mt_doc_envelope table. This proves the bug is purely in
        // Marten's DDL migration logic for hash-partitioned tables, unrelated to
        // inline projections or EnableSideEffectsOnInlineProjections.
        using var host2 = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Services.AddMarten(m =>
                {
                    m.Connection(Servers.PostgresConnectionString);
                    m.DatabaseSchemaName = SchemaName;
                    m.DisableNpgsqlLogging = true;
                    // Envelope not explicitly configured — Marten defaults to single-tenant
                })
                .IntegrateWithWolverine()
                .UseLightweightSessions();
                opts.Services.AddResourceSetupOnStartup();
            }).StartAsync();
    }
}
