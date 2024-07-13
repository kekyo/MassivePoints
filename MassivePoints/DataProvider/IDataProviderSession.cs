////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.Collections;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MassivePoints.DataProvider;

/// <summary>
/// Remove manipulation result.
/// </summary>
public readonly struct RemoveResults
{
    /// <summary>
    /// Number of removed coordinate points.
    /// </summary>
    public readonly long Removed;
    
    /// <summary>
    /// Remains coordinate points when available.
    /// </summary>
    public readonly int RemainsHint;

    public RemoveResults(long removed, int remainsHint)
    {
        this.Removed = removed;
        this.RemainsHint = remainsHint;
    }

    public void Deconstruct(out long removed, out int remainsHint)
    {
        removed = this.Removed;
        remainsHint = this.RemainsHint;
    }
}

/// <summary>
/// QuadTree backend data provider session interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
public interface IDataProviderSession<TValue, TNodeId> : IAsyncDisposable
{
    /// <summary>
    /// The overall range of the coordinate points managed.
    /// </summary>
    Bound Entire { get; }

    /// <summary>
    /// Maximum number of coordinate points in each node.
    /// </summary>
    int MaxNodePoints { get; }

    /// <summary>
    /// Root node ID.
    /// </summary>
    TNodeId RootId { get; }
    
    /// <summary>
    /// Flush partially data.
    /// </summary>
    ValueTask FlushAsync();

    /// <summary>
    /// Finish the session.
    /// </summary>
    ValueTask FinishAsync();

    /// <summary>
    /// Get information about the node.
    /// </summary>
    /// <param name="nodeId">Node ID</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Node information if available</returns>
    ValueTask<QuadTreeNode<TNodeId>?> GetNodeAsync(
        TNodeId nodeId, CancellationToken ct);

    /// <summary>
    /// Get number of coordinate points on the node.
    /// </summary>
    /// <param name="nodeId">Target node id</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Coordinate point count</returns>
    ValueTask<int> GetPointCountAsync(
        TNodeId nodeId, CancellationToken ct);

    /// <summary>
    /// Inserts the specified coordinate points.
    /// </summary>
    /// <param name="nodeId">Node ID</param>
    /// <param name="points">Coordinate points</param>
    /// <param name="offset">Coordinate point list offset</param>
    /// <param name="isForceInsert">Force insert all points</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Inserted points</returns>
    ValueTask<int> InsertPointsAsync(
        TNodeId nodeId, IReadOnlyArray<PointItem<TValue>> points, int offset, bool isForceInsert, CancellationToken ct);

    /// <summary>
    /// Distribute coordinate points into the new child nodes.
    /// </summary>
    /// <param name="nodeId">From node id</param>
    /// <param name="toBounds">To child bounds</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Updated node information</returns>
    ValueTask<QuadTreeNode<TNodeId>> DistributePointsAsync(
        TNodeId nodeId, Bound[] toBounds, CancellationToken ct);

    /// <summary>
    /// Aggregate coordinate points on child nodes to a node.
    /// </summary>
    /// <param name="nodeIds">Child node ids</param>
    /// <param name="toBound">Target bound</param>
    /// <param name="toNodeId">Target node id</param>
    /// <param name="ct">`CancellationToken`</param>
    ValueTask AggregatePointsAsync(
        TNodeId[] nodeIds, Bound toBound, TNodeId toNodeId, CancellationToken ct);

    /// <summary>
    /// Lookup coordinate points from a exact point.
    /// </summary>
    /// <param name="nodeId">Target node id</param>
    /// <param name="targetPoint">Target point</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Got coordinate points</returns>
    ValueTask<PointItem<TValue>[]> LookupPointAsync(
        TNodeId nodeId, Point targetPoint, CancellationToken ct);

    /// <summary>
    /// Lookup coordinate points from coordinate range.
    /// </summary>
    /// <param name="nodeId">Target node id</param>
    /// <param name="targetBound">Target coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Got coordinate points</returns>
    ValueTask<PointItem<TValue>[]> LookupBoundAsync(
        TNodeId nodeId, Bound targetBound, CancellationToken ct);

    /// <summary>
    /// Lookup and streaming coordinate points from coordinate range.
    /// </summary>
    /// <param name="nodeId">Target node id</param>
    /// <param name="targetBound">Target coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Coordinate points asynchronous iterator</returns>
    IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
        TNodeId nodeId, Bound targetBound, CancellationToken ct);

    /// <summary>
    /// Remove a coordinate point.
    /// </summary>
    /// <param name="nodeId">Target node id</param>
    /// <param name="point">Target coordinate point</param>
    /// <param name="includeRemains">Include coordinate point remains count in result if true</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Removed count and remains count</returns>
    ValueTask<RemoveResults> RemovePointAsync(
        TNodeId nodeId, Point point, bool includeRemains, CancellationToken ct);

    /// <summary>
    /// Remove coordinate points.
    /// </summary>
    /// <param name="nodeId">Target node id</param>
    /// <param name="bound">Target coordinate bound</param>
    /// <param name="includeRemains">Include coordinate point remains count in result if true</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Removed count and remains count</returns>
    ValueTask<RemoveResults> RemoveBoundAsync(
        TNodeId nodeId, Bound bound, bool includeRemains, CancellationToken ct);
}
