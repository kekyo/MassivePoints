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
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998
// The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
#pragma warning disable CS8424

namespace MassivePoints;

/// <summary>
/// QuadTree reading session implementation.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class QuadTreeSession<TValue, TNodeId> : QuadTreeSession<TValue>
{
    private readonly InternalQuadTreeSession<TValue, TNodeId> session;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="providerSession">Data provider session.</param>
    public QuadTreeSession(IDataProviderSession<TValue, TNodeId> providerSession) =>
        this.session = new(providerSession);

    /// <summary>
    /// The overall range of the coordinate points managed.
    /// </summary>
    public override Bound Entire =>
        this.session.Entire;

    /////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Dispose method.
    /// </summary>
    public override ValueTask DisposeAsync() =>
        this.session.DisposeAsync();

    /////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Lookup values with a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public override ValueTask<PointItem<TValue>[]> LookupPointAsync(
        Point point, CancellationToken ct = default) =>
        this.session.LookupPointAsync(point, ct);

    /// <summary>
    /// Lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public override ValueTask<PointItem<TValue>[]> LookupBoundAsync(
        Bound bound, CancellationToken ct = default) =>
        this.session.LookupBoundAsync(bound, ct);

    /// <summary>
    /// Streaming lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values asynchronous iterator</returns>
    public override IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
        Bound bound, [EnumeratorCancellation] CancellationToken ct = default) =>
        this.session.EnumerateBoundAsync(bound, ct);
}
