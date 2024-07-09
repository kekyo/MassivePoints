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
/// QuadTree update session abstraction interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public interface IQuadTreeUpdateSession<TValue> : IQuadTreeSession<TValue>
{
    /// <summary>
    /// Flush partially data.
    /// </summary>
    ValueTask FlushAsync();

    /// <summary>
    /// Finish the session.
    /// </summary>
    ValueTask FinishAsync();
    
    /// <summary>
    /// Insert a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="value">Related value</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>A node depth value where placed the coordinate point</returns>
    /// <remarks>The node depth value indicates how deeply the added coordinate points are placed in the node depth.
    /// This value is not used directly, but can be used as a performance indicator.</remarks>
    ValueTask<int> InsertPointAsync(
        Point point, TValue value, CancellationToken ct = default);

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    ValueTask<int> InsertPointsAsync(
        IEnumerable<PointItem<TValue>> points, int bulkInsertBlockSize = 100000, CancellationToken ct = default);

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    ValueTask<int> InsertPointsAsync(
        IAsyncEnumerable<PointItem<TValue>> points, int bulkInsertBlockSize = 100000, CancellationToken ct = default);

    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="point">A coordinate point</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    ValueTask<int> RemovePointAsync(
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
