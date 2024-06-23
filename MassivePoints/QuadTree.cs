////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

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

public sealed class QuadTree<TValue, TNodeId> : IQuadTree<TValue>
{
    private readonly IQuadTreeProvider<TValue, TNodeId> provider;

    public QuadTree(IQuadTreeProvider<TValue, TNodeId> provider) =>
        this.provider = provider;

    public Bound Entire =>
        this.provider.Entire;

    public ValueTask<ISession> BeginSessionAsync(
        bool willUpdate, CancellationToken ct) =>
        this.provider.BeginSessionAsync(willUpdate, ct);
    
    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<int> AddAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point point,
        TValue value,
        int depth,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            if (!await this.provider.IsDensePointsAsync(nodeId, ct))
            {
                await this.provider.AddPointAsync(nodeId, point, value, ct);
                return depth;
            }
            node = await this.provider.AssignNextNodeSetAsync(nodeId, ct);
            await this.provider.MovePointsAsync(nodeId, nodeBound, node, ct);
        }

        if (nodeBound.TopLeft is { } topLeftBound && topLeftBound.IsWithin(point))
        {
            depth = await AddAsync(node.TopLeftId, topLeftBound, point, value, depth + 1, ct);
        }
        else if (nodeBound.TopRight is { } topRightBound && topRightBound.IsWithin(point))
        {
            depth = await AddAsync(node.TopRightId, topRightBound, point, value, depth + 1, ct);
        }
        else if (nodeBound.BottomLeft is { } bottomLeftBound && bottomLeftBound.IsWithin(point))
        {
            depth = await AddAsync(node.BottomLeftId, bottomLeftBound, point, value, depth + 1, ct);
        }
        else
        {
            var bottomRightBound = nodeBound.BottomRight;
            Debug.Assert(bottomRightBound.IsWithin(point));
            depth = await AddAsync(node.BottomRightId, bottomRightBound, point, value, depth + 1, ct);
        }
        return depth;
    }

    public ValueTask<int> AddAsync(
        Point point, TValue value, CancellationToken ct) =>
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
        }
        else if (nodeBound.TopLeft is { } topLeftBound && topLeftBound.IsWithin(targetPoint))
        {
            await this.LookupPointAsync(node.TopLeftId, topLeftBound, targetPoint, results, ct);
        }
        else if (nodeBound.TopRight is { } topRightBound && topRightBound.IsWithin(targetPoint))
        {
            await this.LookupPointAsync(node.TopRightId, topRightBound, targetPoint, results, ct);
        }
        else if (nodeBound.BottomLeft is { } bottomLeftBound && bottomLeftBound.IsWithin(targetPoint))
        {
            await this.LookupPointAsync(node.BottomLeftId, bottomLeftBound, targetPoint, results, ct);
        }
        else
        {
            await this.LookupPointAsync(node.BottomRightId, nodeBound.BottomRight, targetPoint, results, ct);
        }
    }

    public async ValueTask<KeyValuePair<Point, TValue>[]> LookupPointAsync(
        Point point, CancellationToken ct)
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
        }
        else
        {
            if (nodeBound.TopLeft is { } topLeftBound && topLeftBound.IsIntersection(targetBound))
            {
                await this.LookupBoundAsync(node.TopLeftId, topLeftBound, targetBound, results, ct);
            }
            if (nodeBound.TopRight is { } topRightBound && topRightBound.IsIntersection(targetBound))
            {
                await this.LookupBoundAsync(node.TopRightId, topRightBound, targetBound, results, ct);
            }
            if (nodeBound.BottomLeft is { } bottomLeftBound && bottomLeftBound.IsIntersection(targetBound))
            {
                await this.LookupBoundAsync(node.BottomLeftId, bottomLeftBound, targetBound, results, ct);
            }
            if (nodeBound.BottomRight is { } bottomRightBound && bottomRightBound.IsIntersection(targetBound))
            {
                await this.LookupBoundAsync(node.BottomRightId, bottomRightBound, targetBound, results, ct);
            }
        }
    }

    public async ValueTask<KeyValuePair<Point, TValue>[]> LookupBoundAsync(
        Bound bound, CancellationToken ct)
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
        else
        {
            IAsyncEnumerable<KeyValuePair<Point, TValue>>? results = null;
            
            if (nodeBound.TopLeft is { } topLeftBound && topLeftBound.IsIntersection(targetBound))
            {
                results = results?.Concat(await this.EnumerateBoundAsync(node.TopLeftId, topLeftBound, targetBound, ct), ct) ??
                    await this.EnumerateBoundAsync(node.TopLeftId, topLeftBound, targetBound, ct);
            }
            if (nodeBound.TopRight is { } topRightBound && topRightBound.IsIntersection(targetBound))
            {
                results = results?.Concat(await this.EnumerateBoundAsync(node.TopRightId, topRightBound, targetBound, ct), ct) ??
                    await this.EnumerateBoundAsync(node.TopRightId, topRightBound, targetBound, ct);
            }
            if (nodeBound.BottomLeft is { } bottomLeftBound && bottomLeftBound.IsIntersection(targetBound))
            {
                results = results?.Concat(await this.EnumerateBoundAsync(node.BottomLeftId, bottomLeftBound, targetBound, ct), ct) ??
                    await this.EnumerateBoundAsync(node.BottomLeftId, bottomLeftBound, targetBound, ct);
            }
            if (nodeBound.BottomRight is { } bottomRightBound && bottomRightBound.IsIntersection(targetBound))
            {
                results = results?.Concat(await this.EnumerateBoundAsync(node.BottomRightId, bottomRightBound, targetBound, ct), ct) ??
                    await this.EnumerateBoundAsync(node.BottomRightId, bottomRightBound, targetBound, ct);
            }
            return results ?? Utilities.AsyncEmpty<KeyValuePair<Point, TValue>>();
        }
    }

    public async IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(
        Bound bound, [EnumeratorCancellation] CancellationToken ct)
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

    private async ValueTask<int> RemovePointsAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Point targetPoint,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.provider.RemovePointsAsync(nodeId, targetPoint, ct);
        }
        else if (nodeBound.TopLeft is { } topLeftBound && topLeftBound.IsWithin(targetPoint))
        {
            return await this.RemovePointsAsync(node.TopLeftId, topLeftBound, targetPoint, ct);
        }
        else if (nodeBound.TopRight is { } topRightBound && topRightBound.IsWithin(targetPoint))
        {
            return await this.RemovePointsAsync(node.TopRightId, topRightBound, targetPoint, ct);
        }
        else if (nodeBound.BottomLeft is { } bottomLeftBound && bottomLeftBound.IsWithin(targetPoint))
        {
            return await this.RemovePointsAsync(node.BottomLeftId, bottomLeftBound, targetPoint, ct);
        }
        else
        {
            return await this.RemovePointsAsync(node.BottomRightId, nodeBound.BottomRight, targetPoint, ct);
        }
    }

    public ValueTask<int> RemovePointsAsync(
        Point point, CancellationToken ct) =>
        this.RemovePointsAsync(this.provider.RootId, this.provider.Entire, point, ct);

    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<long> RemoveBoundAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Bound targetBound,
        CancellationToken ct)
    {
        if (await this.provider.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return await this.provider.RemoveBoundAsync(nodeId, targetBound, ct);
        }
        else
        {
            var count = 0L;
            if (nodeBound.TopLeft is { } topLeftBound && topLeftBound.IsIntersection(targetBound))
            {
                count += await this.RemoveBoundAsync(node.TopLeftId, topLeftBound, targetBound, ct);
            }
            if (nodeBound.TopRight is { } topRightBound && topRightBound.IsIntersection(targetBound))
            {
                count += await this.RemoveBoundAsync(node.TopRightId, topRightBound, targetBound, ct);
            }
            if (nodeBound.BottomLeft is { } bottomLeftBound && bottomLeftBound.IsIntersection(targetBound))
            {
                count += await this.RemoveBoundAsync(node.BottomLeftId, bottomLeftBound, targetBound, ct);
            }
            if (nodeBound.BottomRight is { } bottomRightBound && bottomRightBound.IsIntersection(targetBound))
            {
                count += await this.RemoveBoundAsync(node.BottomRightId, bottomRightBound, targetBound, ct);
            }

            return count;
        }
    }

    public ValueTask<long> RemoveBoundAsync(
        Bound bound, CancellationToken ct) =>
        this.RemoveBoundAsync(this.provider.RootId, this.provider.Entire, bound, ct);
}
