﻿////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints.Data;

/// <summary>
/// SQLite journal modes.
/// </summary>
public enum SQLiteJournalModes
{
    Delete,
    Truncate,
    Persist,
    Memory,
    Wal,
    Off,
}

public static class DbDataProviderExtension
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
    
    /// <summary>
    /// Create tables for SQLite purpose.
    /// </summary>
    /// <param name="dropToCreate">Drop tables to create when True</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <typeparam name="TValue"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask CreateSQLiteTablesAsync<TValue>(
        this DbDataProvider<TValue> provider,
        bool dropToCreate, CancellationToken ct = default)
    {
        using var connection = await provider.OpenTemporaryConnectionAsync(ct);
        
        if (dropToCreate)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = $"DROP TABLE IF EXISTS {provider.Configuration.Prefix}_nodes";
                if (await command.ExecuteNonQueryAsync(ct) < 0)
                {
                    throw new InvalidOperationException();
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = $"DROP TABLE IF EXISTS {provider.Configuration.Prefix}_node_points";
                if (await command.ExecuteNonQueryAsync(ct) < 0)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        var childBoundCount = provider.Configuration.Entire.GetChildBoundCount();
        var dimensionAxisCount = provider.Configuration.Entire.GetDimensionAxisCount();

        var nodeColumnNames = string.Join(
            ",",
            Enumerable.Range(0, childBoundCount).Select(index => $"{provider.Configuration.NodeColumnName(index)} INTEGER"));
        using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.Text;
            command.CommandText =
                $"CREATE TABLE IF NOT EXISTS {provider.Configuration.Prefix}_nodes (id INTEGER PRIMARY KEY NOT NULL,{nodeColumnNames})";
            if (await command.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidOperationException();
            }
        }

        var pointColumnNames =string.Join(
            ",",
            Enumerable.Range(0, dimensionAxisCount).Select(index => $"{provider.Configuration.NodePointColumnName(index)} REAL NOT NULL"));
        using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.Text;
            command.CommandText =
                $"CREATE TABLE IF NOT EXISTS {provider.Configuration.Prefix}_node_points (node_id INTEGER NOT NULL,{pointColumnNames},[value] {GetSQLiteTypeName<TValue>()} {GetSQLiteTypeAttribute<TValue>()})";
            if (await command.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidOperationException();
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.Text;
            command.CommandText =
                $"CREATE INDEX IF NOT EXISTS {provider.Configuration.Prefix}_node_points_node_id_index ON {provider.Configuration.Prefix}_node_points(node_id)";
            if (await command.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidOperationException();
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.Text;
            command.CommandText =
                $"SELECT COUNT(*) FROM {provider.Configuration.Prefix}_nodes";
            if (await command.ExecuteScalarAsync(ct) is long count && count == 0)
            {
                var nodeColumnNames2 = string.Join(
                    ",",
                    Enumerable.Range(0, childBoundCount).Select(index => provider.Configuration.NodeColumnName(index)));
                var nodeNullColumns = string.Join(
                    ",",
                    Enumerable.Range(0, childBoundCount).Select(_ => "NULL"));
                
                command.CommandType = CommandType.Text;
                command.CommandText =
                    $"INSERT INTO {provider.Configuration.Prefix}_nodes (id,{nodeColumnNames2}) VALUES (0,{nodeNullColumns})";
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
