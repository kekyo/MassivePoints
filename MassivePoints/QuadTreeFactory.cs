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

public static class QuadTreeFactoryExtension
{
    /// <summary>
    /// Create an in-memory data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="width">Entire coordinate range</param>
    /// <param name="height">Entire coordinate range</param>
    /// <param name="maxNodePoints">Maximum number of coordinate points in each node</param>
    /// <returns>Data provider</returns>
    public static CollectionQuadTreeProvider<TValue> CreateProvider<TValue>(
        this QuadTreeFactory _,
        double width, double height, int maxNodePoints = 65536) =>
        new((width, height), maxNodePoints);

    /// <summary>
    /// Create an in-memory data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="entire">Entire coordinate range</param>
    /// <param name="maxNodePoints">Maximum number of coordinate points in each node</param>
    /// <returns>Data provider</returns>
    public static CollectionQuadTreeProvider<TValue> CreateProvider<TValue>(
        this QuadTreeFactory _,
        Bound entire, int maxNodePoints = 65536) =>
        new(entire, maxNodePoints);

    /// <summary>
    /// Create ADO.NET data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="connection">Database connection</param>
    /// <param name="prefix">Database metadata symbol prefix</param>
    /// <param name="entire">Entire coordinate range</param>
    /// <param name="maxNodePoints">Maximum number of coordinate points in each node</param>
    /// <returns>Data provider</returns>
    public static DbQuadTreeProvider<TValue> CreateProvider<TValue>(
        this QuadTreeFactory _,
        DbConnection connection, string prefix, Bound entire, int maxNodePoints = 1024) =>
        new(connection, prefix, entire, maxNodePoints);

    /// <summary>
    /// Create QuadTree with in-memory data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="width">Entire coordinate range</param>
    /// <param name="height">Entire coordinate range</param>
    /// <returns>QuadTree instance</returns>
    public static IQuadTree<TValue> Create<TValue>(
        this QuadTreeFactory _,
        double width, double height, int maxNodePoints = 65536) =>
        new QuadTree<TValue, int>(
            new CollectionQuadTreeProvider<TValue>((width, height), maxNodePoints));

    /// <summary>
    /// Create QuadTree.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
    /// <param name="provider">Data provider</param>
    /// <returns>QuadTree instance</returns>
    public static IQuadTree<TValue> Create<TValue, TNodeId>(
        this QuadTreeFactory _,
        IDataProvider<TValue, TNodeId> provider) =>
        new QuadTree<TValue, TNodeId>(provider);
}
