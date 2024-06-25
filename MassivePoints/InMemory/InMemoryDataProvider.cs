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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998

namespace MassivePoints.InMemory;

/// <summary>
/// Volatile in-memory QuadTree data provider.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public sealed class InMemoryDataProvider<TValue> :
    IDataProvider<TValue, int>
{
    private readonly AsyncReaderWriterLock locker = new();
    private readonly Dictionary<int, QuadTreeNode<int>> nodes = new();
    private readonly Dictionary<int, List<KeyValuePair<Point, TValue>>> nodePoints = new();
    private int maxNodeId = 0;

    public InMemoryDataProvider(
        Bound entire,
        int maxNodePoints = 65536)
    {
        this.Entire = entire;
        this.MaxNodePoints = maxNodePoints;
    }

    public ValueTask DisposeAsync()
    {
        this.nodes.Clear();
        return default;
    }

    /// <summary>
    /// This indicates the overall range of the coordinate points managed by data provider.
    /// </summary>
    public Bound Entire { get; }

    /// <summary>
    /// Maximum number of coordinate points in each node.
    /// </summary>
    public int MaxNodePoints { get; }

    /// <summary>
    /// Root node ID.
    /// </summary>
    public int RootId => 0;

    /// <summary>
    /// Begin a session.
    /// </summary>
    /// <param name="willUpdate">True if possibility changes will be made during the session</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The session</returns>
    public async ValueTask<ISession> BeginSessionAsync(
        bool willUpdate, CancellationToken ct)
    {
        var disposer = await (willUpdate ?
            this.locker.WriterLockAsync(ct) : this.locker.ReaderLockAsync(ct));
        return new InMemoryDataSession(disposer);
    }

    public ValueTask<QuadTreeNode<int>?> GetNodeAsync(
        int nodeId, CancellationToken ct) =>
        new((this.nodes.TryGetValue(nodeId, out var node) && node.TopLeftId != -1) ?
            new QuadTreeNode<int>(node.TopLeftId, node.TopRightId, node.BottomLeftId, node.BottomRightId) : null);

    public ValueTask<int> GetPointCountAsync(
        int nodeId, CancellationToken ct) =>
        new(this.nodePoints.TryGetValue(nodeId, out var points) ?
            points.Count : 0);

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

    public ValueTask<QuadTreeNode<int>> DistributePointsAsync(
        int nodeId, Bound[] toBounds, CancellationToken ct)
    {
        var baseNodeId = this.maxNodeId;
        this.maxNodeId += 4;
        var node = new QuadTreeNode<int>(baseNodeId + 1, baseNodeId + 2, baseNodeId + 3, baseNodeId + 4);

        var points = this.nodePoints[nodeId];
        var toPoints = new List<KeyValuePair<Point, TValue>>[toBounds.Length];

        Parallel.For(
            0, toPoints.Length,
            index =>
            {
                var toPointList = new List<KeyValuePair<Point, TValue>>();
                toPoints[index] = toPointList;
                var toBound = toBounds[index];
                
                foreach (var pointItem in points)
                {
                    if (toBound.IsWithin(pointItem.Key))
                    {
                        toPointList.Add(pointItem);
                    }
                }
            });

        Debug.Assert(toPoints.Sum(pointItems => pointItems.Count) == points.Count);

        var toNodeIds = node.ChildIds;
        for (var index = 0; index < toPoints.Length; index++)
        {
            this.nodePoints.Add(toNodeIds[index], toPoints[index]);
        }
        this.nodePoints.Remove(nodeId);

        this.nodes.Add(nodeId, node);
        return new(node);
    }

    public ValueTask AggregatePointsAsync(
        int[] nodeIds, Bound toBound, int toNodeId, CancellationToken ct)
    {
        var points = new List<KeyValuePair<Point, TValue>>();

        foreach (var nodeId in nodeIds)
        {
            if (this.nodePoints.TryGetValue(nodeId, out var pointItems))
            {
                Debug.Assert(pointItems.All(pointItem => toBound.IsWithin(pointItem.Key)));

                points.AddRange(pointItems);
                this.nodePoints.Remove(nodeId);
            }
        }
        this.nodePoints.Add(toNodeId, points);

        this.nodes.Remove(toNodeId);
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

    public async ValueTask<RemoveResults> RemovePointsAsync(
        int nodeId, Point point, bool _, CancellationToken ct)
    {
        var points = this.nodePoints[nodeId];
        var removed = 0;

        for (var index = points.Count - 1; index >= 0; index--)
        {
            var entry = points[index];
            if (point.Equals(entry.Key))
            {
                points.RemoveAt(index);
                removed++;
            }
        }
        
        return new(removed, points.Count);
    }

    public async ValueTask<RemoveResults> RemoveBoundAsync(
        int nodeId, Bound bound, bool _, CancellationToken ct)
    {
        var points = this.nodePoints[nodeId];
        var removed = 0;

        for (var index = points.Count - 1; index >= 0; index--)
        {
            var entry = points[index];
            if (bound.IsWithin(entry.Key))
            {
                points.RemoveAt(index);
                removed++;
            }
        }

        return new(removed, points.Count);
    }
}
