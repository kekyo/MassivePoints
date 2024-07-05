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

        public ValueTask<QuadTreeNode<int>?> GetNodeAsync(
            int nodeId, CancellationToken ct) =>
            new(this.parent.nodes.TryGetValue(nodeId, out var node) ? node : null);

        public ValueTask<int> GetPointCountAsync(
            int nodeId, CancellationToken ct) =>
            new(this.parent.nodePoints.TryGetValue(nodeId, out var points) ? points.Count : 0);

        public ValueTask<int> InsertPointsAsync(
            int nodeId, IReadOnlyArray<PointItem<TValue>> points, int offset, CancellationToken ct)
        {
            int insertCount;

            if (!this.parent.nodePoints.TryGetValue(nodeId, out var pointItems))
            {
                insertCount = Math.Min(points.Count - offset, this.MaxNodePoints);
                pointItems = new(insertCount);
                this.parent.nodePoints.Add(nodeId, pointItems);
            }
            else
            {
                insertCount = Math.Min(points.Count - offset, this.MaxNodePoints - pointItems.Count);
            }

            pointItems.AddRange(points, offset, insertCount);

            return new(insertCount);
        }

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

            await Task.WhenAll(
                Enumerable.Range(0, toPoints.Length).
                Select(index => Task.Run(() =>
                {
                    var toPointList = new ExpandableArray<PointItem<TValue>>();
                    toPoints[index] = toPointList;
                    var toBound = toBounds[index];
                    toPointList.AddRangePredicate(points, pointItem => toBound.IsWithin(pointItem.Point));
                })));

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

        public ValueTask AggregatePointsAsync(
            int[] nodeIds, Bound toBound, int toNodeId, CancellationToken ct)
        {
            var points = new ExpandableArray<PointItem<TValue>>();

            foreach (var nodeId in nodeIds)
            {
                if (this.parent.nodePoints.TryGetValue(nodeId, out var pointItems))
                {
                    Debug.Assert(pointItems.All(pointItem => toBound.IsWithin(pointItem.Point)));

                    points.AddRange(pointItems);
                    this.parent.nodePoints.Remove(nodeId);
                }
            }

            this.parent.nodePoints.Add(toNodeId, points);

            this.parent.nodes.Remove(toNodeId);
            return default;
        }

        public ValueTask<PointItem<TValue>[]> LookupPointAsync(
            int nodeId, Point targetPoint, CancellationToken ct) =>
            new(Task.Run(() => this.parent.nodePoints[nodeId].Where(entry => targetPoint.Equals(entry.Point)).ToArray()));

        public ValueTask<PointItem<TValue>[]> LookupBoundAsync(
            int nodeId, Bound targetBound, CancellationToken ct) =>
            new(Task.Run(() => this.parent.nodePoints[nodeId].Where(entry => targetBound.IsWithin(entry.Point)).ToArray()));

        public async IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
            int nodeId, Bound targetBound, [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var entry in this.parent.nodePoints[nodeId])
            {
                if (targetBound.IsWithin(entry.Point))
                {
                    yield return entry;
                }
            }
        }

        public async ValueTask<RemoveResults> RemovePointsAsync(
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

        public async ValueTask<RemoveResults> RemoveBoundAsync(
            int nodeId, Bound bound, bool _, CancellationToken ct)
        {
            var points = this.parent.nodePoints[nodeId];
            var removed = 0;

            for (var index = points.Count - 1; index >= 0; index--)
            {
                var entry = points[index];
                if (bound.IsWithin(entry.Point))
                {
                    points.RemoveAt(index);
                    removed++;
                }
            }

            return new(removed, points.Count);
        }
    }
}
