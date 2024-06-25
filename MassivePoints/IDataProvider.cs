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
using System.Threading;
using System.Threading.Tasks;

namespace MassivePoints;

public readonly struct RemoveResults
{
    public readonly long Removed;
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
/// QuadTree backend data provider interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
public interface IDataProvider<TValue, TNodeId> : IAsyncDisposable
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
    /// Begin a session.
    /// </summary>
    /// <param name="willUpdate">True if possibility changes will be made during the session</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The session</returns>
    ValueTask<ISession> BeginSessionAsync(bool willUpdate, CancellationToken ct);

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

    ValueTask AddPointAsync(
        TNodeId nodeId, Point point, TValue value, CancellationToken ct);

    ValueTask<QuadTreeNode<TNodeId>> DistributePointsAsync(
        TNodeId nodeId, Bound[] toBounds, CancellationToken ct);

    ValueTask AggregatePointsAsync(
        TNodeId[] nodeIds, Bound toBound, TNodeId toNodeId, CancellationToken ct);

    ValueTask LookupPointAsync(
        TNodeId nodeId, Point targetPoint, List<KeyValuePair<Point, TValue>> results, CancellationToken ct);

    ValueTask LookupBoundAsync(
        TNodeId nodeId, Bound targetBound, List<KeyValuePair<Point, TValue>> results, CancellationToken ct);

    IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(
        TNodeId nodeId, Bound targetBound, CancellationToken ct);

    ValueTask<RemoveResults> RemovePointsAsync(
        TNodeId nodeId, Point point, bool includeRemains, CancellationToken ct);

    ValueTask<RemoveResults> RemoveBoundAsync(
        TNodeId nodeId, Bound bound, bool includeRemains, CancellationToken ct);
}
