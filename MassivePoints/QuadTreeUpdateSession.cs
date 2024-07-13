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
/// QuadTree update session implementation.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class QuadTreeUpdateSession<TValue, TNodeId> :
    QuadTreeUpdateSession<TValue>
{
    private readonly InternalQuadTreeSession<TValue, TNodeId> session;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="providerSession">Data provider session.</param>
    public QuadTreeUpdateSession(IDataProviderSession<TValue, TNodeId> providerSession) =>
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

    /// <summary>
    /// Flush partially data.
    /// </summary>
    public override ValueTask FlushAsync() =>
        this.session.FlushAsync();

    /// <summary>
    /// Finish the session.
    /// </summary>
    public override ValueTask FinishAsync() =>
        this.session.FinishAsync();
    
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

    /////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Insert a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="value">Related value</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>A depth value where placed the coordinate point</returns>
    /// <remarks>The depth value indicates how deeply the added coordinate points are placed in the node depth.
    /// This value is not used directly, but can be used as a performance indicator.</remarks>
    public override ValueTask<int> InsertPointAsync(
        Point point, TValue value, CancellationToken ct = default) =>
        this.session.InsertPointAsync(point, value, ct);

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    public override ValueTask<int> InsertPointsAsync(
        IEnumerable<PointItem<TValue>> points,
        int bulkInsertBlockSize = 100000,
        CancellationToken ct = default) =>
        this.session.InsertPointsAsync(points, bulkInsertBlockSize, ct);

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    public override ValueTask<int> InsertPointsAsync(
        IAsyncEnumerable<PointItem<TValue>> points,
        int bulkInsertBlockSize = 100000,
        CancellationToken ct = default) =>
        this.session.InsertPointsAsync(points, bulkInsertBlockSize, ct);

    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="point">A coordinate point</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    public override ValueTask<int> RemovePointAsync(
        Point point, bool performShrinking = false, CancellationToken ct = default) =>
        this.session.RemovePointAsync(point, performShrinking, ct);

    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    public override ValueTask<long> RemoveBoundAsync(
        Bound bound, bool performShrinking = false, CancellationToken ct = default) =>
        this.session.RemoveBoundAsync(bound, performShrinking, ct);
}
