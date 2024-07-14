////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.DataProvider;
using MassivePoints.Internal;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints;

/// <summary>
/// QuadTree reading session abstraction interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public abstract class QuadTreeSession<TValue> : IAsyncDisposable
{
    internal readonly InternalQuadTreeSession<TValue> internalSession;

    private protected QuadTreeSession(InternalQuadTreeSession<TValue> internalSession) =>
        this.internalSession = internalSession;

    /// <summary>
    /// The overall range of the coordinate points managed.
    /// </summary>
    public Bound Entire =>
        this.internalSession.Entire;

    /// <summary>
    /// Dispose method.
    /// </summary>
    public ValueTask DisposeAsync() =>
        this.internalSession.DisposeAsync();
}

/// <summary>
/// QuadTree reading session implementation.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class QuadTreeSession<TValue, TNodeId> : QuadTreeSession<TValue>
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="providerSession">Data provider session.</param>
    public QuadTreeSession(IDataProviderSession<TValue, TNodeId> providerSession) :
        base(new InternalQuadTreeSession<TValue, TNodeId>(providerSession))
    {
    }
}
