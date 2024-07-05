////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.Data;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MassivePoints;

[Parallelizable(ParallelScope.All)]
public sealed class QuadTreeTests_SQLite
{
    private static readonly string basePath = Path.Combine("testdb", $"{DateTime.Now:yyyyMMdd_HHmmss}");

    static QuadTreeTests_SQLite()
    {
        try
        {
            Directory.CreateDirectory(basePath);
        }
        catch
        {
        }
    }

    private static DbConnection CreateSQLiteConnection(string prefix)
    {
        var dbFilePath = Path.Combine(basePath, $"{prefix}.db");
#if false
        var connectionString = new SqliteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        return new SqliteConnection(connectionString);
#else
        var connectionString = new SQLiteConnectionStringBuilder()
        {
            DataSource = dbFilePath,
            JournalMode = SQLiteJournalModeEnum.Wal,
        }.ToString();
        return new SQLiteConnection(connectionString);
#endif
    }

    private static Bound GetEntireBound(int dimension) =>
        new(Enumerable.Range(0, dimension).Select(_ => new Axis(0, 100000)).ToArray());
    
    private static Point GetRandomPoint(Random r, int dimension)
    {
        var ps = new double[dimension];
        for (var d = 0; d < dimension; d++)
        {
            ps[d] = r.Next(0, 99999);
        }
        return new Point(ps);
    }
    
    private static Bound GetRandomBound(Random r, int dimension)
    {
        var axes = new Axis[dimension];
        for (var d = 0; d < dimension; d++)
        {
            axes[d] = new Axis(r.Next(0, 49999), r.Next(0, 49999));
        }
        return new Bound(axes);
    }

    //////////////////////////////////////////////////////////////////////////////////

    [TestCase(1, 10, 2)]
    [TestCase(10, 10, 2)]
    [TestCase(11, 10, 2)]
    [TestCase(100000, 1024, 2)]
    [TestCase(1, 10, 3)]
    [TestCase(10, 10, 3)]
    [TestCase(11, 10, 3)]
    [TestCase(100000, 1024, 3)]
    public async Task InsertSqlite(long count, int maxNodePoints, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"insert_{count}_{dimension}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginUpdateSessionAsync();

        try
        {
            var r = new Random();
            var maxDepth = 0;
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                var nodeDepth = await session.InsertPointAsync(
                    point, index);
                maxDepth = Math.Max(maxDepth, nodeDepth);
            }
        }
        finally
        {
            await session.FinishAsync();
        }
    }

    [TestCase(1, 10, 2)]
    [TestCase(10, 10, 2)]
    [TestCase(11, 10, 2)]
    [TestCase(10000, 1024, 2)]
    [TestCase(1, 10, 3)]
    [TestCase(10, 10, 3)]
    [TestCase(11, 10, 3)]
    [TestCase(10000, 1024, 3)]
    public async Task BulklInsertSqlite1(long count, int maxNodePoints, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"bulkinsert1_{count}_{dimension}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);

        var quadTree = QuadTree.Factory.Create(provider);

        var allPoints = new Point[count];

        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            try
            {
                var r = new Random();
                for (var index = 0L; index < count; index += 100000)
                {
                    var points = Enumerable.Range(0, (int)Math.Min(100000, count - index)).
                        Select(i =>
                        {
                            var point = GetRandomPoint(r, dimension);
                            allPoints[index + i] = point;
                            return new PointItem<long>(point, index + i);
                        }).
                        ToArray();

                    await session.InsertPointsAsync(points);
                }
            }
            finally
            {
                await session.FinishAsync();
            }
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            await Task.WhenAll(
                RangeLong(0L, count).
                Select(async index =>
                {
                    var point = allPoints[index];
                    var results = await session.LookupPointAsync(point);

                    var f1 = results.Any(entry => entry.Value == index);
                    Assert.That(f1, Is.True);
                    var f2 = results.All(entry => entry.Point.Equals(point));
                    Assert.That(f2, Is.True);
                }));
        }
    }

    private static IEnumerable<long> RangeLong(long start, long count)
    {
        for (var index = 0L; index < count; index++)
        {
            yield return index + start;
        }
    }

    [TestCase(1, 10, 2)]
    [TestCase(10, 10, 2)]
    [TestCase(11, 10, 2)]
    [TestCase(1000000, 1024, 2)]
    [TestCase(1, 10, 3)]
    [TestCase(10, 10, 3)]
    [TestCase(11, 10, 3)]
    [TestCase(1000000, 1024, 3)]
    public async Task BulklInsertSqlite2(long count, int maxNodePoints, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"bulkinsert2_{count}_{dimension}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);

        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginUpdateSessionAsync();

        try
        {
            var r = new Random();
            await session.InsertPointsAsync(
                RangeLong(0, count).
                Select(index => new PointItem<long>(
                    GetRandomPoint(r, dimension),
                    index)));
        }
        finally
        {
            await session.FinishAsync();
        }
    }

    private static async IAsyncEnumerable<long> RangeLongAsync(long start, long count)
    {
        for (var index = 0L; index < count; index++)
        {
            if ((index % 100000) == 0)
            {
                await Task.Delay(1);   // insert continuation
            }
            yield return index + start;
        }
    }

    private static async IAsyncEnumerable<TR> Select<T, TR>(
        IAsyncEnumerable<T> enumerable, Func<T, TR> selector)
    {
        await foreach (var item in enumerable)
        {
            yield return selector(item);
        }
    }

    [TestCase(1, 10, 2)]
    [TestCase(10, 10, 2)]
    [TestCase(11, 10, 2)]
    [TestCase(1000000, 1024, 2)]
    [TestCase(1, 10, 3)]
    [TestCase(10, 10, 3)]
    [TestCase(11, 10, 3)]
    [TestCase(1000000, 1024, 3)]
    public async Task BulklInsertSqlite3(long count, int maxNodePoints, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"bulkinsert3_{count}_{dimension}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);

        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginUpdateSessionAsync();

        try
        {
            var r = new Random();
            await session.InsertPointsAsync(
                Select(RangeLongAsync(0, count),
                index => new PointItem<long>(
                    GetRandomPoint(r, dimension),
                    index)));
        }
        finally
        {
            await session.FinishAsync();
        }
    }

    [TestCase(10000, 256, 2)]
    [TestCase(10000, 256, 3)]
    public async Task LookupPointSqlite(long count, int maxNodePoints, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"lookup_point_{count}_{dimension}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);
        
        var quadTree = QuadTree.Factory.Create(provider);

        var points = new Point[count];
        
        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            try
            {
                var r = new Random();
                for (var index = 0L; index < count; index++)
                {
                    var point = GetRandomPoint(r, dimension);
                    points[index] = point;
                    await session.InsertPointAsync(point, index);
                }
            }
            finally
            {
                await session.FinishAsync();
            }
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            for (var index = 0L; index < count; index++)
            {
                var point = points[index];
                var results = await session.LookupPointAsync(point);

                var f1 = results.Any(entry => entry.Value == index);
                Assert.That(f1, Is.True);
                var f2 = results.All(entry => entry.Point.Equals(point));
                Assert.That(f2, Is.True);
            }
        }
    }
    
    [TestCase(10000, 256, 2)]
    [TestCase(10000, 256, 3)]
    public async Task LookupBoundSqlite(long count, int maxNodePoints, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"lookup_bound_{count}_{dimension}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);
        
        var quadTree = QuadTree.Factory.Create(provider);

        var points = new Point[count];
        
        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            try
            {
                var r = new Random();
                for (var index = 0L; index < count; index++)
                {
                    var point = GetRandomPoint(r, dimension);
                    points[index] = point;
                    await session.InsertPointAsync(point, index);
                }
            }
            finally
            {
                await session.FinishAsync();
            }
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            // Try random bounds lookup, repeats 1000 times.
            var r = new Random();
            for (var index = 0; index < 1000; index++)
            {
                var bound = GetRandomBound(r, dimension);

                var expected = points.
                    Select((p, index) => (p, index)).
                    Where(entry => bound.IsWithin(entry.p)).
                    OrderBy(entry => entry.index).
                    Select(entry => (long)entry.index).
                    ToArray();
        
                var results = await session.LookupBoundAsync(bound);
                var actual = results.
                    Select(r => r.Value).
                    OrderBy(index => index).
                    ToArray();
        
                Assert.That(actual, Is.EqualTo(expected));
            }
        }
    }
    
    [TestCase(10000, 256, 2)]
    [TestCase(10000, 256, 3)]
    public async Task EnumerateBoundSqlite(long count, int maxNodePoints, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"enumerate_bound_{count}_{dimension}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);
        
        var quadTree = QuadTree.Factory.Create(provider);

        var points = new Point[count];
        
        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            try
            {
                var r = new Random();
                for (var index = 0L; index < count; index++)
                {
                    var point = GetRandomPoint(r, dimension);
                    points[index] = point;
                    await session.InsertPointAsync(point, index);
                }
            }
            finally
            {
                await session.FinishAsync();
            }
        }
        
        await using (var session = await quadTree.BeginSessionAsync())
        {
            // Try random bounds lookup, repeats 100 times.
            var r = new Random();
            for (var index = 0; index < 1000; index++)
            {
                var bound = GetRandomBound(r, dimension);

                var expected = points.
                    Select((p, index) => (p, index)).
                    Where(entry => bound.IsWithin(entry.p)).
                    OrderBy(entry => entry.index).
                    Select(entry => (long)entry.index).
                    ToArray();
        
                var results = new List<PointItem<long>>();
                await foreach (var entry in session.EnumerateBoundAsync(bound))
                {
                    results.Add(entry);
                }
        
                var actual = results.
                    Select(r => r.Value).
                    OrderBy(index => index).
                    ToArray();
        
                Assert.That(actual, Is.EqualTo(expected));
            }
        }
    }
    
    [TestCase(1000, 256, true, 2)]
    [TestCase(10000, 256, false, 2)]
    [TestCase(1000, 256, true, 3)]
    [TestCase(10000, 256, false, 3)]
    public async Task RemovePointsSqlite(long count, int maxNodePoints, bool performShrinking, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"remove_points_{count}_{dimension}{(performShrinking ? "_shr" : "")}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginUpdateSessionAsync();

        try
        {
            var points = new Dictionary<Point, List<long>>();
        
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                if (!points.TryGetValue(point, out var list))
                {
                    list = new();
                    points.Add(point, list);
                }

                list.Add(index);
                await session.InsertPointAsync(point, index);
            }

            foreach (var entry in points)
            {
                var removed = await session.RemovePointsAsync(entry.Key, performShrinking);
                Assert.That(removed, Is.EqualTo(entry.Value.Count));
            }

            var willEmpty = await session.LookupBoundAsync(session.Entire);
            Assert.That(willEmpty.Length, Is.EqualTo(0));
        }
        finally
        {
            await session.FinishAsync();
        }
    }

    [TestCase(10000, 256, true, 2)]
    [TestCase(10000, 256, false, 2)]
    [TestCase(10000, 256, true, 3)]
    [TestCase(10000, 256, false, 3)]
    public async Task RemoveBoundSqlite(long count, int maxNodePoints, bool performShrinking, int dimension)
    {
        var provider = QuadTree.Factory.CreateProvider<long>(() =>
            CreateSQLiteConnection($"remove_bound_{count}_{dimension}{(performShrinking ? "_shr" : "")}"),
            new(GetEntireBound(dimension), maxNodePoints, prefix: "test"));

        await provider.CreateSQLiteTablesAsync(false);
        
        var quadTree = QuadTree.Factory.Create(provider);

        await using var session = await quadTree.BeginUpdateSessionAsync();

        try
        {
            var points = new Dictionary<Point, List<long>>();
        
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                if (!points.TryGetValue(point, out var list))
                {
                    list = new();
                    points.Add(point, list);
                }

                list.Add(index);
                await session.InsertPointAsync(point, index);
            }

            var removed = 0L;
            var childBounds = session.Entire.GetChildBounds();
            
            foreach (var childBound in childBounds)
            {
                removed += await session.RemoveBoundAsync(childBound, performShrinking);
            }
            Assert.That(removed, Is.EqualTo(count));

            var willEmpty = await session.LookupBoundAsync(session.Entire);
            Assert.That(willEmpty.Length, Is.EqualTo(0));
        }
        finally
        {
            await session.FinishAsync();
        }
    }
}
