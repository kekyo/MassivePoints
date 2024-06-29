////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Threading;
using System.Threading.Tasks;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998
// The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
#pragma warning disable CS8424

namespace MassivePoints;

public sealed class QuadTree<TValue, TNodeId> : IQuadTree<TValue>
{
    private readonly IDataProvider<TValue, TNodeId> provider;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="provider">Backend data provider</param>
    public QuadTree(
        IDataProvider<TValue, TNodeId> provider) =>
        this.provider = provider;

    /// <summary>
    /// Begin a reading session.
    /// </summary>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The reading session</returns>
    public async ValueTask<IQuadTreeSession<TValue>> BeginSessionAsync(
        CancellationToken ct = default)
    {
        var session = await this.provider.BeginSessionAsync(false, ct);
        return new QuadTreeSession<TValue,TNodeId>(session);
    }

    /// <summary>
    /// Begin an update session.
    /// </summary>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The update session</returns>
    public async ValueTask<IQuadTreeUpdateSession<TValue>> BeginUpdateSessionAsync(
        CancellationToken ct = default)
    {
        var session = await this.provider.BeginSessionAsync(true, ct);
        return new QuadTreeUpdateSession<TValue,TNodeId>(session);
    }
}
