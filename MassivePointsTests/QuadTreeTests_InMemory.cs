////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998

namespace MassivePoints;

[Parallelizable(ParallelScope.All)]
public sealed class QuadTreeTests_InMemory
{
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
    [TestCase(1000000, 1024, 2)]
    [TestCase(1, 10, 3)]
    [TestCase(10, 10, 3)]
    [TestCase(11, 10, 3)]
    [TestCase(1000000, 256, 3)]
    public async Task InsertCollection(long count, int maxNodePoints, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

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
    [TestCase(1000000, 256, 3)]
    public async Task BulkInsertCollection1(long count, int maxNodePoints, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

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

    [TestCase(1, 10, 2)]
    [TestCase(10, 10, 2)]
    [TestCase(11, 10, 2)]
    [TestCase(10000000, 65536, 2)]
    [TestCase(1, 10, 3)]
    [TestCase(10, 10, 3)]
    [TestCase(11, 10, 3)]
    [TestCase(10000000, 1024, 3)]
    public async Task BulkInsertCollection2(long count, int maxNodePoints, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

        await using var session = await quadTree.BeginUpdateSessionAsync();

        try
        {
            var r = new Random();
            await session.InsertPointsAsync(
                RangeLong(0, count).
                Select(index => new PointItem<long>(
                    GetRandomPoint(r, dimension), index)));
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
    [TestCase(10000000, 65536, 2)]
    [TestCase(1, 10, 3)]
    [TestCase(10, 10, 3)]
    [TestCase(11, 10, 3)]
    [TestCase(10000000, 1024, 3)]
    public async Task BulkInsertCollection3(long count, int maxNodePoints, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

        await using var session = await quadTree.BeginUpdateSessionAsync();

        try
        {
            var r = new Random();
            await session.InsertPointsAsync(
                Select(RangeLongAsync(0, count),
                    index => new PointItem<long>(
                        GetRandomPoint(r, dimension), index)));
        }
        finally
        {
            await session.FinishAsync();
        }
    }

    [TestCase(100000, 1024, 2)]
    [TestCase(100000, 256, 3)]
    public async Task LookupPointCollection(long count, int maxNodePoints, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

        var allPoints = new Point[count];

        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                allPoints[index] = point;
                await session.InsertPointAsync(point, index);
            }
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            for (var index = 0L; index < count; index++)
            {
                var point = allPoints[index];
                var results = await session.LookupPointAsync(point);

                var f1 = results.Any(entry => entry.Value == index);
                Assert.That(f1, Is.True);
                var f2 = results.All(entry => entry.Point.Equals(point));
                Assert.That(f2, Is.True);
            }
        }
    }
    
    [TestCase(100000, 1024, 2)]
    [TestCase(100000, 256, 3)]
    public async Task LookupBoundCollection(long count, int maxNodePoints, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

        var allPoints = new Point[count];
        var r = new Random();

        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                allPoints[index] = point;
                await session.InsertPointAsync(point, index);
            }
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            // Try random bounds lookup, repeats 100 times.
            for (var index = 0; index < 100; index++)
            {
                var bound = GetRandomBound(r, dimension);

                var expected = allPoints.
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

    [TestCase(100000, 1024, 2)]
    [TestCase(100000, 256, 3)]
    public async Task EnumerateBoundCollection(long count, int maxNodePoints, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

        var allPoints = new Point[count];
        var r = new Random();

        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                allPoints[index] = point;
                await session.InsertPointAsync(point, index);
            }
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            // Try random bounds lookup, repeats 100 times.
            for (var index = 0; index < 100; index++)
            {
                var bound = GetRandomBound(r, dimension);

                var expected = allPoints.
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

    [TestCase(100000, 1024, true, 2)]
    [TestCase(100000, 1024, false, 2)]
    [TestCase(100000, 256, true, 3)]
    [TestCase(100000, 256, false, 3)]
    public async Task RemovePointsCollection(long count, int maxNodePoints, bool performShrinking, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

        var allPoints = new Dictionary<Point, List<long>>();
        var r = new Random();

        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                if (!allPoints.TryGetValue(point, out var list))
                {
                    list = new();
                    allPoints.Add(point, list);
                }

                list.Add(index);
                await session.InsertPointAsync(point, index);
            }

            foreach (var entry in allPoints)
            {
                var removed = await session.RemovePointsAsync(entry.Key, performShrinking);
                Assert.That(removed, Is.EqualTo(entry.Value.Count));
            }
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            var willEmpty = await session.LookupBoundAsync(session.Entire);
            Assert.That(willEmpty.Length, Is.EqualTo(0));
        }
    }

    [TestCase(100000, 1024, true, 2)]
    [TestCase(100000, 1024, false, 2)]
    [TestCase(100000, 256, true, 3)]
    [TestCase(100000, 256, false, 3)]
    public async Task RemoveBoundCollection(long count, int maxNodePoints, bool performShrinking, int dimension)
    {
        var quadTree = QuadTree.Factory.Create<long>(
            GetEntireBound(dimension), maxNodePoints);

        var allPoints = new Dictionary<Point, List<long>>();
        var r = new Random();

        await using (var session = await quadTree.BeginUpdateSessionAsync())
        {
            for (var index = 0L; index < count; index++)
            {
                var point = GetRandomPoint(r, dimension);
                if (!allPoints.TryGetValue(point, out var list))
                {
                    list = new();
                    allPoints.Add(point, list);
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
        }

        await using (var session = await quadTree.BeginSessionAsync())
        {
            var willEmpty = await session.LookupBoundAsync(session.Entire);
            Assert.That(willEmpty.Length, Is.EqualTo(0));
        }
    }
}
