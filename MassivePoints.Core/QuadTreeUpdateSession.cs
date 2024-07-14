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
using System.ComponentModel;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints;

/// <summary>
/// QuadTree update session abstraction interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public abstract class QuadTreeUpdateSession<TValue> :
    QuadTreeSession<TValue>
{
    private protected QuadTreeUpdateSession(InternalQuadTreeSession<TValue> internalSession) :
        base(internalSession)
    {
    }
}

/// <summary>
/// QuadTree update session implementation.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class QuadTreeUpdateSession<TValue, TNodeId> :
    QuadTreeUpdateSession<TValue>
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="providerSession">Data provider session.</param>
    public QuadTreeUpdateSession(IDataProviderSession<TValue, TNodeId> providerSession) :
        base(new InternalQuadTreeSession<TValue, TNodeId>(providerSession))
    {
    }
}
