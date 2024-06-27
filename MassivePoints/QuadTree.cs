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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MassivePoints.Internal;

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
    /// Begin a session.
    /// </summary>
    /// <param name="willUpdate">True if possibility changes will be made during the session</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The session</returns>
    public async ValueTask<IQuadTreeSession<TValue>> BeginSessionAsync(
        bool willUpdate, CancellationToken ct = default)
    {
        var session = await this.provider.BeginSessionAsync(willUpdate, ct);
        return new QuadTreeSession<TValue,TNodeId>(session);
    }
}
