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
using System.Threading;
using System.Threading.Tasks;

namespace MassivePoints.Data;

[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class DbConnectionCache : IAsyncDisposable
{
    private readonly Func<DbConnection> connectionFactory;
    private readonly Dictionary<string, Stack<DbPreparedCommand>> preparedCommands = new();
    private readonly bool useSharedConnection;

    private DbConnection? commonConnection;
    private DbTransaction? commonTransaction;

    internal DbConnectionCache(
        bool useSharedConnection,
        Func<DbConnection> connectionFactory)
    {
        this.connectionFactory = connectionFactory;
        this.useSharedConnection = useSharedConnection;
    }

    private void Shutdown()
    {
        lock (this.preparedCommands)
        {
            foreach (var preparedCommandStack in this.preparedCommands.Values)
            {
                while (preparedCommandStack.Count >= 1)
                {
                    var preparedCommand = preparedCommandStack.Pop();

                    var command = preparedCommand.UnsafeCommand;
                    var connection = command.Connection!;

                    command.Dispose();

                    if (!object.ReferenceEquals(connection, this.commonConnection))
                    {
                        connection.Dispose();
                    }
                }
            }
            this.preparedCommands.Clear();
        }

        if (this.commonConnection != null)
        {
            this.commonConnection.Dispose();
        }

        this.commonTransaction = null;
        this.commonConnection = null;
    }
    
    /// <summary>
    /// All reserved command will be destroyed, transaction will be rollback when state is running.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (this.commonTransaction != null)
            {
#if NETCOREAPP3_0_OR_GREATER
                await this.commonTransaction.DisposeAsync();
#else
                await Task.Run(this.commonTransaction.Dispose);
#endif
            }
        }
        finally
        {
            this.Shutdown();
        }
    }

    /// <summary>
    /// All reserved command will be destroyed, transaction will be commit when state is running.
    /// </summary>
    public async ValueTask CommitAsync()
    {
        try
        {
            if (this.commonTransaction != null)
            {
#if NETCOREAPP3_0_OR_GREATER
                await this.commonTransaction.CommitAsync();
#else
                await Task.Run(this.commonTransaction.Commit);
#endif
            }
        }
        finally
        {
            this.Shutdown();
        }
    }

    /// <summary>
    /// Create accessible command instance from related query.
    /// </summary>
    /// <param name="query">The query</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>`DbPreparedCommand`</returns>
    public async ValueTask<DbPreparedCommand> GetPreparedCommandAsync(
        DbQueryDefinition query,
        CancellationToken ct)
    {
        lock (this.preparedCommands)
        {
            if (this.preparedCommands.TryGetValue(
                query.CommandText, out var preparedCommandStack) &&
                preparedCommandStack.Count >= 1)
            {
                return preparedCommandStack.Pop();
            }
        }

        if (this.useSharedConnection)
        {
            if (this.commonConnection == null)
            {
                var connection = this.connectionFactory();
                await connection.OpenAsync(ct);

                this.commonConnection = connection;
                this.commonTransaction =
#if NETCOREAPP3_0_OR_GREATER
                    await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
#else
                    await Task.Run(() => connection.BeginTransaction(IsolationLevel.Serializable), ct);
#endif
            }

            var command = this.commonConnection.CreateCommand();
            command.Transaction = this.commonTransaction;

            return new(this, command, query);
        }
        else
        {
            var connection = this.connectionFactory();
            await connection.OpenAsync(ct);

            var command = connection.CreateCommand();
            return new(this, command, query);
        }
    }

    /// <summary>
    /// Release this command.
    /// </summary>
    /// <param name="preparedCommand">`DbPreparedCommand`</param>
    /// <remarks>The command will be reserved for future requests.</remarks>
    internal void ReleasePreparedCommand(
        DbPreparedCommand preparedCommand)
    {
        var commandText = preparedCommand.UnsafeCommand.CommandText;

        lock (this.preparedCommands)
        {
            if (!this.preparedCommands.TryGetValue(commandText, out var preparedCommandStack))
            {
                preparedCommandStack = new();
                this.preparedCommands.Add(commandText, preparedCommandStack);
            }
            preparedCommandStack.Push(preparedCommand);
        }
    }
}
