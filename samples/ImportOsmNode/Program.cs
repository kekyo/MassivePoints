////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

// This sample code uses bulk insert to the coordinate points
// contained in the OpenStreetMap PBF file into a SQLite provided QuadTree.

using System;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MassivePoints;
using MassivePoints.Data;
//using Microsoft.Data.Sqlite;
using OsmSharp;
using OsmSharp.Streams;

namespace ImportOsmNode;

public static class Program
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Download PBF file.
    /// </summary>
    /// <param name="pbfUrl">PBF file url.</param>
    /// <param name="pbfFileName">Store to PBF file.</param>
    /// <param name="ct">CancellationToken</param>
    private static async Task DownloadOsmPbfAsync(
        Uri pbfUrl, string pbfFileName, CancellationToken ct)
    {
        Console.WriteLine($"Downloading OpenStreetMap pbf: {pbfUrl} ...");
            
        await using (var fs = File.Create(pbfFileName + ".tmp"))
        {
            var httpClient = new HttpClient();
            await using var stream = await httpClient.GetStreamAsync(pbfUrl, ct);

            var buffer = new byte[1024 * 1204];

            var length = 1024 * 1024 * 10;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read == 0)
                {
                    break;
                }
                await fs.WriteAsync(buffer, 0, read, ct);

                length -= read;
                if (length <= 0)
                {
                    Console.WriteLine($"Downloading: Size={fs.Length}");
                    length = 1024 * 1024 * 10;
                }
            }

            await fs.FlushAsync(ct);
        }

        File.Delete(pbfFileName);
        File.Move(pbfFileName + ".tmp", pbfFileName);
            
        Console.WriteLine("Downloaded.");
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Create QuadTree instance related by a SQLite database file.
    /// </summary>
    /// <param name="dbFileName">SQLite database file path</param>
    /// <param name="isReadOnly">Readonly if True</param>
    /// <param name="maxNodePoints">Maximum points of each node</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>QuadTree instance</returns>
    private static async ValueTask<QuadTree<long>> CreateQuadTreeAsync(
        string dbFileName, bool isReadOnly, int maxNodePoints, CancellationToken ct)
    {
#if true
        var connectionString = new SQLiteConnectionStringBuilder
        {
            DataSource = dbFileName,
            ReadOnly = isReadOnly,
            JournalMode = SQLiteJournalModeEnum.Memory,
            Pooling = isReadOnly,   // HACK: avoid db file locking.
        }.ToString();

        var quadTreeProvider = QuadTree.Factory.CreateProvider<long>(
            async ct =>
            {
                var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync(ct);
                return connection;
            },
            new DbDataProviderConfiguration(Bound.TheGlobe2D, maxNodePoints));
#else
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbFileName,
            Mode = isReadOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = isReadOnly,   // HACK: avoid db file locking.
        }.ToString();

        var quadTreeProvider = QuadTree.Factory.CreateProvider<long>(
            async ct =>
            {
                var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync(ct);
                
                // Set journal mode to MEMORY.
                await connection.SetSQLiteJournalModeAsync(SQLiteJournalModes.Memory, ct);
                return connection;
            },
            new DbDataProviderConfiguration(Bound.TheGlobe2D, maxNodePoints));
#endif

        await quadTreeProvider.CreateSQLiteTablesAsync(false, ct);

        return QuadTree.Factory.Create(quadTreeProvider);
    }

    /// <summary>
    /// Bulk insert OSM nodes into QuadTree.
    /// </summary>
    /// <param name="pbfFileName">OSM PBF file path</param>
    /// <param name="dbFileName">SQLite database file path</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Inserted nodes</returns>
    private static async ValueTask<long> BulkInsertOsmNodesAsync(
        string pbfFileName, string dbFileName, CancellationToken ct)
    {
        // Delete junks.
        foreach (var path in Directory.EnumerateFiles(
            Path.GetDirectoryName(dbFileName) switch
            {
                null => Path.DirectorySeparatorChar.ToString(),
                "" => ".",
                var p => p,
            },
            Path.GetFileName(dbFileName) + "*"))
        {
            File.Delete(path);
        }

        // Create QuadTree instance.
        var quadTree = await CreateQuadTreeAsync(
            dbFileName + ".tmp", false, 131072, ct);

        // Open an OSM PBF file.
        await using var pbfStream = new FileStream(
            pbfFileName,
            FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, true);
        
        // Construct OsmSharp streaming deserializer.
        var source = new PBFOsmStreamSource(pbfStream);

        var nodeCount = 0L;
        var sw = Stopwatch.StartNew();

        // Begin update session.
        int maximumNodeDepth;
        await using (var session = await quadTree.BeginUpdateSessionAsync(ct))
        {
            // Perform bulk insertion.
            maximumNodeDepth = await session.InsertPointsAsync(
                source.ToAsyncEnumerable().
                OfType<Node>().  // Filter only the node.
                SelectAwait(async node =>
                {
                    if (Interlocked.Increment(ref nodeCount) % 100000 == 0)
                    {
                        // Checkpoint: flush current insertion.
                        await session.FlushAsync();

                        Console.WriteLine($"Bulk inserting: NodeCount={nodeCount}");
                    }

                    // Create a 2D point with value (node id).
                    return PointItem.Create(node.Longitude ?? 0.0, node.Latitude ?? 0.0, node.Id ?? 0L);
                }),
                100000,  // Bulk insert block size
                ct);

            // Finish the session
            await session.FinishAsync();
        }

        sw.Stop();
        
        Console.WriteLine($"Bulk inserted: NodeCount={nodeCount}, MaximumNodeDepth={maximumNodeDepth}, Elapsed={sw.Elapsed}, Performance={sw.Elapsed.TotalMicroseconds / nodeCount:F}usec");

#if true
        var connectionString1 = new SQLiteConnectionStringBuilder
        {
            DataSource = dbFileName + ".tmp",
            ReadOnly = false,
            JournalMode = SQLiteJournalModeEnum.Memory,
            Pooling = false,   // HACK: avoid db file locking.
        }.ToString();

        await using (var connection = new SQLiteConnection(connectionString1))
#else
        var connectionString1 = new SqliteConnectionStringBuilder
        {
            DataSource = dbFileName + ".tmp",
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,   // HACK: avoid db file locking.
        }.ToString();

        await using (var connection = new SqliteConnection(connectionString1))
#endif
        {
            await connection.OpenAsync(ct);

            Console.WriteLine("Creating additional index ...");

            // Create value column index because using verify process.
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE INDEX IF NOT EXISTS quadtree_node_points_value_index ON quadtree_node_points([value])";
                await command.ExecuteNonQueryAsync(ct);
            }

            Console.WriteLine("SQLite vacuum now ...");

            // Perform vacuum.
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "VACUUM";
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        File.Delete(dbFileName);
        while (true)
        {
            try
            {
                File.Move(dbFileName + ".tmp", dbFileName);
                return nodeCount;
            }
            catch
            {
                await Task.Delay(1000);
            }
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Verify raw database records.
    /// </summary>
    /// <param name="pbfFileName">Refer OSM PBF file path</param>
    /// <param name="dbFileName">SQLite database file path</param>
    /// <param name="totalNodeCount">Total OSM node count</param>
    /// <param name="ct">CancellationToken</param>
    private static async ValueTask VerifyInsertedNodes1Async(
        string pbfFileName, string dbFileName, long totalNodeCount, CancellationToken ct)
    {
        // Verify between PBF node and inserted recrods without using QuadTree.

#if true
        var connectionString = new SQLiteConnectionStringBuilder
        {
            DataSource = dbFileName,
            ReadOnly = true,
            JournalMode = SQLiteJournalModeEnum.Memory,
        }.ToString();

        await using (var connection = new SQLiteConnection(connectionString))
#else
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbFileName,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        await using (var connection = new SqliteConnection(connectionString))
#endif
        {
            await connection.OpenAsync(ct);

            await using var command2 = connection.CreateCommand();
            command2.CommandText = "SELECT x,y FROM quadtree_node_points WHERE [value]=@id";
            var parameter0 = command2.CreateParameter();
            parameter0.ParameterName = "@id";
            command2.Parameters.Add(parameter0);

            // Open an OSM PBF file.
            await using var pbfStream = new FileStream(
                pbfFileName,
                FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, true);

            // Construct OsmSharp streaming deserializer.
            var source = new PBFOsmStreamSource(pbfStream);

            var nodeCount = 0L;
            var sw = Stopwatch.StartNew();

            // Proceed all OSM nodes.
            foreach (var osmGeo in source)
            {
                switch (osmGeo)
                {
                    case Node node:
                        parameter0.Value = node.Id;
                        using (var reader = await command2.ExecuteReaderAsync(
                           CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess,
                           ct))
                        {
                            if (await reader.ReadAsync(ct))
                            {
                                var lon = reader.GetDouble(0);
                                var lat = reader.GetDouble(1);
                                if (node.Longitude != lon || node.Latitude != lat)
                                {
                                    throw new InvalidDataException(
                                        $"Differ [1]: Node={node.Id}, [{node.Latitude},{node.Longitude}] != [{lat},{lon}], NodeCount={nodeCount}/{totalNodeCount}");
                                }
                            }
                            else
                            {
                                throw new InvalidDataException(
                                    $"Could not found [1]: Node={node.Id}, NodeCount={nodeCount}/{totalNodeCount}");
                            }
                        }
                        if (Interlocked.Increment(ref nodeCount) % 100000 == 0)
                        {
                            Console.WriteLine($"Verifying [1]: NodeCount={nodeCount}/{totalNodeCount}");
                        }
                        break;
                }
            }

            sw.Stop();

            Console.WriteLine($"Verified [1]: NodeCount={nodeCount}/{totalNodeCount}, Elapsed={sw.Elapsed}, Performance={sw.Elapsed.TotalMicroseconds / nodeCount:F}usec");
        }
    }

    /// <summary>
    /// Verify inserted nodes by QuadTree (too slow)
    /// </summary>
    /// <param name="pbfFileName">Refer OSM PBF file path</param>
    /// <param name="dbFileName">SQLite database file path</param>
    /// <param name="totalNodeCount">Total OSM node count</param>
    /// <param name="ct">CancellationToken</param>
    private static async ValueTask VerifyInsertedNodes2Async(
        string pbfFileName, string dbFileName, long totalNodeCount, CancellationToken ct)
    {
        // Create QuadTree instance.
        var quadTree = await CreateQuadTreeAsync(
            dbFileName, true, 131072, ct);

        await using (var session = await quadTree.BeginSessionAsync(ct))
        {
            // Open an OSM PBF file.
            await using var pbfStream = new FileStream(
                pbfFileName,
                FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, true);

            // Construct OsmSharp streaming deserializer.
            var source = new PBFOsmStreamSource(pbfStream);

            var nodeCount = 0L;
            var sw = Stopwatch.StartNew();

            // Proceed all OSM nodes.
            foreach (var osmGeo in source)
            {
                switch (osmGeo)
                {
                    case Node node:
                        var points = await session.LookupPointAsync(
                            new(node.Longitude!.Value, node.Latitude!.Value), ct);
                        if (!points.Any(p => p.Value == node.Id!))
                        {
                            throw new InvalidDataException(
                                $"Could not found [2]: Node={node.Id}, NodeCount={nodeCount}/{totalNodeCount}");
                        }
                        if (Interlocked.Increment(ref nodeCount) % 100000 == 0)
                        {
                            Console.WriteLine($"Verifying [2]: NodeCount={nodeCount}/{totalNodeCount}");
                        }
                        break;
                }
            }

            sw.Stop();

            Console.WriteLine($"Verified [2]: NodeCount={nodeCount}/{totalNodeCount}, Elapsed={sw.Elapsed}, Performance={sw.Elapsed.TotalMicroseconds / nodeCount:F}usec");
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Main.
    /// </summary>
    public static async Task Main(string[] args)
    {
        string pbfFileName;
        string dbFileName;
        Uri pbfUrl;

        switch (args.Length)
        {
            // Full automatic proressing on OSM overall japan region.
            case 0:
                pbfFileName = "japan-latest.osm.pbf";
                dbFileName = "japan-latest.osm.db";
                pbfUrl = new("https://download.geofabrik.de/asia/japan-latest.osm.pbf");
                break;
            // Easy instruction for relative URL path of geofabrik.de.
            // ex: `asia/japan/shikoku-latest.osm.pbf`
            case 1:
                var basePathName = args[0].Replace('/', Path.DirectorySeparatorChar);
                pbfFileName = Path.GetFileName(basePathName);
                dbFileName = Path.GetFileNameWithoutExtension(basePathName) + ".db";
                pbfUrl = new($"https://download.geofabrik.de/{basePathName}");
                break;
            // Strict instruction for all required local file paths and URL.
            case 3:
                pbfFileName = args[0];
                dbFileName = args[1];
                pbfUrl = new(args[2]);
                break;
            default:
                Console.WriteLine("usage: ImportOsmNode");
                Console.WriteLine("usage: ImportOsmNode <pbf relative path>");
                Console.WriteLine("usage: ImportOsmNode <pbf file path> <db file path> <pbf url>");
                return;
        }

        ///////////////////////////////////////////////////////////////////////////////
        // Step 1: Download OSM PBF file.

        // OSM PBF file already exists.
        if (File.Exists(pbfFileName))
        {
            Console.WriteLine($"PBF file already downloaded: {pbfFileName}");
        }
        else
        {
            // Perform to download.
            await DownloadOsmPbfAsync(pbfUrl, pbfFileName, default);
        }

        ///////////////////////////////////////////////////////////////////////////////
        // Step 2: Bulk inserts OSM PBF nodes to SQLite database.

        // SQLite database file already exists.
        long totalNodeCount;
        if (File.Exists(dbFileName))
        {
            // Counts total nodes.
#if true
            var connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = dbFileName,
                ReadOnly = true,
                JournalMode = SQLiteJournalModeEnum.Memory,
            }.ToString();

            await using var connection = new SQLiteConnection(connectionString);
#else
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbFileName,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
#endif
            await connection.OpenAsync(default);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM quadtree_node_points";
            totalNodeCount = (long)(await command.ExecuteScalarAsync(default))!;

            Console.WriteLine($"Database file already constructed: {dbFileName}, NodeCount={totalNodeCount}");
        }
        else
        {
            // Perform bulk insertion.
            totalNodeCount = await BulkInsertOsmNodesAsync(pbfFileName, dbFileName, default);
        }

        ///////////////////////////////////////////////////////////////////////////////
        // Step 3: Verify SQLite database.

        await VerifyInsertedNodes1Async(pbfFileName, dbFileName, totalNodeCount, default);

        Console.WriteLine("Will be verified with lookups each points, it is slow progress [2].");
        Console.WriteLine("You can terminate it because surface verification is already completed.");

        await VerifyInsertedNodes1Async(pbfFileName, dbFileName, totalNodeCount, default);
    }
}
