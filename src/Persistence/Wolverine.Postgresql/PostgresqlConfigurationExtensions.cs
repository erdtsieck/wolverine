﻿using JasperFx.Core;
using Npgsql;

namespace Wolverine.Postgresql;

public static class PostgresqlConfigurationExtensions
{
    /// <summary>
    ///     Register Postgresql backed message persistence to a known connection string
    /// </summary>
    /// <param name="options"></param>
    /// <param name="connectionString"></param>
    /// <param name="schemaName">Optional schema name for the Wolverine envelope storage</param>
    public static void PersistMessagesWithPostgresql(this WolverineOptions options, string connectionString,
        string? schemaName = null)
    {
        if (schemaName.IsNotEmpty() && schemaName != schemaName.ToLowerInvariant())
        {
            throw new ArgumentOutOfRangeException(nameof(schemaName),
                "The schema name must be in all lower case characters");
        }
        
        options.Include<PostgresqlBackedPersistence>(o =>
        {
            o.Settings.ConnectionString = connectionString;
            o.Settings.SchemaName = schemaName ?? "public";
            o.Settings.DataSource = NpgsqlDataSource.Create(connectionString);
            
            o.Settings.ScheduledJobLockId = $"{schemaName ?? "public"}:scheduled-jobs".GetDeterministicHashCode();
        });
    }
    
    /// <summary>
    ///     Register Postgresql backed message persistence to a known connection string
    /// </summary>
    /// <param name="options"></param>
    /// <param name="dataSource"></param>
    /// <param name="schemaName">Optional schema name for the Wolverine envelope storage</param>
    public static void PersistMessagesWithPostgresql(this WolverineOptions options, NpgsqlDataSource dataSource,
        string? schemaName = null)
    {
        if (schemaName.IsNotEmpty() && schemaName != schemaName.ToLowerInvariant())
        {
            throw new ArgumentOutOfRangeException(nameof(schemaName),
                "The schema name must be in all lower case characters");
        }
        
        options.Include<PostgresqlBackedPersistence>(o =>
        {
            o.Settings.SchemaName = schemaName ?? "public";
            o.Settings.DataSource = dataSource;
            
            o.Settings.ScheduledJobLockId = $"{schemaName ?? "public"}:scheduled-jobs".GetDeterministicHashCode();
        });
    }
}