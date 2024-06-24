# MassivePoints

.NET implementation of QuadTree, hold a very large number of 2D coordinates and perform fast range searches, with in-memory and database offloading. 

![MassivePoints](Images/MassivePoints.200.png)

# Status

[![Project Status: WIP – Initial development is in progress, but there has not yet been a stable, usable release suitable for the public.](https://www.repostatus.org/badges/latest/wip.svg)](https://www.repostatus.org/#wip)

|Target|Pakcage|
|:----|:----|
|Any|[![NuGet MassivePoints](https://img.shields.io/nuget/v/MassivePoints.svg?style=flat)](https://www.nuget.org/packages/MassivePoints)|

----

## What is this?

Have you ever tried to store a large amount of 2D coordinate points and extract these from any given coordinate range?
Normally for such requests, we would use a GIS-compatible database system or service with complex management.

This library provides the ability to store and filter ranges of 2D coordinate points in the portable way.

It's very easy to use:

```csharp
using MassivePoints;

// Create QuadTree dictionary with coordinate bound
// and pair of value type on the memory.
double width = 100000.0;
double height = 100000.0;
IQuadTree<string> quadTree = QuadTree.Factory.Create<string>(width, height);

// Add a lot of random coordinates.
var count = 1000000;
var r = new Random();
for (var index = 0; index < count; index++)
{
    double x = r.Next(0, width - 1);
    double y = r.Next(0, height - 1);
    await quadTree.AddAsync((x, y), $"Point{index}");
}

// Extract values by specifying coordinate range.
double x = 30000.0;
double y = 40000.0;
double width = 35000.0;
double height = 23000.0;
foreach (KeyValuePair<Point, string> entry in
    await quadTree.LookupBoundAsync((x, y, width, height)))
{
    Console.WriteLine($"{entry.Key}: {entry.Value}");
}
```

It has the following features:

* Implements QuadTree coordinate search algorithm.
* Included add a coordinate point, lookup and remove features.
* Completely separates between QuadTree controller and data provider.
  * Builtin data providers: In-memory and ADO.NET.
* Fully asynchronous operation.
* Supported asynchronous streaming lookup (`IAsyncEnumerable<T>`).

### Target .NET platforms

* .NET 8.0 to 5.0
* .NET Core 3.1 to 2.0
* .NET Standard 2.1 and 2.0
* .NET Framework 4.8.1 to 4.6.1

----

## How to use

Install [MassivePoints](https://www.nuget.org/packages/MassivePoints) from NuGet.

### Create in-memory QuadTree

```csharp
using MassivePoints;

// Create QuadTree dictionary with coordinate bound
// and pair of value type on the memory.
double width = 100000.0;
double height = 100000.0;
IQuadTree<string> quadTree = QuadTree.Factory.Create<string>(width, height);
```

### Create QuadTree with ADO.NET provider

This sample code uses SQLite ADO.NET provider: [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/)

TODO: The steps for using the ADO.NET data provider are possibly subject to change.


```csharp
using MassivePoints;
using MassivePoints.Data;
using Microsoft.Data.Sqlite;

// Open SQLite database.
var connectionString = new SqliteConnectionStringBuilder()
{
    DataSource = "points.db",
    Mode = SqliteOpenMode.ReadWriteCreate,
}.ToString();

await using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync(default);

// Create QuadTree provider using SQLite database.
double width = 100000.0;
double height = 100000.0;
var provider = QuadTree.Factory.CreateProvider<string>(
    connection, "test", new Bound(width, height));

// Setup the SQLite tables to be used with QuadTree.
await provider.CreateSQLiteTablesAsync(false, default);
await provider.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, default);

// Create QuadTree dictionary.
IQuadTree<string> quadTree = QuadTree.Factory.Create(provider);
```

### Add coordinate points

You can add a coordinate point and a value to associate with it using `AddAsync()`:

```csharp
// Add a lot of random coordinates.
var count = 1000000;
var r = new Random();
for (var index = 0; index < count; index++)
{
    Point point = new Point(r.Next(0, width - 1), r.Next(0, height - 1));
    await quadTree.AddAsync(point, $"Point{index}");
}
```

### Lookup coordinate points

With exact coordinate point by `LookupPointAsync()`:

```csharp
// Extract values by specifying a coordinate point.
// There is a possibility that multiple values
// with the same coordinates will be extracted.

Point targetPoint = new Point(31234.0, 45678.0);

foreach (KeyValuePair<Point, string> entry in
    await quadTree.LookupPointAsync(targetPoint))
{
    Console.WriteLine($"{entry.Key}: {entry.Value}");
}
```

With coordinate range by `LookupBoundAsync()`:

```csharp
// Extract values by specifying coordinate range.
Bound targetBound = new Bound(30000.0, 40000.0, 35000.0, 23000.0);

foreach (KeyValuePair<Point, string> entry in
    await quadTree.LookupBoundAsync(targetBound))
{
    Console.WriteLine($"{entry.Key}: {entry.Value}");
}
```

### Streaming lookup

MassivePoints supported `IAsyncEnumerable<T>` asynchronous streaming.
Use `EnumerateBoundAsync()`:

```csharp
// Extract values on asynchronous iterator.
Bound targetBound = new Bound(30000.0, 40000.0, 35000.0, 23000.0);

await foreach (KeyValuePair<Point, string> entry in
    quadTree.EnumerateBoundAsync(targetBound))
{
    Console.WriteLine($"{entry.Key}: {entry.Value}");
}
```

Because of the streaming process, `EnumerateBoundAsync()` can enumerate even a huge set of coordinate points in the result without any problem.
However, be aware that its performance is not as good as that of `LookupBoundAsync()`.

### Remove coordinate points

With exact coordinate point by `RemovePointsAsync()`:

```csharp
// Remove exact coordinate point.
Point targetPoint = new Point(31234.0, 45678.0);

int removed = await quadTree.RemovePointsAsync(targetPoint);
```

With coordinate range by `RemoveBoundAsync()`:

```csharp
// Remove coordinate range.
Bound targetBound = new Bound(30000.0, 40000.0, 35000.0, 23000.0);

long removed = await quadTree.RemoveBoundAsync(targetBound);
```

### Scoped session

TODO:

You should use `BeginSessionAsync()` to protect QuadTree indexes
when used in multi-threaded and/or multiple asynchronous processing:

```csharp
// Begin a update session.
await using (var session = await quadTree.BeginSessionAsync(true))
{
    // (Manipulation for QuadTree)

    // Finish a session.
    await session.FinishAsync();
}
```

The argument `willUpdate` indicates whether coordinate points will be added or removed during this session.
If `true`, an exclusive lock is applied, so it is advisable to keep the number of operations to a minimum when running concurrently.

Also, be sure to call `FinishAsync()` after any updates.
Depending on the backend data provider, the updates may be undone.


----

## TODO

* When coordinates are removed, the index reduction process is not performed.
* Will fail when multiple read/write coordinates with overlapped asynchronous operations.
* Additional xml comment and documents.
* Supports F# friendly interfaces.
* Supports 3D or Multi-dimensionals.
* Improved concurrency.
* Added more useful helper methods.

## License

Apache-v2

## History

* 0.8.0:
  * Initial release.
