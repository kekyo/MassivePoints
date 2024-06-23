////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998

namespace MassivePoints.Collections;

public sealed class CollectionQuadTreeProvider<TValue> : IQuadTreeProvider<TValue, int>
{
    private readonly AsyncReaderWriterLock locker = new();
    private readonly Dictionary<int, QuadTreeNode<int>> nodes = new();
    private readonly Dictionary<int, List<KeyValuePair<Point, TValue>>> nodePoints = new();
    private int maxNodeId = 0;

    public CollectionQuadTreeProvider(
        Bound entire, int maxNodePoints = 65536)
    {
        this.Entire = entire;
        this.MaxNodePoints = maxNodePoints;
    }

    public ValueTask DisposeAsync()
    {
        this.nodes.Clear();
        return default;
    }

    public Bound Entire { get; }
    public int MaxNodePoints { get; }
    public int RootId => 0;

    public async ValueTask<ISession> BeginSessionAsync(
        bool willUpdate, CancellationToken ct)
    {
        var disposer = await (willUpdate ?
            this.locker.WriterLockAsync(ct) : this.locker.ReaderLockAsync(ct));
        return new CollectionQuadTreeSession(disposer);
    }

    public ValueTask<QuadTreeNode<int>?> GetNodeAsync(
        int nodeId, CancellationToken ct) =>
        new((this.nodes.TryGetValue(nodeId, out var node) && node.TopLeftId != -1) ?
            new QuadTreeNode<int>(node.TopLeftId, node.TopRightId, node.BottomLeftId, node.BottomRightId) : null);

    public ValueTask<bool> IsDensePointsAsync(
        int nodeId, CancellationToken ct) =>
        new(this.nodePoints.TryGetValue(nodeId, out var points) &&
            points.Count >= this.MaxNodePoints);

    public ValueTask AddPointAsync(
        int nodeId, Point point, TValue value, CancellationToken ct)
    {
        if (!this.nodePoints.TryGetValue(nodeId, out var pointItems))
        {
            pointItems = new();
            this.nodePoints.Add(nodeId, pointItems);
        }
        pointItems.Add(new(point, value));
        return default;
    }

    public ValueTask<QuadTreeNode<int>> AssignNextNodeSetAsync(
        int nodeId, CancellationToken ct)
    {
        var baseNodeId = this.maxNodeId;
        var node = new QuadTreeNode<int>(baseNodeId + 1, baseNodeId + 2, baseNodeId + 3, baseNodeId + 4);

        this.nodes[nodeId] = node;

        this.maxNodeId += 4;
        return new(node);
    }

    public ValueTask MovePointsAsync(
        int nodeId, Bound toBound, QuadTreeNode<int> toNodes, CancellationToken ct)
    {
        var points = this.nodePoints[nodeId];

        var topLeftPoints = new List<KeyValuePair<Point, TValue>>();
        var topRightPoints = new List<KeyValuePair<Point, TValue>>();
        var bottomLeftPoints = new List<KeyValuePair<Point, TValue>>();
        var bottomRightPoints = new List<KeyValuePair<Point, TValue>>();

        Parallel.ForEach(
            points,
            pointItem =>
            {
                if (toBound.TopLeft.IsWithin(pointItem.Key))
                {
                    lock (topLeftPoints)
                    {
                        topLeftPoints.Add(pointItem);
                    }
                }
                else if (toBound.TopRight.IsWithin(pointItem.Key))
                {
                    lock (topRightPoints)
                    {
                        topRightPoints.Add(pointItem);
                    }
                }
                else if (toBound.BottomLeft.IsWithin(pointItem.Key))
                {
                    lock (bottomLeftPoints)
                    {
                        bottomLeftPoints.Add(pointItem);
                    }
                }
                else
                {
                    lock (bottomRightPoints)
                    {
                        bottomRightPoints.Add(pointItem);
                    }
                }
            });
        
        this.nodePoints[toNodes.TopLeftId] = topLeftPoints;
        this.nodePoints[toNodes.TopRightId] = topRightPoints;
        this.nodePoints[toNodes.BottomLeftId] = bottomLeftPoints;
        this.nodePoints[toNodes.BottomRightId] = bottomRightPoints;
        this.nodePoints.Remove(nodeId);

        return default;
    }

    public ValueTask LookupPointAsync(
        int nodeId, Point targetPoint, List<KeyValuePair<Point, TValue>> results, CancellationToken ct)
    {
        foreach (var entry in this.nodePoints[nodeId])
        {
            if (targetPoint.Equals(entry.Key))
            {
                results.Add(entry);
            }
        }
        return default;
    }

    public ValueTask LookupBoundAsync(
        int nodeId, Bound targetBound, List<KeyValuePair<Point, TValue>> results, CancellationToken ct)
    {
        foreach (var entry in this.nodePoints[nodeId])
        {
            if (targetBound.IsWithin(entry.Key))
            {
                results.Add(entry);
            }
        }
        return default;
    }

    public async IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(
        int nodeId, Bound targetBound, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var entry in this.nodePoints[nodeId])
        {
            if (targetBound.IsWithin(entry.Key))
            {
                yield return entry;
            }
        }
    }

    public async ValueTask<int> RemovePointsAsync(
        int nodeId, Point point, CancellationToken ct)
    {
        var points = this.nodePoints[nodeId];
        var count = 0;

        for (var index = points.Count - 1; index >= 0; index--)
        {
            var entry = points[index];
            if (point.Equals(entry.Key))
            {
                points.RemoveAt(index);
                count++;
            }
        }
        
        // TODO: rebalance

        return count;
    }

    public async ValueTask<int> RemoveBoundAsync(
        int nodeId, Bound bound, CancellationToken ct)
    {
        var points = this.nodePoints[nodeId];
        var count = 0;

        for (var index = points.Count - 1; index >= 0; index--)
        {
            var entry = points[index];
            if (bound.IsWithin(entry.Key))
            {
                points.RemoveAt(index);
                count++;
            }
        }
        
        // TODO: rebalance

        return count;
    }
}
