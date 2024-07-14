////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.DataProvider;
using MassivePoints.Internal;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints;

public static class QuadTreeUpdateSessionExtension
{
    /// <summary>
    /// Flush partially data.
    /// </summary>
    public static ValueTask FlushAsync<TValue>(
        this QuadTreeUpdateSession<TValue> self) =>
        self.internalSession.FlushAsync();

    /// <summary>
    /// Finish the session.
    /// </summary>
    public static ValueTask FinishAsync<TValue>(
        this QuadTreeUpdateSession<TValue> self) =>
        self.internalSession.FinishAsync();

    /// <summary>
    /// Insert a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="value">Related value</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>A node depth value where placed the coordinate point</returns>
    /// <remarks>The node depth value indicates how deeply the added coordinate points are placed in the node depth.
    /// This value is not used directly, but can be used as a performance indicator.</remarks>
    public static ValueTask<int> InsertPointAsync<TValue>(
        this QuadTreeUpdateSession<TValue> self,
        Point point, TValue value, CancellationToken ct = default) =>
        self.internalSession.InsertPointAsync(point, value, ct);

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    public static ValueTask<int> InsertPointsAsync<TValue>(
        this QuadTreeUpdateSession<TValue> self,
        IEnumerable<PointItem<TValue>> points, int bulkInsertBlockSize = 100000, CancellationToken ct = default) =>
        self.internalSession.InsertPointsAsync(points, bulkInsertBlockSize, ct);

    /// <summary>
    /// Bulk insert coordinate points.
    /// </summary>
    /// <param name="points">Coordinate point and values</param>
    /// <param name="bulkInsertBlockSize">Bulk insert block size</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Maximum node depth value where placed the coordinate points</returns>
    public static ValueTask<int> InsertPointsAsync<TValue>(
        this QuadTreeUpdateSession<TValue> self,
        IAsyncEnumerable<PointItem<TValue>> points, int bulkInsertBlockSize = 100000, CancellationToken ct = default) =>
        self.internalSession.InsertPointsAsync(points, bulkInsertBlockSize, ct);

    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="point">A coordinate point</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    public static ValueTask<int> RemovePointAsync<TValue>(
        this QuadTreeUpdateSession<TValue> self,
        Point point, bool performShrinking = false, CancellationToken ct = default) =>
        self.internalSession.RemovePointAsync(point, performShrinking, ct);
    
    /// <summary>
    /// Remove coordinate point and values.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="performShrinking">Index shrinking is performed or not</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Count of removed coordinate points</returns>
    public static ValueTask<long> RemoveBoundAsync<TValue>(
        this QuadTreeUpdateSession<TValue> self,
        Bound bound, bool performShrinking = false, CancellationToken ct = default) =>
        self.internalSession.RemoveBoundAsync(bound, performShrinking, ct);
}
