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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
    QuadTreeSession<TValue, TNodeId>,
    IQuadTreeUpdateSession<TValue>
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="providerSession">Data provider session.</param>
    public QuadTreeUpdateSession(IDataProviderSession<TValue, TNodeId> providerSession) :
        base(providerSession)
    {
    }
    
    /// <summary>
    /// Flush partially data.
    /// </summary>
    public ValueTask FlushAsync() =>
        this.providerSession.FlushAsync();

    /// <summary>
    /// Finish the session.
    /// </summary>
    public ValueTask FinishAsync() =>
        this.providerSession.FinishAsync();
    
    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<int> InsertPointAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        TValue value,
        int nodeDepth,
        CancellationToken ct)
    {
        Bound[] childBounds;
        
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            var inserted = await this.providerSession.InsertPointsAsync(
                nodeId,
                new ReadOnlyArray<PointItem<TValue>>([new(targetPoint, value)]),
                0,
                nodeBound.IsEmpty,
                ct);
            if (inserted >= 1)
            {
                return nodeDepth;
            }

            childBounds = nodeBound.GetChildBounds();
            node = await this.providerSession.DistributePointsAsync(
                nodeId, childBounds, ct);
        }
        else
        {
            childBounds = nodeBound.GetChildBounds();
        }

        var childIds = node.ChildIds;

        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsWithin(targetPoint, false))
            {
                return await this.InsertPointAsync(
                    childId, childBound, targetPoint, value, nodeDepth + 1, ct);
            }
        }

        throw new ArgumentException(
            $"Could not add a coordinate point outside entire bound: Point={targetPoint}");
    }

    /// <summary>
    /// Insert a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="value">Related value</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>A depth value where placed the coordinate point</returns>
    /// <remarks>The depth value indicates how deeply the added coordinate points are placed in the node depth.
    /// This value is not used directly, but can be used as a performance indicator.</remarks>
    public ValueTask<int> InsertPointAsync(
        Point point, TValue value, CancellationToken ct = default) =>
        this.InsertPointAsync(this.providerSession.RootId, this.providerSession.Entire, point, value, 1, ct);

    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<int> InsertPointsCoreAsync(
        TNodeId nodeId,
        Bound nodeBound,
        IReadOnlyArray<PointItem<TValue>> points,
        int bulkInsertBlockSize,
        int nodeDepth,
        CancellationToken ct)
    {
        int offset = 0;
        Bound[] childBounds;

        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            var inserted = await this.providerSession.InsertPointsAsync(
                nodeId,
                points,
                offset,
                nodeBound.IsEmpty,
                ct);
            offset += inserted;
            if (offset >= points.Count)
            {
                return nodeDepth;
            }

            childBounds = nodeBound.GetChildBounds();
            node = await this.providerSession.DistributePointsAsync(
                nodeId, childBounds, ct);
        }
        else
        {
            childBounds = nodeBound.GetChildBounds();
        }

        var childIds = node.ChildIds;

        var splittedLists = new ExpandableArray<PointItem<TValue>>[childIds.Length];

        Parallel.For(
            0, childIds.Length,
            index => 
            {
                var list = new ExpandableArray<PointItem<TValue>>();
                var bound = childBounds[index];
                list.AddRangePredicate(points, offset, pointItem => bound.IsWithin(pointItem.Point, false));
                splittedLists[index] = list;
            });

        var maxNodeDepth = nodeDepth;
        for (var index = 0; index < childIds.Length; index++)
        {
            var list = splittedLists[index];
            splittedLists[index] = null!;   // Will make early collectible by GC.
            if (list.Count >= 1)
            {
                var childId = childIds[index];
                var childBound = childBounds[index];
                maxNodeDepth = Math.Max(maxNodeDepth,
                    await this.InsertPointsAsync(
                        childId, childBound, list, bulkInsertBlockSize, nodeDepth + 1, ct));
            }
        }
        return maxNodeDepth;
    }

    private async ValueTask<int> InsertPointsAsync(
        TNodeId nodeId,
        Bound nodeBound,
        IReadOnlyArray<PointItem<TValue>> points,
        int bulkInsertBlockSize,
        int nodeDepth,
        CancellationToken ct)
    {
        if (points.Count <= bulkInsertBlockSize)
        {
            return await this.InsertPointsCoreAsync(
                nodeId, nodeBound, points, bulkInsertBlockSize, nodeDepth + 1, ct);
        }
        else
        {
            var fixedList = new ExpandableArray<PointItem<TValue>>(bulkInsertBlockSize);
            var maxNodeDepth = nodeDepth;
            foreach (var pointItem in points)
            {
                fixedList.Add(pointItem);
                if (fixedList.Count >= bulkInsertBlockSize)
                {
                    maxNodeDepth = Math.Max(maxNodeDepth,
                        await this.InsertPointsCoreAsync(
                            this.providerSession.RootId,
                            this.providerSession.Entire,
                            fixedList,
                            bulkInsertBlockSize,
                            nodeDepth + 1,
                            ct));
                    fixedList.Clear();
                }
            }
            if (fixedList.Count >= 1)
            {
                maxNodeDepth = Math.Max(maxNodeDepth,
                    await this.InsertPointsCoreAsync(
                        nodeId,
                        nodeBound,
                        fixedList,
                        bulkInsertBlockSize,
                        nodeDepth + 1,
                        ct));
            }
            return maxNodeDepth;
        }
    }

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    public async ValueTask<int> InsertPointsAsync(
        IEnumerable<PointItem<TValue>> points,
        int bulkInsertBlockSize = 100000,
        CancellationToken ct = default)
    {
        if (points is IReadOnlyList<PointItem<TValue>> pointList &&
            pointList.Count < bulkInsertBlockSize)
        {
            return await this.InsertPointsCoreAsync(
                this.providerSession.RootId,
                this.providerSession.Entire,
                new ReadOnlyArray<PointItem<TValue>>(pointList),
                bulkInsertBlockSize,
                1,
                ct);
        }
        else
        {
            var fixedList = new ExpandableArray<PointItem<TValue>>(bulkInsertBlockSize);
            var maxNodeDepth = 0;
            foreach (var pointItem in points)
            {
                fixedList.Add(pointItem);
                if (fixedList.Count >= bulkInsertBlockSize)
                {
                    maxNodeDepth = Math.Max(maxNodeDepth,
                        await this.InsertPointsCoreAsync(
                            this.providerSession.RootId,
                            this.providerSession.Entire,
                            fixedList,
                            bulkInsertBlockSize,
                            1,
                            ct));
                    fixedList.Clear();
                }
            }
            if (fixedList.Count >= 1)
            {
                maxNodeDepth = Math.Max(maxNodeDepth,
                    await this.InsertPointsCoreAsync(
                        this.providerSession.RootId,
                        this.providerSession.Entire,
                        fixedList,
                        bulkInsertBlockSize,
                        1,
                        ct));
            }
            return maxNodeDepth;
        }
    }

    /////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    public async ValueTask<int> InsertPointsAsync(
        IAsyncEnumerable<PointItem<TValue>> points,
        int bulkInsertBlockSize = 100000,
        CancellationToken ct = default)
    {
        var fixedList = new ExpandableArray<PointItem<TValue>>(bulkInsertBlockSize);
        var maxNodeDepth = 0;
        await foreach (var pointItem in points)
        {
            fixedList.Add(pointItem);
            if (fixedList.Count >= bulkInsertBlockSize)
            {
                maxNodeDepth = Math.Max(maxNodeDepth,
                    await this.InsertPointsCoreAsync(
                        this.providerSession.RootId,
                        this.providerSession.Entire,
                        fixedList,
                        bulkInsertBlockSize,
                        1,
                        ct));
                fixedList.Clear();
            }
        }
        if (fixedList.Count >= 1)
        {
            maxNodeDepth = Math.Max(maxNodeDepth,
                await this.InsertPointsCoreAsync(
                    this.providerSession.RootId,
                    this.providerSession.Entire,
                    fixedList,
                    bulkInsertBlockSize,
                    1,
                    ct));
        }
        return maxNodeDepth;
    }

    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<int> GetPointCountAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.providerSession.GetPointCountAsync(nodeId, ct);
        }

        var childIds = node!.ChildIds;
        var childBounds = nodeBound.GetChildBounds();
        var remainsHint = 0;

        for (var index = 0; index < childBounds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            Debug.Assert(!childBound.IsWithin(targetPoint, false));
            remainsHint += await this.GetPointCountAsync(
                childId, childBound, targetPoint, ct);

            // HACK: If remains is exceeded, this node is terminated as there is no further need to examine it.
            if (remainsHint >= this.providerSession.MaxNodePoints)
            {
                break;
            }
        }

        return remainsHint;
    }
    
    private async ValueTask<RemoveResults> RemovePointAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        bool performShrinking,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.providerSession.RemovePointAsync(
                nodeId, targetPoint, performShrinking, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.GetChildBounds();

        if (performShrinking)
        {
            var removed = 0L;
            var remainsHint = 0;

            for (var index = 0; index < childBounds.Length; index++)
            {
                var childId = childIds[index];
                var childBound = childBounds[index];
                if (childBound.IsWithin(targetPoint, false))
                {
                    var (rmd, rms) = await this.RemovePointAsync(
                        childId, childBound, targetPoint, performShrinking, ct);
                    removed += rmd;
                    remainsHint += rms;
                }
                else
                {
                    // HACK: If remains is exceeded, this node is ignored as it does not need to be inspected further.
                    if (remainsHint < this.providerSession.MaxNodePoints)
                    {
                        remainsHint += await this.GetPointCountAsync(
                            childId, childBound, targetPoint, ct);
                    }
                }
            }

            if (remainsHint < this.providerSession.MaxNodePoints)
            {
                await this.providerSession.AggregatePointsAsync(
                    childIds, nodeBound, nodeId, ct);
            }
            return new(removed, remainsHint);
        }
        else
        {
            for (var index = 0; index < childBounds.Length; index++)
            {
                var childId = childIds[index];
                var childBound = childBounds[index];
                if (childBound.IsWithin(targetPoint, false))
                {
                    return await this.RemovePointAsync(
                        childId, childBound, targetPoint, performShrinking, ct);
                }
            }
            return new(0, -1);
        }
    }

    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="point">A coordinate point</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    public async ValueTask<int> RemovePointAsync(
        Point point, bool performShrinking = false, CancellationToken ct = default)
    {
        var (removed, _) = await this.RemovePointAsync(
            this.providerSession.RootId, this.providerSession.Entire, point, performShrinking, ct);
        Debug.Assert(removed <= int.MaxValue);
        return (int)removed;
    }

    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<RemoveResults> RemoveBoundAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Bound targetBound,
        bool performShrinking,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.providerSession.RemoveBoundAsync(
                nodeId, targetBound, performShrinking, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.GetChildBounds();

        if (performShrinking)
        {
            var removed = 0L;
            var remainsHint = 0;

            for (var index = 0; index < childBounds.Length; index++)
            {
                var childId = childIds[index];
                var childBound = childBounds[index];
                if (childBound.IsIntersection(targetBound, false, false))
                {
                    var (rmd, rms) = await this.RemoveBoundAsync(
                        childId, childBound, targetBound, performShrinking, ct);
                    removed += rmd;
                    remainsHint += rms;
                }
                else
                {
                    // HACK: If remains is exceeded, this node is ignored as it does not need to be inspected further.
                    if (remainsHint < this.providerSession.MaxNodePoints)
                    {
                        remainsHint += await this.providerSession.GetPointCountAsync(childId, ct);
                    }
                }
            }

            if (remainsHint < this.providerSession.MaxNodePoints)
            {
                await this.providerSession.AggregatePointsAsync(
                    childIds, nodeBound, nodeId, ct);
            }
            return new(removed, remainsHint);
        }
        else
        {
            var removed = 0L;

            for (var index = 0; index < childBounds.Length; index++)
            {
                var childId = childIds[index];
                var childBound = childBounds[index];
                if (childBound.IsIntersection(targetBound, false, false))
                {
                    var (rmd, _) = await this.RemoveBoundAsync(
                        childId, childBound, targetBound, performShrinking, ct);
                    removed += rmd;
                }
            }

            return new(removed, -1);
        }
    }

    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    public async ValueTask<long> RemoveBoundAsync(
        Bound bound, bool performShrinking = false, CancellationToken ct = default)
    {
        var (removed, _) = await this.RemoveBoundAsync(
            this.providerSession.RootId, this.providerSession.Entire, bound, performShrinking, ct);
        return removed;
    }
}
