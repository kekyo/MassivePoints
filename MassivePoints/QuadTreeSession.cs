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
using MassivePoints.Internal;
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

namespace MassivePoints;

/// <summary>
/// QuadTree reading session implementation.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public class QuadTreeSession<TValue, TNodeId> : IQuadTreeSession<TValue>
{
    private protected readonly IDataProviderSession<TValue, TNodeId> providerSession;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="providerSession">Data provider session.</param>
    public QuadTreeSession(IDataProviderSession<TValue, TNodeId> providerSession) =>
        this.providerSession = providerSession;

    /// <summary>
    /// Dispose method.
    /// </summary>
    public ValueTask DisposeAsync() =>
        this.providerSession.DisposeAsync();

    /// <summary>
    /// The overall range of the coordinate points managed.
    /// </summary>
    public Bound Entire =>
        this.providerSession.Entire;

    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask<IReadOnlyArray<PointItem<TValue>>> LookupPointAsync(
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
            if (childBound.IsWithin(targetPoint, false))
            {
                return await this.LookupPointAsync(
                    childId, childBound, targetPoint, ct);
            }
        }

        return ReadOnlyArray<PointItem<TValue>>.Empty;
    }

    /// <summary>
    /// Lookup values with a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public async ValueTask<PointItem<TValue>[]> LookupPointAsync(
        Point point, CancellationToken ct = default)
    {
        var results = await this.LookupPointAsync(
            this.providerSession.RootId,
            this.providerSession.Entire,
            point,
            ct);
        return results.AsArray();
    }
    
    /////////////////////////////////////////////////////////////////////////////////

    private async ValueTask LookupBoundAsync(
        TNodeId nodeId,
        Bound nodeBound,
        Bound targetBound,
        bool inclusiveBoundTo,
        IExpandableArray<IReadOnlyArray<PointItem<TValue>>> results,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            var rs = await this.providerSession.LookupBoundAsync(
                nodeId, targetBound, inclusiveBoundTo, ct);
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
                if (childBound.IsIntersection(targetBound, false, inclusiveBoundTo))
                {
                    var childId = childIds[index];
                    return this.LookupBoundAsync(
                        childId, childBound, targetBound, inclusiveBoundTo, results, ct).
                        AsTask();
                }
                else
                {
                    return Task.CompletedTask;
                }
            }));
    }

    /// <summary>
    /// Lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="isRightClosed">Perform right-closed interval on coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public async ValueTask<PointItem<TValue>[]> LookupBoundAsync(
        Bound bound, bool isRightClosed = false, CancellationToken ct = default)
    {
        var results = new ExpandableArray<IReadOnlyArray<PointItem<TValue>>>();
        await this.LookupBoundAsync(
            this.providerSession.RootId,
            this.providerSession.Entire,
            bound,
            isRightClosed,
            results,
            ct);
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
        bool inclusiveBoundTo,
        CancellationToken ct)
    {
        if (await this.providerSession.GetNodeAsync(nodeId, ct) is not { } node)
        {
            return this.providerSession.EnumerateBoundAsync(
                nodeId, targetBound, inclusiveBoundTo, ct);
        }

        var childIds = node.ChildIds;
        var childBounds = nodeBound.GetChildBounds();
        IAsyncEnumerable<PointItem<TValue>>? results = null;
            
        for (var index = 0; index < childIds.Length; index++)
        {
            var childId = childIds[index];
            var childBound = childBounds[index];
            if (childBound.IsIntersection(targetBound, false, inclusiveBoundTo))
            {
                results = results?.Concat(await this.EnumerateBoundAsync(
                    childId, childBound, targetBound, inclusiveBoundTo, ct), ct) ??
                    await this.EnumerateBoundAsync(
                        childId, childBound, targetBound, inclusiveBoundTo, ct);
            }
        }
        
        return results ?? Utilities.AsyncEmpty<PointItem<TValue>>();
    }

    /// <summary>
    /// Streaming lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="isRightClosed">Perform right-closed interval on coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values asynchronous iterator</returns>
    public async IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
        Bound bound, bool isRightClosed = false, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Unwrap all nested asynchronous tasks.
        await foreach (var entry in
            (await this.EnumerateBoundAsync(
                this.providerSession.RootId,
                this.providerSession.Entire,
                bound,
                isRightClosed,
                ct)).
            WithCancellation(ct))
        {
            yield return entry;
        }
    }
}
