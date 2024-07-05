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

    ValueTask<int> GetPointCountAsync(
        TNodeId nodeId, CancellationToken ct);

    /// <summary>
    /// Inserts the specified coordinate points.
    /// </summary>
    /// <param name="nodeId">Node ID</param>
    /// <param name="points">Coordinate points</param>
    /// <param name="offset">Coordinate point list offset</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Inserted points</returns>
    ValueTask<int> InsertPointsAsync(
        TNodeId nodeId, IReadOnlyArray<PointItem<TValue>> points, int offset, CancellationToken ct);

    ValueTask<QuadTreeNode<TNodeId>> DistributePointsAsync(
        TNodeId nodeId, Bound[] toBounds, CancellationToken ct);

    ValueTask AggregatePointsAsync(
        TNodeId[] nodeIds, Bound toBound, TNodeId toNodeId, CancellationToken ct);

    ValueTask<PointItem<TValue>[]> LookupPointAsync(
        TNodeId nodeId, Point targetPoint, CancellationToken ct);

    ValueTask<PointItem<TValue>[]> LookupBoundAsync(
        TNodeId nodeId, Bound targetBound, CancellationToken ct);

    IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
        TNodeId nodeId, Bound targetBound, CancellationToken ct);

    ValueTask<RemoveResults> RemovePointsAsync(
        TNodeId nodeId, Point point, bool includeRemains, CancellationToken ct);

    ValueTask<RemoveResults> RemoveBoundAsync(
        TNodeId nodeId, Bound bound, bool includeRemains, CancellationToken ct);
}
