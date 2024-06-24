////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Data.Common;
using MassivePoints.Collections;
using MassivePoints.Data;

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

public static class QuadTreeFactoryExtension
{
    public static CollectionQuadTreeProvider<TValue> CreateProvider<TValue>(
        this QuadTreeFactory _,
        Bound entire, int maxNodePoints = 65536) =>
        new(entire, maxNodePoints);
    
    public static DbQuadTreeProvider<TValue> CreateProvider<TValue>(
        this QuadTreeFactory _,
        DbConnection connection, string prefix, Bound entire, int maxNodePoints = 1024) =>
        new(connection, prefix, entire, maxNodePoints);
    
    public static IQuadTree<TValue> Create<TValue>(
        this QuadTreeFactory _,
        double width, double height, int maxNodePoints = 65536) =>
        new QuadTree<TValue, int>(new CollectionQuadTreeProvider<TValue>((width, height), maxNodePoints));

    public static IQuadTree<TValue> Create<TValue, TNodeId>(
        this QuadTreeFactory _,
        IDataProvider<TValue, TNodeId> provider) =>
        new QuadTree<TValue, TNodeId>(provider);
}
