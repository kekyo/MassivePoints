﻿////////////////////////////////////////////////////////////////////////////
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MassivePoints.Internal;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998
// The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
#pragma warning disable CS8424

namespace MassivePoints;

/// <summary>
/// QuadTree implementation.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
public sealed class QuadTree<TValue, TNodeId> : IQuadTree<TValue>
{
    private readonly IDataProvider<TValue, TNodeId> provider;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="provider">Backend data provider</param>
    public QuadTree(
        IDataProvider<TValue, TNodeId> provider) =>
        this.provider = provider;

    /// <summary>
    /// The overall range of the coordinate points managed.
    /// </summary>
    public Bound Entire =>
        this.provider.Entire;

    /// <summary>
    /// Begin a session.
    /// </summary>
    /// <param name="willUpdate">True if possibility changes will be made during the session</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The session</returns>
    public ValueTask<ISession> BeginSessionAsync(
        bool willUpdate, CancellationToken ct = default) =>
        this.provider.BeginSessionAsync(willUpdate, ct);
    
    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<int> AddAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        TValue value,
        int depth,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            var count = await this.provider.GetPointCountAsync(nodeId, ct);
            if (count < this.provider.MaxNodePoints)
            {
                await this.provider.AddPointAsync(nodeId, targetPoint, value, ct);
                return depth;
            }

            node = await this.provider.DistributePointsAsync(
                nodeId, nodeBound.ChildBounds, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.ChildBounds;

        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsWithin(targetPoint))
            {
                return await AddAsync(childId, childBound, targetPoint, value, depth + 1, ct);
            }
        }

        throw new ArgumentException(
            $"Could not add a coordinate point outside entire bound: Point={targetPoint}");
    }

    /// <summary>
    /// Add a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="value">Related value</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>A depth value where placed the coordinate point</returns>
    /// <remarks>The depth value indicates how deeply the added coordinate points are placed in the node depth.
    /// This value is not used directly, but can be used as a performance indicator.</remarks>
    public ValueTask<int> AddAsync(
        Point point, TValue value, CancellationToken ct = default) =>
        this.AddAsync(this.provider.RootId, this.provider.Entire, point, value, 0, ct);
        
    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask LookupPointAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        List<KeyValuePair<Point, TValue>> results,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            await this.provider.LookupPointAsync(nodeId, targetPoint, results, ct);
            return;
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.ChildBounds;

        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsWithin(targetPoint))
            {
                await this.LookupPointAsync(childId, childBound, targetPoint, results, ct);
                return;
            }
        }
    }

    /// <summary>
    /// Lookup values with a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public async ValueTask<KeyValuePair<Point, TValue>[]> LookupPointAsync(
        Point point, CancellationToken ct = default)
    {
        var results = new List<KeyValuePair<Point, TValue>>();
        await this.LookupPointAsync(this.provider.RootId, this.provider.Entire, point, results, ct);
        return results.ToArray();
    }
    
    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask LookupBoundAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Bound targetBound,
        List<KeyValuePair<Point, TValue>> results,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            await this.provider.LookupBoundAsync(nodeId, targetBound, results, ct);
            return;
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.ChildBounds;

        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsIntersection(targetBound))
            {
                await this.LookupBoundAsync(childId, childBound, targetBound, results, ct);
            }
        }
    }

    /// <summary>
    /// Lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public async ValueTask<KeyValuePair<Point, TValue>[]> LookupBoundAsync(
        Bound bound, CancellationToken ct = default)
    {
        var results = new List<KeyValuePair<Point, TValue>>();
        await this.LookupBoundAsync(this.provider.RootId, this.provider.Entire, bound, results, ct);
        return results.ToArray();
    }
    
    /////////////////////////////////////////////////////////////////////////////////

    // I know that this nested awaitable has a strange signature:
    // Instead of performing nested asynchronous iteration and returning the results in every call,
    // we achieve better performance by performing asynchronous iteration only in the leaf nodes.

    private async ValueTask<IAsyncEnumerable<KeyValuePair<Point, TValue>>> EnumerateBoundAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Bound targetBound,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return this.provider.EnumerateBoundAsync(nodeId, targetBound, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.ChildBounds;
        IAsyncEnumerable<KeyValuePair<Point, TValue>>? results = null;
            
        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsIntersection(targetBound))
            {
                results = results?.Concat(await this.EnumerateBoundAsync(childId, childBound, targetBound, ct), ct) ??
                    await this.EnumerateBoundAsync(childId, childBound, targetBound, ct);
            }
        }
        
        return results ?? Utilities.AsyncEmpty<KeyValuePair<Point, TValue>>();
    }

    /// <summary>
    /// Streaming lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values asynchronous iterator</returns>
    public async IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(
        Bound bound, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Unwrap all nested asynchronous tasks.
        await foreach (var entry in
            (await this.EnumerateBoundAsync(this.provider.RootId, this.provider.Entire, bound, ct)).
            WithCancellation(ct))
        {
            yield return entry;
        }
    }

    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<int> GetPointCountAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.provider.GetPointCountAsync(nodeId, ct);
        }

        var childIds = node!.ChildIds;
        var childBounds = nodeBound.ChildBounds;
        var remainsHint = 0;

        for (var index = 0; index < childBounds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            Debug.Assert(!childBound.IsWithin(targetPoint));
            remainsHint += await this.GetPointCountAsync(childId, childBound, targetPoint, ct);

            // HACK: If remains is exceeded, this node is terminated as there is no further need to examine it.
            if (remainsHint >= this.provider.MaxNodePoints)
            {
                break;
            }
        }

        return remainsHint;
    }
    
    private async ValueTask<RemoveResults> RemovePointsAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        bool performShrinking,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.provider.RemovePointsAsync(
                nodeId, targetPoint, performShrinking, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.ChildBounds;

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
                    var (rmd, rms) = await this.RemovePointsAsync(
                        childId, childBound, targetPoint, performShrinking, ct);
                    removed += rmd;
                    remainsHint += rms;
                }
                else
                {
                    // HACK: If remains is exceeded, this node is ignored as it does not need to be inspected further.
                    if (remainsHint < this.provider.MaxNodePoints)
                    {
                        remainsHint += await this.GetPointCountAsync(childId, childBound, targetPoint, ct);
                    }
                }
            }

            if (remainsHint < this.provider.MaxNodePoints)
            {
                await this.provider.AggregatePointsAsync(childIds, nodeBound, nodeId, ct);
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
                    return await this.RemovePointsAsync(
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
    public async ValueTask<int> RemovePointsAsync(
        Point point, bool performShrinking = false, CancellationToken ct = default)
    {
        var (removed, _) = await this.RemovePointsAsync(
            this.provider.RootId, this.provider.Entire, point, performShrinking, ct);
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
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.provider.RemoveBoundAsync(
                nodeId, targetBound, performShrinking, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.ChildBounds;

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
                    if (remainsHint < this.provider.MaxNodePoints)
                    {
                        remainsHint += await this.provider.GetPointCountAsync(childId, ct);
                    }
                }
            }

            if (remainsHint < this.provider.MaxNodePoints)
            {
                await this.provider.AggregatePointsAsync(childIds, nodeBound, nodeId, ct);
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
            this.provider.RootId, this.provider.Entire, bound, performShrinking, ct);
        return removed;
    }
}
