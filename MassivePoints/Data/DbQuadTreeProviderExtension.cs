////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MassivePoints.Data;

public enum SQLiteJournalModes
{
    Delete,
    Truncate,
    Persist,
    Memory,
    Wal,
    Off,
}

public static class DbQuadTreeProviderExtension
{
    private static string GetSQLiteTypeName<TValue>()
    {
        var type = typeof(TValue);
        if (type.IsEnum)
        {
            return "INTEGER";
        }
        return type.FullName switch
        {
            "System.String" => "TEXT",
            "System.Char" => "TEXT",
            "System.Boolean" => "INTEGER",
            "System.Byte" => "INTEGER",
            "System.SByte" => "INTEGER",
            "System.Int16" => "INTEGER",
            "System.UInt16" => "INTEGER",
            "System.Int32" => "INTEGER",
            "System.UInt32" => "INTEGER",
            "System.Int64" => "INTEGER",
            "System.UInt64" => "INTEGER",
            "System.Single" => "REAL",
            "System.Double" => "REAL",
            "System.Decimal" => "REAL",
            "System.Guid" => "TEXT",
            "System.DateTime" => "TEXT",
            "System.DateTimeOffset" => "TEXT",
            _ => "BLOB",
        };
    }
    
    private static string GetSQLiteTypeAttribute<TValue>()
    {
        var type = typeof(TValue);
        if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
        {
            return "NOT NULL";
        }
        else
        {
            return "";
        }
    }
    
    public static async ValueTask CreateSQLiteTablesAsync<TValue>(
        this DbQuadTreeProvider<TValue> provider,
        bool dropToCreate, CancellationToken ct = default)
    {
        if (dropToCreate)
        {
            using (var command = provider.CreateCommand(
                $"DROP TABLE IF EXISTS {provider.Prefix}_nodes"))
            {
                if (await command.ExecuteNonQueryAsync(ct) < 0)
                {
                    throw new InvalidOperationException();
                }
            }

            using (var command = provider.CreateCommand(
                $"DROP TABLE IF EXISTS {provider.Prefix}_node_points"))
            {
                if (await command.ExecuteNonQueryAsync(ct) < 0)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        using (var command = provider.CreateCommand(
            $"CREATE TABLE {provider.Prefix}_nodes (id INTEGER PRIMARY KEY NOT NULL,top_left_id INTEGER,top_right_id INTEGER,bottom_left_id INTEGER,bottom_right_id INTEGER)"))
        {
            if (await command.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidOperationException();
            }
        }

        using (var command = provider.CreateCommand(
            $"CREATE TABLE {provider.Prefix}_node_points (node_id INTEGER NOT NULL,x REAL NOT NULL,y REAL NOT NULL,[value] {GetSQLiteTypeName<TValue>()} {GetSQLiteTypeAttribute<TValue>()})"))
        {
            if (await command.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidOperationException();
            }
        }

        using (var command = provider.CreateCommand(
            $"CREATE INDEX {provider.Prefix}_node_points_node_id_index ON {provider.Prefix}_node_points(node_id)"))
        {
            if (await command.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidOperationException();
            }
        }

        using (var command = provider.CreateCommand(
            $"INSERT INTO {provider.Prefix}_nodes (id,top_left_id,top_right_id,bottom_left_id,bottom_right_id) VALUES (0,NULL,NULL,NULL,NULL)"))
        {
            if (await command.ExecuteNonQueryAsync(ct) != 1)
            {
                throw new InvalidOperationException();
            }
        }
    }
    
    public static async ValueTask SetSQLiteJournalModeAsync<TValue>(
        this DbQuadTreeProvider<TValue> provider,
        SQLiteJournalModes journalMode, CancellationToken ct = default)
    {
        using (var command = provider.CreateCommand(
            $"PRAGMA journal_mode={journalMode.ToString().ToUpperInvariant()}"))
        {
            if (await command.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
