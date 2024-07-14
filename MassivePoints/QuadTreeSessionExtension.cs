////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints;

public static class QuadTreeSessionExtension
{
    /// <summary>
    /// Lookup values with a coordinate point.
    /// </summary>
    /// <param name="point">Coordinate point</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public static ValueTask<PointItem<TValue>[]> LookupPointAsync<TValue>(
        this QuadTreeSession<TValue> self,
        Point point, CancellationToken ct = default) =>
        self.internalSession.LookupPointAsync(point, ct);
    
    /// <summary>
    /// Lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values</returns>
    public static ValueTask<PointItem<TValue>[]> LookupBoundAsync<TValue>(
        this QuadTreeSession<TValue> self,
        Bound bound, CancellationToken ct = default) =>
        self.internalSession.LookupBoundAsync(bound, ct);
    
    /// <summary>
    /// Streaming lookup values with coordinate range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Point and values asynchronous iterator</returns>
    public static IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync<TValue>(
        this QuadTreeSession<TValue> self,
        Bound bound, CancellationToken ct = default) =>
        self.internalSession.EnumerateBoundAsync(bound, ct);
}
