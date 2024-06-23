////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace MassivePoints.Data;

internal sealed class DbQuadTreeSession : ISession
{
    private readonly DbTransaction transaction;

    public DbQuadTreeSession(DbTransaction transaction) =>
        this.transaction = transaction;

    public ValueTask DisposeAsync() =>
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        this.transaction.DisposeAsync();
#else
        // In runtime, maybe it implements IAsyncDisposable.
        this.transaction is IAsyncDisposable ad ?
            ad.DisposeAsync() : new(Task.Run(this.transaction.Dispose));
#endif

    public ValueTask FinishAsync() =>
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        new(this.transaction.CommitAsync());
#else
        new(Task.Run(this.transaction.Commit));
#endif
}
