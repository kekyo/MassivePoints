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
using System.ComponentModel;
using System.Data.Common;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MassivePoints.Data;

/// <summary>
/// Non volatile QuadTree ADO.NET data provider.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public sealed class DbDataProvider<TValue> : IDataProvider<TValue, long>
{
    private readonly Func<DbConnection> connectionFactory;
    private readonly Bound entire;
    private readonly int maxNodePoints;

    private readonly DbQueryDefinition selectNodeQuery;
    private readonly DbQueryDefinition selectPointCountQuery;
    private readonly DbQueryDefinition insertPointQuery;
    private readonly DbQueryDefinition selectNodeMaxIdQuery;
    private readonly DbQueryDefinition updateNodeQuery;
    private readonly DbQueryDefinition insertNodeQuery;
    private readonly DbQueryDefinition deleteNodeQuery;
    private readonly DbQueryDefinition updatePointsQuery;
    private readonly DbQueryDefinition selectPointQuery;
    private readonly DbQueryDefinition selectPointsQuery;
    private readonly DbQueryDefinition deletePointQuery;
    private readonly DbQueryDefinition deleteBoundQuery;

    public DbDataProvider(
        Func<DbConnection> connectionFactory,
        string symbol_prefix,
        Bound entire,
        int maxNodePoints = 1024)
    {
        this.connectionFactory = connectionFactory;
        this.entire = entire;
        this.maxNodePoints = maxNodePoints;
        this.Prefix = symbol_prefix;

        this.selectNodeQuery = new(
            $"SELECT top_left_id,top_right_id,bottom_left_id,bottom_right_id FROM {symbol_prefix}_nodes WHERE id=@id",
            "@id");
        this.selectPointCountQuery = new(
            $"SELECT COUNT(*) FROM {symbol_prefix}_node_points WHERE node_id=@node_id",
            "@node_id");
        this.insertPointQuery = new(
            $"INSERT INTO {symbol_prefix}_node_points (node_id,x,y,[value]) VALUES (@node_id,@x,@y,@value)",
            "@node_id", "@x", "@y", "@value");
        this.selectNodeMaxIdQuery = new(
            $"SELECT MAX(id) FROM {symbol_prefix}_nodes");
        this.updateNodeQuery = new(
            $"UPDATE {symbol_prefix}_nodes SET top_left_id=@top_left_id,top_right_id=@top_right_id,bottom_left_id=@bottom_left_id,bottom_right_id=@bottom_right_id WHERE id=@id",
            "@id", "@top_left_id", "@top_right_id", "@bottom_left_id", "@bottom_right_id");
        this.insertNodeQuery = new(
            $"INSERT INTO {symbol_prefix}_nodes (id,top_left_id,top_right_id,bottom_left_id,bottom_right_id) VALUES (@id,@top_left_id,@top_right_id,@bottom_left_id,@bottom_right_id)",
            "@id", "@top_left_id", "@top_right_id", "@bottom_left_id", "@bottom_right_id");
        this.deleteNodeQuery = new(
            $"DELETE FROM {symbol_prefix}_nodes WHERE id=@id",
            "@id");
        this.updatePointsQuery = new(
            $"UPDATE {symbol_prefix}_node_points SET node_id=@to_node_id WHERE node_id=@node_id AND @x0<=x AND @y0<=y AND x<@x1 AND y<@y1",
            "@node_id", "@x0", "@y0", "@x1", "@y1", "@to_node_id");
        this.selectPointQuery = new(
            $"SELECT x,y,[value] FROM {symbol_prefix}_node_points WHERE node_id=@node_id AND x=@x AND y=@y",
            "@node_id", "@x", "@y");
        this.selectPointsQuery = new(
            $"SELECT x,y,[value] FROM {symbol_prefix}_node_points WHERE node_id=@node_id AND @x0<=x AND @y0<=y AND x<@x1 AND y<@y1",
            "@node_id", "@x0", "@y0", "@x1", "@y1");
        this.deletePointQuery = new(
            $"DELETE FROM {symbol_prefix}_node_points WHERE node_id=@node_id AND x=@x AND y=@y",
            "@node_id", "@x", "@y");
        this.deleteBoundQuery = new(
            $"DELETE FROM {symbol_prefix}_node_points WHERE node_id=@node_id AND @x0<=x AND @y0<=y AND x<@x1 AND y<@y1",
            "@node_id", "@x0", "@y0", "@x1", "@y1");
    }
    
    public string Prefix { get; }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public async ValueTask<DbConnection> OpenTemporaryConnectionAsync(
        CancellationToken ct)
    {
        var connection = this.connectionFactory();
        try
        {
            await connection.OpenAsync(ct);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
        return connection;
    }

    /// <summary>
    /// Begin a session.
    /// </summary>
    /// <param name="willUpdate">True if possibility changes will be made during the session</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>The session</returns>
    public ValueTask<IDataProviderSession<TValue, long>> BeginSessionAsync(
        bool willUpdate, CancellationToken ct)
    {
        var connectionCache = new DbConnectionCache(
            willUpdate, this.connectionFactory);
        return new(new DataProviderSession(this, connectionCache));
    }

    /// <summary>
    /// Volatile in-memory QuadTree data provider session.
    /// </summary>
    private sealed class DataProviderSession :
        IDataProviderSession<TValue, long>
    {
        private readonly DbDataProvider<TValue> parent;
        private readonly DbConnectionCache connectionCache;

        public DataProviderSession(
            DbDataProvider<TValue> parent,
            DbConnectionCache connectionCache)
        {
            this.parent = parent;
            this.connectionCache = connectionCache;
        }

        public ValueTask DisposeAsync() =>
            this.connectionCache.DisposeAsync();

        public ValueTask FinishAsync() =>
            this.connectionCache.CommitAsync();

        /// <summary>
        /// This indicates the overall range of the coordinate points managed by data provider.
        /// </summary>
        public Bound Entire =>
            this.parent.entire;

        /// <summary>
        /// Maximum number of coordinate points in each node.
        /// </summary>
        public int MaxNodePoints =>
            this.parent.maxNodePoints;

        /// <summary>
        /// Root node ID.
        /// </summary>
        public long RootId => 0;

        /// <summary>
        /// Get information about the node.
        /// </summary>
        /// <param name="nodeId">Node ID</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Node information if available</returns>
        public async ValueTask<QuadTreeNode<long>?> GetNodeAsync(
            long nodeId, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.selectNodeQuery, ct);
            return await command.ExecuteReadOneRecordAsync(
                record => record.IsDBNull(0) ?
                    null :
                    new QuadTreeNode<long>(
                        record.GetInt64(0),
                        record.GetInt64(1),
                        record.GetInt64(2),
                        record.GetInt64(3)),
                ct,
                nodeId);
        }

        public async ValueTask<int> GetPointCountAsync(
            long nodeId, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.selectPointCountQuery, ct);
            return await command.ExecuteReadOneRecordAsync(
                record => record.GetInt32(0),
                ct,
                nodeId);
        }

        public async ValueTask AddPointAsync(
            long nodeId, Point point, TValue value, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.insertPointQuery, ct);
            if (await command.ExecuteNonQueryAsync(
                ct,
                nodeId,
                point.X,
                point.Y,
                (object?)value ?? DBNull.Value) != 1)
            {
                throw new InvalidDataException(
                    $"AddPoint: NodeId={nodeId}, Point={point}");
            }
        }

        private async ValueTask MovePointsAsync(
            long nodeId, Bound bound, long toNodeId, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.updatePointsQuery, ct);
            if (await command.ExecuteNonQueryAsync(
                ct,
                nodeId,
                bound.X,
                bound.Y,
                bound.X + bound.Width,
                bound.Y + bound.Height,
                toNodeId) < 0)
            {
                throw new InvalidDataException(
                    $"MovePoints: NodeId={nodeId}, TargetBound={bound}, ToNodeId={toNodeId}");
            }
        }

        public async ValueTask<QuadTreeNode<long>> DistributePointsAsync(
            long nodeId, Bound[] toBounds, CancellationToken ct)
        {
            using var selectCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.selectNodeMaxIdQuery, ct);
            if (await selectCommand.ExecuteReadOneRecordAsync(
                recored =>
                {
                    var baseNodeId = recored.GetInt64(0);
                    return new QuadTreeNode<long>(baseNodeId + 1, baseNodeId + 2, baseNodeId + 3, baseNodeId + 4);
                },
                ct) is not { } node)
            {
                throw new InvalidDataException(
                    $"DistributePoints [1]: NodeId={nodeId}");
            }
            
            using var updateCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.updateNodeQuery, ct);
            if (await updateCommand.ExecuteNonQueryAsync(
                ct,
                nodeId,
                node.TopLeftId,
                node.TopRightId,
                node.BottomLeftId,
                node.BottomRightId) < 0)
            {
                throw new InvalidDataException(
                    $"DistributePoints [2]: NodeId={nodeId}");
            }

            using var insertCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.insertNodeQuery, ct);
            var toNodeIds = node.ChildIds;
            for (var index = 0; index < toBounds.Length; index++)
            {
                if (await insertCommand.ExecuteNonQueryAsync(
                    ct,
                    toNodeIds[index],
                    DBNull.Value,
                    DBNull.Value,
                    DBNull.Value,
                    DBNull.Value) < 0)
                {
                    throw new InvalidDataException(
                        $"DistributePoints [3]: NodeId={toNodeIds[index]}");
                }
                await this.MovePointsAsync(nodeId, toBounds[index], toNodeIds[index], ct);
            }

            return node;
        }

        public async ValueTask AggregatePointsAsync(
            long[] nodeIds, Bound toBound, long toNodeId, CancellationToken ct)
        {
            foreach (var nodeId in nodeIds)
            {
                await this.MovePointsAsync(nodeId, toBound, toNodeId, ct);

                using var deleteCommand = await this.connectionCache.GetPreparedCommandAsync(
                    this.parent.deleteNodeQuery, ct);
                if (await deleteCommand.ExecuteNonQueryAsync(
                    ct,
                    nodeId) < 0)
                {
                    throw new InvalidDataException(
                        $"AggregatePoints [1]: NodeId={nodeId}");
                }
            }

            using var updateCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.updateNodeQuery, ct);
            if (await updateCommand.ExecuteNonQueryAsync(
                ct,
                toNodeId,
                DBNull.Value,
                DBNull.Value,
                DBNull.Value,
                DBNull.Value) < 0)
            {
                throw new InvalidDataException(
                    $"AggregatePoints [2]: NodeId={toNodeId}");
            }
        }

        public async ValueTask<KeyValuePair<Point, TValue>[]> LookupPointAsync(
            long nodeId, Point targetPoint, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.selectPointQuery, ct);
            var results = new List<KeyValuePair<Point, TValue>>();
            await command.ExecuteReadRecordsAsync(
                record => results.Add(new(new(record.GetDouble(0), record.GetDouble(1)), (TValue)record.GetValue(2))),
                ct,
                nodeId,
                targetPoint.X,
                targetPoint.Y);
            return results.ToArray();
        }

        public async ValueTask<KeyValuePair<Point, TValue>[]> LookupBoundAsync(
            long nodeId, Bound targetBound, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.selectPointsQuery, ct);
            var results = new List<KeyValuePair<Point, TValue>>();
            await command.ExecuteReadRecordsAsync(
                record => results.Add(
                    new(new(record.GetDouble(0), record.GetDouble(1)), (TValue)record.GetValue(2))),
                ct,
                nodeId,
                targetBound.X,
                targetBound.Y,
                targetBound.X + targetBound.Width,
                targetBound.Y + targetBound.Height);
            return results.ToArray();
        }

        public async IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(
            long nodeId, Bound targetBound, [EnumeratorCancellation] CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.selectPointsQuery, ct);
            await foreach (var result in command.ExecuteEnumerateAsync(
               record => new KeyValuePair<Point, TValue>(
                   new(record.GetDouble(0), record.GetDouble(1)), (TValue)record.GetValue(2)),
               ct,
               nodeId,
               targetBound.X,
               targetBound.Y,
               targetBound.X + targetBound.Width,
               targetBound.Y + targetBound.Height))
            {
                yield return result;
            }
        }

        public async ValueTask<RemoveResults> RemovePointsAsync(
            long nodeId, Point point, bool includeRemains, CancellationToken ct)
        {
            using var deleteCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.deletePointQuery, ct);
            var removed = await deleteCommand.ExecuteNonQueryAsync(
                ct,
                nodeId,
                point.X,
                point.Y);
            if (removed < 0)
            {
                throw new InvalidDataException(
                    $"RemovePoints: NodeId={nodeId}, TargetPoint={point}");
            }

            if (includeRemains)
            {
                using var selectCommand = await this.connectionCache.GetPreparedCommandAsync(
                    this.parent.selectPointCountQuery, ct);
                if (await selectCommand.ExecuteReadOneRecordAsync(
                    record => (int?)record.GetInt32(0),
                    ct,
                    nodeId) is not { } remains)
                {
                    throw new InvalidDataException($"RemovePoints: NodeId={nodeId}");
                }
                return new(removed, remains);
            }
            else
            {
                return new(removed, -1);
            }
        }

        public async ValueTask<RemoveResults> RemoveBoundAsync(
            long nodeId, Bound bound, bool includeRemains, CancellationToken ct)
        {
            using var deleteCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.deleteBoundQuery, ct);
            var removed = await deleteCommand.ExecuteNonQueryAsync(
                ct,
                nodeId,
                bound.X,
                bound.Y,
                bound.X + bound.Width,
                bound.Y + bound.Height);
            if (removed < 0)
            {
                throw new InvalidDataException(
                    $"RemoveBound: NodeId={nodeId}, TargetBound={bound}");
            }

            if (includeRemains)
            {
                using var selectCommand = await this.connectionCache.GetPreparedCommandAsync(
                    this.parent.selectPointCountQuery, ct);
                if (await selectCommand.ExecuteReadOneRecordAsync(
                    record => (int?)record.GetInt32(0),
                    ct,
                    nodeId) is not { } remains)
                {
                    throw new InvalidDataException($"RemovePoints: NodeId={nodeId}");
                }
                return new(removed, remains);
            }
            else
            {
                return new(removed, -1);
            }
        }
    }
}
