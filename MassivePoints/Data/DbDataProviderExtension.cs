////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.Internal;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints.Data;

public static class DbDataProviderExtension
{
    /// <summary>
    /// Create tables for SQLite purpose.
    /// </summary>
    /// <param name="dropToCreate">Drop tables to create when True</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <typeparam name="TValue"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public static ValueTask CreateSQLiteTablesAsync<TValue>(
        this DbDataProvider<TValue> provider,
        bool dropToCreate, CancellationToken ct = default) =>
        InternalDbDataProvider.CreateSQLiteTablesAsync(provider, dropToCreate, ct);

    /// <summary>
    /// Set SQLite journal mode.
    /// </summary>
    /// <param name="connection">ADO.NET database connection</param>
    /// <param name="journalMode">Journal mode</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <remarks>If you are using a SQLite provider other than `System.Data.SQLite`, this method may be useful to set the journal mode.
    /// If you are using `System.Data.SQLite`, you can specify it using a connection string.</remarks>
    /// <example>
    /// <code>
    /// // Create QuadTree database provider.
    /// var quadTreeProvider = QuadTree.Factory.CreateProvider<long>(
    ///     async ct =>
    ///     {
    ///         var connection = new SqliteConnection(connectionString);
    ///         await connection.OpenAsync(ct);
    ///             
    ///         // Set journal mode to MEMORY.
    ///         await connection.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, ct);
    ///         return connection;
    ///     },
    ///     new DbDataProviderConfiguration(Bound.TheGlobe2D, maxNodePoints));
    /// </code>
    /// </example>
    public static ValueTask SetSQLiteJournalModeAsync(
        this DbConnection connection,
        SQLiteJournalModes journalMode,
        CancellationToken ct = default) =>
        InternalDbDataProvider.SetSQLiteJournalModeAsync(connection, journalMode, ct);
}
