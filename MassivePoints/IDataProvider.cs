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

/// <summary>
/// QuadTree backend data provider interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
public interface IDataProvider<TValue, TNodeId> : IAsyncDisposable
{
    /// <summary>
    /// This indicates the overall range of the coordinate points managed by data provider.
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
    ValueTask<QuadTreeNode<TNodeId>?> GetNodeAsync(TNodeId nodeId, CancellationToken ct);
    ValueTask<bool> IsDensePointsAsync(TNodeId nodeId, CancellationToken ct);
    ValueTask AddPointAsync(TNodeId nodeId, Point point, TValue value, CancellationToken ct);

    ValueTask<QuadTreeNode<TNodeId>> AssignNextNodeSetAsync(TNodeId nodeId, CancellationToken ct);
    ValueTask MovePointsAsync(TNodeId nodeId, Bound toBound, QuadTreeNode<TNodeId> toNodes, CancellationToken ct);

    ValueTask LookupPointAsync(TNodeId nodeId, Point targetPoint, List<KeyValuePair<Point, TValue>> results, CancellationToken ct);
    ValueTask LookupBoundAsync(TNodeId nodeId, Bound targetBound, List<KeyValuePair<Point, TValue>> results, CancellationToken ct);
    IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(TNodeId nodeId, Bound targetBound, CancellationToken ct);

    ValueTask<int> RemovePointsAsync(TNodeId nodeId, Point point, CancellationToken ct);
    ValueTask<int> RemoveBoundAsync(TNodeId nodeId, Bound bound, CancellationToken ct);
}
