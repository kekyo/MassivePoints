﻿////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MassivePoints.Data;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace MassivePoints;

[Parallelizable(ParallelScope.All)]
public sealed class QuadTreeTests
{
    private static readonly string basePath = Path.Combine("testdb", $"{DateTime.Now:yyyyMMdd_HHmmss}");

    static QuadTreeTests()
    {
        try
        {
            Directory.CreateDirectory(basePath);
        }
        catch
        {
        }
    }
    
    //////////////////////////////////////////////////////////////////////////////////
    
    [TestCase(1, 10)]
    [TestCase(10, 10)]
    [TestCase(11, 10)]
    [TestCase(10000000, 65536)]
    public async Task InsertCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true, default);

        try
        {
            var r = new Random();
            var maxDepth = 0;
            for (var index = 0L; index < count; index++)
            {
                var depth = await quadTree.AddAsync(
                    (r.Next(0, 99999), r.Next(0, 99999)),
                    index,
                    default);
                maxDepth = Math.Max(maxDepth, depth);
            }
        }
        finally
        {
            await session.FinishAsync();
        }
    }
    
    [TestCase(1000000, 1024)]
    public async Task LookupPointCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        var points = new Point[count];
        
        var r = new Random();
        for (var index = 0L; index < count; index++)
        {
            var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
            points[index] = point;
            await quadTree.AddAsync(point, index, default);
        }
        
        for (var index = 0L; index < count; index++)
        {
            var point = points[index];
            var results = await quadTree.LookupPointAsync(point, default);

            var f1 = results.Any(entry => entry.Value == index);
            Assert.That(f1, Is.True);
            var f2 = results.All(entry => entry.Key.Equals(point));
            Assert.That(f2, Is.True);
        }
    }
    
    [TestCase(1000000, 1024)]
    public async Task LookupBoundCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        var points = new Point[count];
        
        var r = new Random();
        for (var index = 0L; index < count; index++)
        {
            var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
            points[index] = point;
            await quadTree.AddAsync(point, index, default);
        }
        
        // Try random bounds lookup, repeats 100 times.
        for (var index = 0; index < 100; index++)
        {
            var point = new Point(r.Next(0, 49999), r.Next(0, 49999));
            var bound = new Bound(point, r.Next(0, 49999), r.Next(0, 49999));

            var expected = points.
                Select((p, index) => (p, index)).
                Where(entry => bound.IsWithin(entry.p)).
                OrderBy(entry => entry.index).
                Select(entry => (long)entry.index).
                ToArray();
            
            var results = await quadTree.LookupBoundAsync(bound, default);
            var actual = results.
                Select(r => r.Value).
                OrderBy(index => index).
                ToArray();
            
            Assert.That(expected, Is.EqualTo(actual));
        }
    }

    [TestCase(1000000, 1024)]
    public async Task EnumerateBoundCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        var points = new Point[count];
        
        var r = new Random();
        for (var index = 0L; index < count; index++)
        {
            var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
            points[index] = point;
            await quadTree.AddAsync(point, index, default);
        }
        
        // Try random bounds lookup, repeats 100 times.
        for (var index = 0; index < 100; index++)
        {
            var point = new Point(r.Next(0, 49999), r.Next(0, 49999));
            var bound = new Bound(point, r.Next(0, 49999), r.Next(0, 49999));

            var expected = points.
                Select((p, index) => (p, index)).
                Where(entry => bound.IsWithin(entry.p)).
                OrderBy(entry => entry.index).
                Select(entry => (long)entry.index).
                ToArray();

            var results = new List<KeyValuePair<Point, long>>();
            await foreach (var entry in quadTree.EnumerateBoundAsync(bound, default))
            {
                results.Add(entry);
            }
            
            var actual = results.
                Select(r => r.Value).
                OrderBy(index => index).
                ToArray();
            
            Assert.That(expected, Is.EqualTo(actual));
        }
    }

    [TestCase(1000000, 1024)]
    public async Task RemovePointsCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        var points = new Dictionary<Point, List<long>>();
        
        var r = new Random();
        for (var index = 0L; index < count; index++)
        {
            var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
            if (!points.TryGetValue(point, out var list))
            {
                list = new();
                points.Add(point, list);
            }

            list.Add(index);
            await quadTree.AddAsync(point, index, default);
        }

        foreach (var entry in points)
        {
            var removed = await quadTree.RemovePointsAsync(entry.Key, default);
            Assert.That(entry.Value.Count, Is.EqualTo(removed));
        }

        var willEmpty = await quadTree.LookupBoundAsync(quadTree.Entire, default);
        Assert.That(willEmpty.Length, Is.EqualTo(0));
    }

    [TestCase(1000000, 1024)]
    public async Task RemoveBoundCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        var points = new Dictionary<Point, List<long>>();
        
        var r = new Random();
        for (var index = 0L; index < count; index++)
        {
            var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
            if (!points.TryGetValue(point, out var list))
            {
                list = new();
                points.Add(point, list);
            }

            list.Add(index);
            await quadTree.AddAsync(point, index, default);
        }

        var removed = await quadTree.RemoveBoundAsync(quadTree.Entire.TopLeft, default);
        removed += await quadTree.RemoveBoundAsync(quadTree.Entire.TopRight, default);
        removed += await quadTree.RemoveBoundAsync(quadTree.Entire.BottomLeft, default);
        removed += await quadTree.RemoveBoundAsync(quadTree.Entire.BottomRight, default);
        Assert.That(count, Is.EqualTo(removed));

        var willEmpty = await quadTree.LookupBoundAsync(quadTree.Entire, default);
        Assert.That(willEmpty.Length, Is.EqualTo(0));
    }

    //////////////////////////////////////////////////////////////////////////////////
    
    [TestCase(1, 10)]
    [TestCase(10, 10)]
    [TestCase(11, 10)]
    [TestCase(1000000, 1024)]
    public async Task InsertSqlite(long count, int maxNodePoints)
    {
        var dbFilePath = Path.Combine(basePath, $"insert_{count}.db");
        var connectionString = new SqliteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(default);

        var provider = QuadTree.Factory.CreateProvider<long>(connection, "test", (100000, 100000), maxNodePoints);

        await provider.CreateSQLiteTablesAsync(false, default);
        await provider.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, default);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginSessionAsync(true, default);

        try
        {
            var r = new Random();
            var maxDepth = 0;
            for (var index = 0L; index < count; index++)
            {
                var depth = await quadTree.AddAsync(
                    (r.Next(0, 99999), r.Next(0, 99999)),
                    index,
                    default);
                maxDepth = Math.Max(maxDepth, depth);
            }
        }
        finally
        {
            await session.FinishAsync();
        }
    }
    
    [TestCase(100000, 256)]
    public async Task LookupPointSqlite(long count, int maxNodePoints)
    {
        var dbFilePath = Path.Combine(basePath, $"lookup_point_{count}.db");
        var connectionString = new SqliteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(default);

        var provider = QuadTree.Factory.CreateProvider<long>(connection, "test", (100000, 100000), maxNodePoints);

        await provider.CreateSQLiteTablesAsync(false, default);
        await provider.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, default);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginSessionAsync(true, default);

        try
        {
            var points = new Point[count];
        
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                points[index] = point;
                await quadTree.AddAsync(point, index, default);
            }
        
            for (var index = 0L; index < count; index++)
            {
                var point = points[index];
                var results = await quadTree.LookupPointAsync(point, default);

                var f1 = results.Any(entry => entry.Value == index);
                Assert.That(f1, Is.True);
                var f2 = results.All(entry => entry.Key.Equals(point));
                Assert.That(f2, Is.True);
            }
        }
        finally
        {
            await session.FinishAsync();
        }
    }
    
    [TestCase(100000, 256)]
    public async Task LookupBoundSqlite(long count, int maxNodePoints)
    {
        var dbFilePath = Path.Combine(basePath, $"lookup_bound_{count}.db");
        var connectionString = new SqliteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(default);

        var provider = QuadTree.Factory.CreateProvider<long>(connection, "test", (100000, 100000), maxNodePoints);

        await provider.CreateSQLiteTablesAsync(false, default);
        await provider.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, default);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginSessionAsync(true, default);

        try
        {
            var points = new Point[count];
        
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                points[index] = point;
                await quadTree.AddAsync(point, index, default);
            }
        
            // Try random bounds lookup, repeats 100 times.
            for (var index = 0; index < 100; index++)
            {
                var point = new Point(r.Next(0, 49999), r.Next(0, 49999));
                var bound = new Bound(point, r.Next(0, 49999), r.Next(0, 49999));

                var expected = points.
                    Select((p, index) => (p, index)).
                    Where(entry => bound.IsWithin(entry.p)).
                    OrderBy(entry => entry.index).
                    Select(entry => (long)entry.index).
                    ToArray();
            
                var results = await quadTree.LookupBoundAsync(bound, default);
                var actual = results.
                    Select(r => r.Value).
                    OrderBy(index => index).
                    ToArray();
            
                Assert.That(expected, Is.EqualTo(actual));
            }
        }
        finally
        {
            await session.FinishAsync();
        }
    }
    
    [TestCase(100000, 256)]
    public async Task EnumerateBoundSqlite(long count, int maxNodePoints)
    {
        var dbFilePath = Path.Combine(basePath, $"enumerate_bound_{count}.db");
        var connectionString = new SqliteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(default);

        var provider = QuadTree.Factory.CreateProvider<long>(connection, "test", (100000, 100000), maxNodePoints);

        await provider.CreateSQLiteTablesAsync(false, default);
        await provider.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, default);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginSessionAsync(true, default);

        try
        {
            var points = new Point[count];
        
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                points[index] = point;
                await quadTree.AddAsync(point, index, default);
            }
        
            // Try random bounds lookup, repeats 100 times.
            for (var index = 0; index < 100; index++)
            {
                var point = new Point(r.Next(0, 49999), r.Next(0, 49999));
                var bound = new Bound(point, r.Next(0, 49999), r.Next(0, 49999));

                var expected = points.
                    Select((p, index) => (p, index)).
                    Where(entry => bound.IsWithin(entry.p)).
                    OrderBy(entry => entry.index).
                    Select(entry => (long)entry.index).
                    ToArray();
            
                var results = new List<KeyValuePair<Point, long>>();
                await foreach (var entry in quadTree.EnumerateBoundAsync(bound, default))
                {
                    results.Add(entry);
                }
            
                var actual = results.
                    Select(r => r.Value).
                    OrderBy(index => index).
                    ToArray();
            
                Assert.That(expected, Is.EqualTo(actual));
            }
        }
        finally
        {
            await session.FinishAsync();
        }
    }
    
    [TestCase(100000, 256)]
    public async Task RemovePointsSqlite(long count, int maxNodePoints)
    {
        var dbFilePath = Path.Combine(basePath, $"remove_points_{count}.db");
        var connectionString = new SqliteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(default);

        var provider = QuadTree.Factory.CreateProvider<long>(connection, "test", (100000, 100000), maxNodePoints);

        await provider.CreateSQLiteTablesAsync(false, default);
        await provider.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, default);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginSessionAsync(true, default);

        try
        {
            var points = new Dictionary<Point, List<long>>();
        
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                if (!points.TryGetValue(point, out var list))
                {
                    list = new();
                    points.Add(point, list);
                }

                list.Add(index);
                await quadTree.AddAsync(point, index, default);
            }

            foreach (var entry in points)
            {
                var removed = await quadTree.RemovePointsAsync(entry.Key, default);
                Assert.That(entry.Value.Count, Is.EqualTo(removed));
            }

            var willEmpty = await quadTree.LookupBoundAsync(quadTree.Entire, default);
            Assert.That(willEmpty.Length, Is.EqualTo(0));
        }
        finally
        {
            await session.FinishAsync();
        }
    }
    
    [TestCase(100000, 256)]
    public async Task RemoveBoundSqlite(long count, int maxNodePoints)
    {
        var dbFilePath = Path.Combine(basePath, $"remove_bound_{count}.db");
        var connectionString = new SqliteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(default);

        var provider = QuadTree.Factory.CreateProvider<long>(connection, "test", (100000, 100000), maxNodePoints);

        await provider.CreateSQLiteTablesAsync(false, default);
        await provider.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, default);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginSessionAsync(true, default);

        try
        {
            var points = new Dictionary<Point, List<long>>();
        
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                if (!points.TryGetValue(point, out var list))
                {
                    list = new();
                    points.Add(point, list);
                }

                list.Add(index);
                await quadTree.AddAsync(point, index, default);
            }

            var removed = await quadTree.RemoveBoundAsync(quadTree.Entire.TopLeft, default);
            removed += await quadTree.RemoveBoundAsync(quadTree.Entire.TopRight, default);
            removed += await quadTree.RemoveBoundAsync(quadTree.Entire.BottomLeft, default);
            removed += await quadTree.RemoveBoundAsync(quadTree.Entire.BottomRight, default);
            Assert.That(count, Is.EqualTo(removed));

            var willEmpty = await quadTree.LookupBoundAsync(quadTree.Entire, default);
            Assert.That(willEmpty.Length, Is.EqualTo(0));
        }
        finally
        {
            await session.FinishAsync();
        }
    }
}