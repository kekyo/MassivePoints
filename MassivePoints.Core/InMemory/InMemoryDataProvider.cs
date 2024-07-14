////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.Collections;
using MassivePoints.DataProvider;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MassivePoints.Internal;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998

namespace MassivePoints.InMemory;

/// <summary>
/// Volatile in-memory QuadTree data provider.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public sealed class InMemoryDataProvider<TValue> : IDataProvider<TValue, int>
{
    private readonly AsyncReaderWriterLock locker = new();
    private readonly Dictionary<int, QuadTreeNode<int>> nodes = new();
    private readonly Dictionary<int, ExpandableArray<PointItem<TValue>>> nodePoints = new();
    private readonly Bound entire;
    private readonly int maxNodePoints;
    private int maxNodeId = 0;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="entire">The overall range of the coordinate points managed</param>
    /// <param name="maxNodePoints">Maximum number of coordinate points in each node</param>
    public InMemoryDataProvider(
        Bound entire,
        int maxNodePoints = 65536)
    {
        this.entire = entire;
        this.maxNodePoints = maxNodePoints;
    }

    /// <summary>
    /// Begin a session.
    /// </summary>
    /// <param name="willUpdate">True if possibility changes will be made during the session</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The session</returns>
    public async ValueTask<IDataProviderSession<TValue, int>> BeginSessionAsync(
        bool willUpdate, CancellationToken ct)
    {
        var disposer = await (willUpdate ? this.locker.WriterLockAsync(ct) : this.locker.ReaderLockAsync(ct));
        return new DataProviderSession(this, disposer);
    }

    /// <summary>
    /// Volatile in-memory QuadTree data provider session.
    /// </summary>
    private sealed class DataProviderSession :
        IDataProviderSession<TValue, int>
    {
        private readonly InMemoryDataProvider<TValue> parent;
        private readonly IDisposable disposer;
        
        public DataProviderSession(
            InMemoryDataProvider<TValue> parent,
            IDisposable disposer)
        {
            this.parent = parent;
            this.disposer = disposer;
        }

        public ValueTask DisposeAsync()
        {
            this.disposer.Dispose();
            return default;
        }

        public ValueTask FlushAsync() =>
            default;

        public ValueTask FinishAsync()
        {
            this.disposer.Dispose();
            return default;
        }

        /// <summary>
        /// This indicates the overall range of the coordinate points managed by data provider.
        /// </summary>
        public Bound Entire =>
            this.parent.entire;

        /// <summary>
        /// Maximum number of coordinate points in each node.
        /// </summary>
        public int MaxNodePoints =>
            this.parent.maxNodePoints;

        /// <summary>
        /// Root node ID.
        /// </summary>
        public int RootId => 0;

        /// <summary>
        /// Get information about the node.
        /// </summary>
        /// <param name="nodeId">Node ID</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Node information if available</returns>
        public ValueTask<QuadTreeNode<int>?> GetNodeAsync(
            int nodeId, CancellationToken ct) =>
            new(this.parent.nodes.TryGetValue(nodeId, out var node) ? node : null);

        /// <summary>
        /// Get number of coordinate points on the node.
        /// </summary>
        /// <param name="nodeId">Target node id</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Coordinate point count</returns>
        public ValueTask<int> GetPointCountAsync(
            int nodeId, CancellationToken ct) =>
            new(this.parent.nodePoints.TryGetValue(nodeId, out var points) ? points.Count : 0);

        /// <summary>
        /// Inserts the specified coordinate points.
        /// </summary>
        /// <param name="nodeId">Node ID</param>
        /// <param name="points">Coordinate points</param>
        /// <param name="offset">Coordinate point list offset</param>
        /// <param name="isForceInsert">Force insert all points</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Inserted points</returns>
        public ValueTask<int> InsertPointsAsync(
            int nodeId, IReadOnlyArray<PointItem<TValue>> points, int offset, bool isForceInsert, CancellationToken ct)
        {
            int insertCount;

            if (!this.parent.nodePoints.TryGetValue(nodeId, out var pointItems))
            {
                insertCount = isForceInsert ?
                    points.Count - offset :
                    Math.Min(points.Count - offset, this.MaxNodePoints);
                pointItems = new(insertCount);
                this.parent.nodePoints.Add(nodeId, pointItems);
            }
            else
            {
                insertCount = isForceInsert ?
                    points.Count - offset :
                    Math.Min(points.Count - offset, this.MaxNodePoints - pointItems.Count);
            }

            pointItems.AddRange(points, offset, insertCount);

            return new(insertCount);
        }

        /// <summary>
        /// Distribute coordinate points into the new child nodes.
        /// </summary>
        /// <param name="nodeId">From node id</param>
        /// <param name="toBounds">To child bounds</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Updated node information</returns>
        public async ValueTask<QuadTreeNode<int>> DistributePointsAsync(
            int nodeId, Bound[] toBounds, CancellationToken ct)
        {
            var baseNodeId = this.parent.maxNodeId + 1;
            this.parent.maxNodeId += toBounds.Length;

            var childIds = new int[toBounds.Length];
            for (var index = 0; index < childIds.Length; index++)
            {
                childIds[index] = baseNodeId + index;
            }
            var node = new QuadTreeNode<int>(childIds);

            var points = this.parent.nodePoints[nodeId];
            var toPoints = new ExpandableArray<PointItem<TValue>>[toBounds.Length];

            Parallel.For(
                0, toPoints.Length,
                index =>
                {
                    var toPointList = new ExpandableArray<PointItem<TValue>>();
                    var toBound = toBounds[index];
                    toPointList.AddRangePredicate(points, pointItem => InternalBound.IsWithin(toBound, pointItem.Point));
                    toPoints[index] = toPointList;
                });

            Debug.Assert(toPoints.Sum(pointItems => pointItems.Count) == points.Count);

            var toNodeIds = node.ChildIds;
            for (var index = 0; index < toPoints.Length; index++)
            {
                this.parent.nodePoints.Add(toNodeIds[index], toPoints[index]);
            }

            this.parent.nodePoints.Remove(nodeId);

            this.parent.nodes.Add(nodeId, node);
            return node;
        }

        /// <summary>
        /// Aggregate coordinate points on child nodes to a node.
        /// </summary>
        /// <param name="nodeIds">Child node ids</param>
        /// <param name="toBound">Target bound</param>
        /// <param name="toNodeId">Target node id</param>
        /// <param name="ct">`CancellationToken`</param>
        public ValueTask AggregatePointsAsync(
            int[] nodeIds, Bound toBound, int toNodeId, CancellationToken ct)
        {
            var points = new ExpandableArray<PointItem<TValue>>();

            foreach (var nodeId in nodeIds)
            {
                if (this.parent.nodePoints.TryGetValue(nodeId, out var pointItems))
                {
                    Debug.Assert(pointItems.All(pointItem => InternalBound.IsWithin(toBound, pointItem.Point)));

                    points.AddRange(pointItems);
                    this.parent.nodePoints.Remove(nodeId);
                }
            }

            this.parent.nodePoints.Add(toNodeId, points);

            this.parent.nodes.Remove(toNodeId);
            return default;
        }

        /// <summary>
        /// Lookup coordinate points from a exact point.
        /// </summary>
        /// <param name="nodeId">Target node id</param>
        /// <param name="targetPoint">Target point</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Got coordinate points</returns>
        public ValueTask<PointItem<TValue>[]> LookupPointAsync(
            int nodeId, Point targetPoint, CancellationToken ct) =>
            new(Task.Run(() => this.parent.nodePoints[nodeId].Where(entry => targetPoint.Equals(entry.Point)).ToArray()));

        /// <summary>
        /// Lookup coordinate points from coordinate range.
        /// </summary>
        /// <param name="nodeId">Target node id</param>
        /// <param name="targetBound">Target coordinate range</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Got coordinate points</returns>
        public ValueTask<PointItem<TValue>[]> LookupBoundAsync(
            int nodeId, Bound targetBound, CancellationToken ct) =>
            new(Task.Run(() => this.parent.nodePoints[nodeId].Where(entry => InternalBound.IsWithin(targetBound, entry.Point)).ToArray()));

        /// <summary>
        /// Lookup and streaming coordinate points from coordinate range.
        /// </summary>
        /// <param name="nodeId">Target node id</param>
        /// <param name="targetBound">Target coordinate range</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Coordinate points asynchronous iterator</returns>
        public async IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
            int nodeId, Bound targetBound, [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var entry in this.parent.nodePoints[nodeId])
            {
                if (InternalBound.IsWithin(targetBound, entry.Point))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Remove a coordinate point.
        /// </summary>
        /// <param name="nodeId">Target node id</param>
        /// <param name="point">Target coordinate point</param>
        /// <param name="_">Include coordinate point remains count in result if true</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Removed count and remains count</returns>
        public async ValueTask<RemoveResults> RemovePointAsync(
            int nodeId, Point point, bool _, CancellationToken ct)
        {
            var points = this.parent.nodePoints[nodeId];
            var removed = 0;

            for (var index = points.Count - 1; index >= 0; index--)
            {
                var entry = points[index];
                if (point.Equals(entry.Point))
                {
                    points.RemoveAt(index);
                    removed++;
                }
            }

            return new(removed, points.Count);
        }

        /// <summary>
        /// Remove coordinate points.
        /// </summary>
        /// <param name="nodeId">Target node id</param>
        /// <param name="bound">Target coordinate bound</param>
        /// <param name="_">Include coordinate point remains count in result if true</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Removed count and remains count</returns>
        public async ValueTask<RemoveResults> RemoveBoundAsync(
            int nodeId, Bound bound, bool _, CancellationToken ct)
        {
            var points = this.parent.nodePoints[nodeId];
            var removed = 0;

            for (var index = points.Count - 1; index >= 0; index--)
            {
                var entry = points[index];
                if (InternalBound.IsWithin(bound, entry.Point))
                {
                    points.RemoveAt(index);
                    removed++;
                }
            }

            return new(removed, points.Count);
        }
    }
}
