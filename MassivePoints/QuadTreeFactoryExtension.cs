﻿////////////////////////////////////////////////////////////////////////////
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

public static class QuadTreeFactoryExtension
{
    /// <summary>
    /// Create an in-memory data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="entire">Entire coordinate range</param>
    /// <param name="maxNodePoints">Maximum number of coordinate points in each node</param>
    /// <returns>Data provider</returns>
    public static InMemoryDataProvider<TValue> CreateProvider<TValue>(
        this QuadTreeFactory _,
        Bound entire, int maxNodePoints = 65536) =>
        new(entire, maxNodePoints);

    /// <summary>
    /// Create ADO.NET data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="connectionFactory">Database connection factory</param>
    /// <param name="configuration">Database configuration</param>
    /// <returns>Data provider</returns>
    /// <remarks>The connection instance returned by the connection factory must be open.</remarks>
    public static DbDataProvider<TValue> CreateProvider<TValue>(
        this QuadTreeFactory _,
        Func<CancellationToken, ValueTask<DbConnection>> connectionFactory,
        DbDataProviderConfiguration configuration) =>
        new(connectionFactory, configuration);

    /// <summary>
    /// Create QuadTree with in-memory data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="width">Entire coordinate range</param>
    /// <param name="height">Entire coordinate range</param>
    /// <returns>2D coordinate points QuadTree instance</returns>
    public static QuadTree<TValue> Create<TValue>(
        this QuadTreeFactory _,
        double width, double height, int maxNodePoints = 65536) =>
        new QuadTree<TValue, int>(
            new InMemoryDataProvider<TValue>(new Bound(width, height), maxNodePoints));

    /// <summary>
    /// Create QuadTree with in-memory data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="width">Entire coordinate range</param>
    /// <param name="height">Entire coordinate range</param>
    /// <param name="depth">Entire coordinate range</param>
    /// <returns>3D coordinate points QuadTree instance</returns>
    public static QuadTree<TValue> Create<TValue>(
        this QuadTreeFactory _,
        double width, double height, double depth, int maxNodePoints = 65536) =>
        new QuadTree<TValue, int>(
            new InMemoryDataProvider<TValue>(new Bound(width, height, depth), maxNodePoints));

    /// <summary>
    /// Create QuadTree with in-memory data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <param name="entire">Entire coordinate range</param>
    /// <returns>QuadTree instance</returns>
    public static QuadTree<TValue> Create<TValue>(
        this QuadTreeFactory _,
        Bound entire, int maxNodePoints = 65536) =>
        new QuadTree<TValue, int>(
            new InMemoryDataProvider<TValue>(entire, maxNodePoints));

    /// <summary>
    /// Create QuadTree with a data provider.
    /// </summary>
    /// <typeparam name="TValue">Coordinate point related value type</typeparam>
    /// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
    /// <param name="provider">Data provider</param>
    /// <returns>QuadTree instance</returns>
    public static QuadTree<TValue> Create<TValue, TNodeId>(
        this QuadTreeFactory _,
        IDataProvider<TValue, TNodeId> provider) =>
        new QuadTree<TValue, TNodeId>(provider);
}
