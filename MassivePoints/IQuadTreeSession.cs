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

/// <summary>
/// QuadTree reading session abstraction interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public abstract class QuadTreeSession<TValue> : IAsyncDisposable
{
    /// <summary>
    /// The overall range of the coordinate points managed.
    /// </summary>
    public abstract Bound Entire { get; }

    public abstract ValueTask DisposeAsync();
    
    /// <summary>
    /// Lookup values with a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public abstract ValueTask<PointItem<TValue>[]> LookupPointAsync(
        Point point, CancellationToken ct = default);
    
    /// <summary>
    /// Lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public abstract ValueTask<PointItem<TValue>[]> LookupBoundAsync(
        Bound bound, CancellationToken ct = default);
    
    /// <summary>
    /// Streaming lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values asynchronous iterator</returns>
    public abstract IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
        Bound bound, CancellationToken ct = default);
}
