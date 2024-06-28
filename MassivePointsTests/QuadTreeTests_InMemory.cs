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
    [TestCase(1, 10)]
    [TestCase(10, 10)]
    [TestCase(11, 10)]
    [TestCase(10000000, 1024)]
    public async Task InsertCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var r = new Random();
            var maxDepth = 0;
            for (var index = 0L; index < count; index++)
            {
                var depth = await session.InsertPointAsync(
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

    private static IEnumerable<long> RangeLong(long start, long count)
    {
        for (var index = 0L; index < count; index++)
        {
            yield return index + start;
        }
    }

    [TestCase(1, 10)]
    [TestCase(10, 10)]
    [TestCase(11, 10)]
    [TestCase(10000000, 1024)]
    public async Task BulkInsertCollection1(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        var allPoints = new Point[count];

        await using (var session = await quadTree.BeginSessionAsync(true))
        {
            try
            {
                var r = new Random();
                for (var index = 0L; index < count; index += 100000)
                {
                    var points = Enumerable.Range(0, (int)Math.Min(100000, count - index)).
                        Select(i =>
                        {
                            var p = new Point(r.Next(0, 99999), r.Next(0, 99999));
                            allPoints[index + i] = p;
                            return new PointItem<long>(p, index + i);
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

        await using (var session = await quadTree.BeginSessionAsync(false))
        {
            try
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
            finally
            {
                await session.FinishAsync();
            }
        }
    }

    [TestCase(1, 10)]
    [TestCase(10, 10)]
    [TestCase(11, 10)]
    [TestCase(100000000, 65536)]
    public async Task BulkInsertCollection2(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var r = new Random();
            await session.InsertPointsAsync(
                RangeLong(0, count).
                Select(index => new PointItem<long>(
                    r.Next(0, 99999), r.Next(0, 99999), index)));
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

    [TestCase(1, 10)]
    [TestCase(10, 10)]
    [TestCase(11, 10)]
    [TestCase(100000000, 65536)]
    public async Task BulkInsertCollection3(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var r = new Random();
            await session.InsertPointsAsync(
                Select(RangeLongAsync(0, count),
                    index => new PointItem<long>(
                    r.Next(0, 99999), r.Next(0, 99999), index)));
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

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var allPoints = new Point[count];

            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                allPoints[index] = point;
                await session.InsertPointAsync(point, index);
            }

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
        finally
        {
            await session.FinishAsync();
        }
    }
    
    [TestCase(1000000, 1024)]
    public async Task LookupBoundCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var allPoints = new Point[count];

            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                allPoints[index] = point;
                await session.InsertPointAsync(point, index);
            }

            // Try random bounds lookup, repeats 100 times.
            for (var index = 0; index < 100; index++)
            {
                var point = new Point(r.Next(0, 49999), r.Next(0, 49999));
                var bound = new Bound(point, r.Next(0, 49999), r.Next(0, 49999));

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
        finally
        {
            await session.FinishAsync();
        }
    }

    [TestCase(1000000, 1024)]
    public async Task EnumerateBoundCollection(long count, int maxNodePoints)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var allPoints = new Point[count];

            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                allPoints[index] = point;
                await session.InsertPointAsync(point, index);
            }

            // Try random bounds lookup, repeats 100 times.
            for (var index = 0; index < 100; index++)
            {
                var point = new Point(r.Next(0, 49999), r.Next(0, 49999));
                var bound = new Bound(point, r.Next(0, 49999), r.Next(0, 49999));

                var expected = allPoints.
                    Select((p, index) => (p, index)).
                    Where(entry => bound.IsWithin(entry.p)).
                    OrderBy(entry => entry.index).
                    Select(entry => (long)entry.index).
                    ToArray();

                var results = new List<PointItem<long>>();
                await foreach (var entry in session.EnumerateBoundAsync(bound, default))
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
        finally
        {
            await session.FinishAsync();
        }
    }

    [TestCase(1000000, 1024, true)]
    [TestCase(1000000, 1024, false)]
    public async Task RemovePointsCollection(long count, int maxNodePoints, bool performShrinking)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var allPoints = new Dictionary<Point, List<long>>();

            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
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

            var willEmpty = await session.LookupBoundAsync(session.Entire);
            Assert.That(willEmpty.Length, Is.EqualTo(0));
        }
        finally
        {
            await session.FinishAsync();
        }
    }

    [TestCase(1000000, 1024, true)]
    [TestCase(1000000, 1024, false)]
    public async Task RemoveBoundCollection(long count, int maxNodePoints, bool performShrinking)
    {
        var quadTree = QuadTree.Factory.Create<long>(100000, 100000, maxNodePoints);

        await using var session = await quadTree.BeginSessionAsync(true);

        try
        {
            var allPoints = new Dictionary<Point, List<long>>();

            var r = new Random();
            for (var index = 0L; index < count; index++)
            {
                var point = new Point(r.Next(0, 99999), r.Next(0, 99999));
                if (!allPoints.TryGetValue(point, out var list))
                {
                    list = new();
                    allPoints.Add(point, list);
                }

                list.Add(index);
                await session.InsertPointAsync(point, index);
            }

            var removed = 0L;
            foreach (var childBound in session.Entire.ChildBounds)
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
