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
/// This is an interface that defines the session scope.
/// </summary>
/// <remarks>If you do not call `FinishAsync()`, the changes may not be finalized.</remarks>
public interface ISession : IAsyncDisposable
{
    /// <summary>
    /// Finish the session.
    /// </summary>
    ValueTask FinishAsync();
}

/// <summary>
/// QuadTree abstraction interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public interface IQuadTree<TValue>
{
    /// <summary>
    /// The overall range of the coordinate points managed.
    /// </summary>
    Bound Entire { get; }

    /// <summary>
    /// Begin a session.
    /// </summary>
    /// <param name="willUpdate">True if possibility changes will be made during the session</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The session</returns>
    ValueTask<ISession> BeginSessionAsync(
        bool willUpdate, CancellationToken ct = default);

    /// <summary>
    /// Add a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="value">Related value</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>A depth value where placed the coordinate point</returns>
    /// <remarks>The depth value indicates how deeply the added coordinate points are placed in the node depth.
    /// This value is not used directly, but can be used as a performance indicator.</remarks>
    ValueTask<int> AddAsync(
        Point point, TValue value, CancellationToken ct = default);

    /// <summary>
    /// Lookup values with a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    ValueTask<KeyValuePair<Point, TValue>[]> LookupPointAsync(
        Point point, CancellationToken ct = default);
    
    /// <summary>
    /// Lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    ValueTask<KeyValuePair<Point, TValue>[]> LookupBoundAsync(
        Bound bound, CancellationToken ct = default);
    
    /// <summary>
    /// Streaming lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values asynchronous iterator</returns>
    IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(
        Bound bound, CancellationToken ct = default);

    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="point">A coordinate point</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    ValueTask<int> RemovePointsAsync(
        Point point, bool performShrinking = false, CancellationToken ct = default);
    
    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    ValueTask<long> RemoveBoundAsync(
        Bound bound, bool performShrinking = false, CancellationToken ct = default);
}
