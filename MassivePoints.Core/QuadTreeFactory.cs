////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.Data;
using MassivePoints.DataProvider;
using MassivePoints.InMemory;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints;

public sealed class QuadTreeFactory
{
    internal QuadTreeFactory()
    {
    }
}

public static class QuadTree
{
    public static readonly QuadTreeFactory Factory = new();
}
