////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MassivePoints.Data;

[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class DbPreparedCommand : IDisposable
{
    private readonly DbCommand command;
    private readonly DbConnectionCache parent;

    internal DbPreparedCommand(
        DbConnectionCache parent,
        DbCommand command,
        DbQueryDefinition query)
    {
        this.parent = parent;
        this.command = command;
        this.command.CommandType = CommandType.Text;
        this.command.CommandText = query.CommandText;

        var parameters = this.command.Parameters;
        for (var index = 0; index < query.ParameterNames.Length; index++)
        {
            var parameter = this.command.CreateParameter();
            parameter.ParameterName = query.ParameterNames[index];
            parameters.Add(parameter);
        }
    }

    public void Dispose() =>
        this.parent.ReleasePreparedCommand(this);

    internal DbCommand UnsafeCommand =>
        this.command;

    private void PrepareExecution(
        object[] args)
    {
        var parameters = this.command.Parameters;
        for (var index = 0; index < args.Length; index++)
        {
            parameters[index].Value = args[index];
        }
    }

    public ValueTask<int> ExecuteNonQueryAsync(
        CancellationToken ct,
        params object[] args)
    {
        this.PrepareExecution(args);
        return new(this.command.ExecuteNonQueryAsync(ct));
    }

    public async ValueTask<T?> ExecuteReadOneRecordAsync<T>(
        Func<IDataRecord, T> selector,
        CancellationToken ct,
        params object[] args)
    {
        this.PrepareExecution(args);

        var reader = await this.command.ExecuteReaderAsync(
            CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess, ct);
        try
        {
            while (await reader.ReadAsync(ct))
            {
                return selector(reader);
            }
        }
        finally
        {
            if (reader is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }
        return default;
    }

    public async ValueTask ExecuteReadRecordsAsync(
        Action<IDataRecord> action,
        CancellationToken ct,
        params object[] args)
    {
        this.PrepareExecution(args);

        var reader = await this.command.ExecuteReaderAsync(
            CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, ct);
        try
        {
            while (await reader.ReadAsync(ct))
            {
                action(reader);
            }
        }
        finally
        {
            if (reader is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }
    }

    public async IAsyncEnumerable<T> ExecuteEnumerateAsync<T>(
        Func<IDataRecord, T> selector,
        [EnumeratorCancellation] CancellationToken ct,
        params object[] args)
    {
        this.PrepareExecution(args);

        var reader = await this.command.ExecuteReaderAsync(
            CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, ct);
        try
        {
            while (await reader.ReadAsync(ct))
            {
                yield return selector(reader);
            }
        }
        finally
        {
            if (reader is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }
    }
}
