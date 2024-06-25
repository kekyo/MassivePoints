////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;

namespace MassivePoints.InMemory;

internal sealed class InMemoryDataSession : ISession
{
    private readonly IDisposable disposer;

    public InMemoryDataSession(IDisposable disposer) =>
        this.disposer = disposer;

    public ValueTask DisposeAsync()
    {
        this.disposer.Dispose();
        return default;
    }

    public ValueTask FinishAsync()
    {
        this.disposer.Dispose();
        return default;
    }
}
