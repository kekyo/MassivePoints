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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MassivePoints;
using MassivePoints.Data;
using Microsoft.Data.Sqlite;
using OsmSharp;
using OsmSharp.Streams;

namespace ImportOsmNode;

public static class Program
{
    /// <summary>
    /// Download PBF file.
    /// </summary>
    /// <param name="pbfUrl">PBF file url.</param>
    /// <param name="pbfFileName">Store to PBF file.</param>
    /// <param name="ct">CancellationToken</param>
    private static async Task DownloadOsmPbfAsync(
        Uri pbfUrl, string pbfFileName, CancellationToken ct)
    {
        if (!File.Exists(pbfFileName))
        {
            Console.Write($"Downloading OpenStreetMap pbf: {pbfUrl} ...");
            
            await using (var fs = File.Create(pbfFileName + ".tmp"))
            {
                var httpClient = new HttpClient();
                await using var stream = await httpClient.GetStreamAsync(pbfUrl, ct);

                await stream.CopyToAsync(fs, ct);
                await fs.FlushAsync(ct);
            }
            
            File.Move(pbfFileName + ".tmp", pbfFileName);
            
            Console.WriteLine(" Done.");
        }
    }

    /// <summary>
    /// Create QuadTree instance related by a SQLite database file.
    /// </summary>
    /// <param name="dbFileName">SQLite database file path</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>QuadTree instance</returns>
    private static async ValueTask<IQuadTree<long>> CreateQuadTreeAsync(
        string dbFileName, CancellationToken ct)
    {
        File.Delete(dbFileName);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbFileName,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        
        var quadTreeProvider = QuadTree.Factory.CreateProvider<long>(
            () => new SqliteConnection(connectionString),
            new DbDataProviderConfiguration(Bound.TheGlobe2D, 65536));

        await quadTreeProvider.CreateSQLiteTablesAsync(false, ct);
        await quadTreeProvider.SetSQLiteJournalModeAsync(
            //SQLiteJournalModes.Wal,
            SQLiteJournalModes.Memory,   // We can use memory journal when use stable environment.
            ct);

        return QuadTree.Factory.Create(quadTreeProvider);
    }

    /// <summary>
    /// Bulk insert OSM nodes into QuadTree.
    /// </summary>
    /// <param name="pbfFileName">OSM PBF file path</param>
    /// <param name="dbFileName">SQLite database file path</param>
    /// <param name="ct">CancellationToken</param>
    private static async ValueTask BulkInsertOsmNodesAsync(
        string pbfFileName, string dbFileName, CancellationToken ct)
    {
        var quadTree = await CreateQuadTreeAsync(dbFileName, ct);

        // Open an OSM PBF file.
        await using var pbfStream = new FileStream(
            pbfFileName,
            FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, true);
        
        // Construct OsmSharp streaming deserializer.
        var source = new PBFOsmStreamSource(pbfStream);

        // Begin update session.
        await using var session = await quadTree.BeginUpdateSessionAsync(ct);

        var nodeCount = 0L;
        var sw = Stopwatch.StartNew();
        
        // Perform bulk insert.
        await session.InsertPointsAsync(
            source.ToAsyncEnumerable().
            OfType<Node>().  // Filter only the node.
            SelectAwait(async node =>
            {
                if (Interlocked.Increment(ref nodeCount) % 100000 == 0)
                {
                    // Checkpoint: flush current insertion.
                    await session.FlushAsync();
                    
                    Console.WriteLine($"Progress: NodeCount={nodeCount}");
                }
                
                // Create a 2D point with value (node id).
                return PointItem.Create(node.Longitude ?? 0.0, node.Latitude ?? 0.0, node.Id ?? 0L);
            }),
            100000,  // Bulk insert block size
            ct);

        // Finish the session
        await session.FinishAsync();

        sw.Stop();
        
        Console.WriteLine($"Finished: NodeCount={nodeCount}, Elapsed={sw.Elapsed}, Performance={(double)sw.Elapsed.Microseconds / nodeCount}usec");
    }
    
    /// <summary>
    /// Main.
    /// </summary>
    public static async Task Main()
    {
        var pbfUrl = new Uri("https://download.geofabrik.de/asia/japan-latest.osm.pbf");
        var pbfFileName = "osm.pbf";
        var dbFileName = "osm.db";
        
        await DownloadOsmPbfAsync(pbfUrl, pbfFileName, default);
        await BulkInsertOsmNodesAsync(pbfFileName, dbFileName, default);
    }
}
