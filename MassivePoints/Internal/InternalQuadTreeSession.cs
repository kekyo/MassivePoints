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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998
// The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
#pragma warning disable CS8424

namespace MassivePoints.Internal;

/// <summary>
/// QuadTree reading session implementation.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
internal sealed class InternalQuadTreeSession<TValue, TNodeId>
{
    private readonly IDataProviderSession<TValue, TNodeId> providerSession;

    public InternalQuadTreeSession(IDataProviderSession<TValue, TNodeId> providerSession) =>
        this.providerSession = providerSession;

    public Bound Entire =>
        this.providerSession.Entire;

    /////////////////////////////////////////////////////////////////////////////////

    public ValueTask DisposeAsync() =>
        this.providerSession.DisposeAsync();
    
    public ValueTask FlushAsync() =>
        this.providerSession.FlushAsync();

    public ValueTask FinishAsync() =>
        this.providerSession.FinishAsync();

    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<PointItem<TValue>[]> LookupPointAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.providerSession.LookupPointAsync(
                nodeId, targetPoint, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.GetChildBounds();

        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsWithin(targetPoint))
            {
                return await this.LookupPointAsync(
                    childId, childBound, targetPoint, ct);
            }
        }

        return Array.Empty<PointItem<TValue>>();
    }

    public ValueTask<PointItem<TValue>[]> LookupPointAsync(
        Point point, CancellationToken ct) =>
        this.LookupPointAsync(this.providerSession.RootId, this.providerSession.Entire, point, ct);
    
    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask LookupBoundAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Bound targetBound,
        IExpandableArray<PointItem<TValue>[]> results,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            var rs = await this.providerSession.LookupBoundAsync(
                nodeId, targetBound, ct);
            lock (results)
            {
                results.Add(rs);
            }
            return;
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.GetChildBounds();

        await Task.WhenAll(
            childBounds.
            Select((childBound, index) =>
            {
                if (childBound.IsIntersection(targetBound))
                {
                    var childId = childIds[index];
                    return this.LookupBoundAsync(
                        childId, childBound, targetBound, results, ct).
                        AsTask();
                }
                else
                {
                    return Task.CompletedTask;
                }
            }));
    }

    public async ValueTask<PointItem<TValue>[]> LookupBoundAsync(
        Bound bound, CancellationToken ct)
    {
        var results = new ExpandableArray<PointItem<TValue>[]>();
        await this.LookupBoundAsync(this.providerSession.RootId, this.providerSession.Entire, bound, results, ct);
        return results.SelectMany(r => r).ToArray();
    }
    
    /////////////////////////////////////////////////////////////////////////////////

    // I know that this nested awaitable has a strange signature:
    // Instead of performing nested asynchronous iteration and returning the results in every call,
    // we achieve better performance by performing asynchronous iteration only in the leaf nodes.

    private async ValueTask<IAsyncEnumerable<PointItem<TValue>>> EnumerateBoundAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Bound targetBound,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return this.providerSession.EnumerateBoundAsync(
                nodeId, targetBound, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.GetChildBounds();
        IAsyncEnumerable<PointItem<TValue>>? results = null;
            
        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsIntersection(targetBound))
            {
                results = results?.Concat(await this.EnumerateBoundAsync(
                    childId, childBound, targetBound, ct), ct) ??
                    await this.EnumerateBoundAsync(
                        childId, childBound, targetBound, ct);
            }
        }
        
        return results ?? Utilities.AsyncEmpty<PointItem<TValue>>();
    }

    public async IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
        Bound bound, [EnumeratorCancellation] CancellationToken ct)
    {
        // Unwrap all nested asynchronous tasks.
        await foreach (var entry in
            (await this.EnumerateBoundAsync(this.providerSession.RootId, this.providerSession.Entire, bound, ct)).
            WithCancellation(ct))
        {
            yield return entry;
        }
    }
    
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
            if (childBound.IsWithin(targetPoint))
            {
                return await this.InsertPointAsync(
                    childId, childBound, targetPoint, value, nodeDepth + 1, ct);
            }
        }

        throw new ArgumentException(
            $"Could not add a coordinate point outside entire bound: Point={targetPoint}");
    }

    public ValueTask<int> InsertPointAsync(
        Point point, TValue value, CancellationToken ct) =>
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
                list.AddRangePredicate(points, offset, pointItem =>
                {
                    if (pointItem.Value!.Equals(293776162L))
                    {
                        Debugger.Break();
                    }
                    return bound.IsWithin(pointItem.Point);
                });
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

    public async ValueTask<int> InsertPointsAsync(
        IEnumerable<PointItem<TValue>> points,
        int bulkInsertBlockSize,
        CancellationToken ct)
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

    public async ValueTask<int> InsertPointsAsync(
        IAsyncEnumerable<PointItem<TValue>> points,
        int bulkInsertBlockSize,
        CancellationToken ct)
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
            Debug.Assert(!childBound.IsWithin(targetPoint));
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
                if (childBound.IsWithin(targetPoint))
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
                if (childBound.IsWithin(targetPoint))
                {
                    return await this.RemovePointAsync(
                        childId, childBound, targetPoint, performShrinking, ct);
                }
            }
            return new(0, -1);
        }
    }

    public async ValueTask<int> RemovePointAsync(
        Point point, bool performShrinking, CancellationToken ct)
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
                if (childBound.IsIntersection(targetBound))
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
                if (childBound.IsIntersection(targetBound))
                {
                    var (rmd, _) = await this.RemoveBoundAsync(
                        childId, childBound, targetBound, performShrinking, ct);
                    removed += rmd;
                }
            }

            return new(removed, -1);
        }
    }

    public async ValueTask<long> RemoveBoundAsync(
        Bound bound, bool performShrinking, CancellationToken ct)
    {
        var (removed, _) = await this.RemoveBoundAsync(
            this.providerSession.RootId, this.providerSession.Entire, bound, performShrinking, ct);
        return removed;
    }
}
