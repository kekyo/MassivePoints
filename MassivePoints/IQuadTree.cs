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
/// QuadTree abstraction interface.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public abstract class QuadTree<TValue>
{
    /// <summary>
    /// Begin a reading session.
    /// </summary>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The reading session</returns>
    public abstract ValueTask<QuadTreeSession<TValue>> BeginSessionAsync(
        CancellationToken ct = default);
    
    /// <summary>
    /// Begin an update session.
    /// </summary>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The update session</returns>
    public abstract ValueTask<QuadTreeUpdateSession<TValue>> BeginUpdateSessionAsync(
        CancellationToken ct = default);
}
