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

public interface IQuadTreeProvider<TValue, TNodeId> : IAsyncDisposable
{
    Bound Entire { get; }
    int MaxNodePoints { get; }
    TNodeId RootId { get; }
    
    ValueTask<ISession> BeginSessionAsync(bool willUpdate, CancellationToken ct);
    
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
