////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MassivePoints.Data;

/// <summary>
/// Non volatile QuadTree ADO.NET data provider.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public sealed class DbQuadTreeProvider<TValue> : IDataProvider<TValue, long>
{
    private readonly DbConnection connection;
    
    private readonly DbCommand selectNodeCommand;
    private readonly DbCommand selectPointCountCommand;
    private readonly DbCommand insertPointCommand;
    private readonly DbCommand selectNodeMaxIdCommand;
    private readonly DbCommand updateNodeCommand;
    private readonly DbCommand insertNodeCommand;
    private readonly DbCommand deleteNodeCommand;
    private readonly DbCommand updatePointsCommand;
    private readonly DbCommand selectPointCommand;
    private readonly DbCommand selectPointsCommand;
    private readonly DbCommand deletePointCommand;
    private readonly DbCommand deleteBoundCommand;

    public DbQuadTreeProvider(
        DbConnection connection,
        string prefix,
        Bound entire,
        int maxNodePoints = 1024)
    {
        this.connection = connection;
        this.Entire = entire;
        this.MaxNodePoints = maxNodePoints;
        this.Prefix = prefix;

        this.selectNodeCommand = this.CreateCommand(
            $"SELECT top_left_id,top_right_id,bottom_left_id,bottom_right_id FROM {prefix}_nodes WHERE id=@id",
            "@id");
        this.selectPointCountCommand = this.CreateCommand(
            $"SELECT COUNT(*) FROM {prefix}_node_points WHERE node_id=@node_id",
            "@node_id");
        this.insertPointCommand = this.CreateCommand(
            $"INSERT INTO {prefix}_node_points (node_id,x,y,[value]) VALUES (@node_id,@x,@y,@value)",
            "@node_id", "@x", "@y", "@value");
        this.selectNodeMaxIdCommand = this.CreateCommand(
            $"SELECT MAX(id) FROM {prefix}_nodes");
        this.updateNodeCommand = this.CreateCommand(
            $"UPDATE {prefix}_nodes SET top_left_id=@top_left_id,top_right_id=@top_right_id,bottom_left_id=@bottom_left_id,bottom_right_id=@bottom_right_id WHERE id=@id",
            "@id", "@top_left_id", "@top_right_id", "@bottom_left_id", "@bottom_right_id");
        this.insertNodeCommand = this.CreateCommand(
            $"INSERT INTO {prefix}_nodes (id,top_left_id,top_right_id,bottom_left_id,bottom_right_id) VALUES (@id,@top_left_id,@top_right_id,@bottom_left_id,@bottom_right_id)",
            "@id", "@top_left_id", "@top_right_id", "@bottom_left_id", "@bottom_right_id");
        this.deleteNodeCommand = this.CreateCommand(
            $"DELETE FROM {prefix}_nodes WHERE id=@id",
            "@id");
        this.updatePointsCommand = this.CreateCommand(
            $"UPDATE {prefix}_node_points SET node_id=@to_node_id WHERE node_id=@node_id AND @x0<=x AND @y0<=y AND x<@x1 AND y<@y1",
            "@node_id", "@x0", "@y0", "@x1", "@y1", "@to_node_id");
        this.selectPointCommand = this.CreateCommand(
            $"SELECT x,y,[value] FROM {prefix}_node_points WHERE node_id=@node_id AND x=@x AND y=@y",
            "@node_id", "@x", "@y");
        this.selectPointsCommand = this.CreateCommand(
            $"SELECT x,y,[value] FROM {prefix}_node_points WHERE node_id=@node_id AND @x0<=x AND @y0<=y AND x<@x1 AND y<@y1",
            "@node_id", "@x0", "@y0", "@x1", "@y1");
        this.deletePointCommand = this.CreateCommand(
            $"DELETE FROM {prefix}_node_points WHERE node_id=@node_id AND x=@x AND y=@y",
            "@node_id", "@x", "@y");
        this.deleteBoundCommand = this.CreateCommand(
            $"DELETE FROM {prefix}_node_points WHERE node_id=@node_id AND @x0<=x AND @y0<=y AND x<@x1 AND y<@y1",
            "@node_id", "@x0", "@y0", "@x1", "@y1");
    }

    public ValueTask DisposeAsync() =>
        new(Task.WhenAll(
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
            this.selectNodeCommand.DisposeAsync().AsTask(),
            this.selectPointCountCommand.DisposeAsync().AsTask(),
            this.insertPointCommand.DisposeAsync().AsTask(),
            this.selectNodeMaxIdCommand.DisposeAsync().AsTask(),
            this.updateNodeCommand.DisposeAsync().AsTask(),
            this.insertNodeCommand.DisposeAsync().AsTask(),
            this.deleteNodeCommand.DisposeAsync().AsTask(),
            this.updatePointsCommand.DisposeAsync().AsTask(),
            this.selectPointCommand.DisposeAsync().AsTask(),
            this.selectPointsCommand.DisposeAsync().AsTask(),
            this.deletePointCommand.DisposeAsync().AsTask(),
            this.deleteBoundCommand.DisposeAsync().AsTask()
#else
            Task.Run(this.selectNodeCommand.Dispose),
            Task.Run(this.selectPointCountCommand.Dispose),
            Task.Run(this.insertPointCommand.Dispose),
            Task.Run(this.selectNodeMaxIdCommand.Dispose),
            Task.Run(this.updateNodeCommand.Dispose),
            Task.Run(this.insertNodeCommand.Dispose),
            Task.Run(this.deleteNodeCommand.Dispose),
            Task.Run(this.updatePointsCommand.Dispose),
            Task.Run(this.selectPointCommand.Dispose),
            Task.Run(this.selectPointsCommand.Dispose),
            Task.Run(this.deletePointCommand.Dispose),
            Task.Run(this.deleteBoundCommand.Dispose)
#endif
        ));

    /// <summary>
    /// This indicates the overall range of the coordinate points managed by data provider.
    /// </summary>
    public Bound Entire { get; }

    /// <summary>
    /// Maximum number of coordinate points in each node.
    /// </summary>
    public int MaxNodePoints { get; }

    /// <summary>
    /// Root node ID.
    /// </summary>
    public long RootId => 0;
    
    /// <summary>
    /// Database metadata symbol prefix.
    /// </summary>
    public string Prefix { get; }

    internal DbCommand CreateCommand(
        string commandText, params string[] parameterNames)
    {
        var command = this.connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = commandText;

        foreach (var parameterName in parameterNames)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            command.Parameters.Add(parameter);
        }

        return command;
    }
    
    public async ValueTask<ISession> BeginSessionAsync(
        bool willUpdate, CancellationToken ct)
    {
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        var transaction = await this.connection.BeginTransactionAsync(
            willUpdate ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);
#else
        var transaction = await Task.Run(() => this.connection.BeginTransaction(
            willUpdate ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted), ct);
#endif
        
        this.selectNodeCommand.Transaction = transaction;
        this.selectPointCountCommand.Transaction = transaction;
        this.insertPointCommand.Transaction = transaction;
        this.selectNodeMaxIdCommand.Transaction = transaction;
        this.updateNodeCommand.Transaction = transaction;
        this.insertNodeCommand.Transaction = transaction;
        this.deleteNodeCommand.Transaction = transaction;
        this.updatePointsCommand.Transaction = transaction;
        this.selectPointCommand.Transaction = transaction;
        this.selectPointsCommand.Transaction = transaction;
        this.deletePointCommand.Transaction = transaction;
        this.deleteBoundCommand.Transaction = transaction;

        return new DbQuadTreeSession(transaction);
    }

    private static async ValueTask<T> ExecuteRead1Async<T>(
        DbCommand command,
        Func<DbDataReader, T> selector,
        Func<T> notFound,
        CancellationToken ct,
        params object[] parameters)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            command.Parameters[index].Value = parameters[index];
        }
        
        var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess, ct);
        try
        {
            while (await reader.ReadAsync(ct))
            {
                return selector(reader);
            }
        }
        finally
        {
            if (reader is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }
        return notFound();
    }

    /// <summary>
    /// Get information about the node.
    /// </summary>
    /// <param name="nodeId">Node ID</param>
    /// <param name="ct">`CancellationToken`</param>
    /// <returns>Node information if available</returns>
    public ValueTask<QuadTreeNode<long>?> GetNodeAsync(
        long nodeId, CancellationToken ct) =>
        ExecuteRead1Async(
            this.selectNodeCommand,
            reader => reader.IsDBNull(0) ? null : new QuadTreeNode<long>(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3)),
            () => throw new InvalidDataException($"GetNode: NodeId={nodeId}"),
            ct,
            nodeId)!;

    public ValueTask<int> GetPointCountAsync(
        long nodeId, CancellationToken ct) =>
        ExecuteRead1Async(
            this.selectPointCountCommand,
            reader => reader.GetInt32(0),
            () => throw new InvalidDataException($"GetPointCount: NodeId={nodeId}"),
            ct,
            nodeId);

    public async ValueTask AddPointAsync(
        long nodeId, Point point, TValue value, CancellationToken ct)
    {
        this.insertPointCommand.Parameters[0].Value = nodeId;
        this.insertPointCommand.Parameters[1].Value = point.X;
        this.insertPointCommand.Parameters[2].Value = point.Y;
        this.insertPointCommand.Parameters[3].Value = (object?)value ?? DBNull.Value;

        if (await this.insertPointCommand.ExecuteNonQueryAsync(ct) != 1)
        {
            throw new InvalidDataException(
                $"AddPoint: NodeId={nodeId}, Point={point}");
        }
    }

    private async ValueTask MovePointsAsync(
        long nodeId, Bound bound, long toNodeId, CancellationToken ct)
    {
        this.updatePointsCommand.Parameters[0].Value = nodeId;
        this.updatePointsCommand.Parameters[1].Value = bound.X;
        this.updatePointsCommand.Parameters[2].Value = bound.Y;
        this.updatePointsCommand.Parameters[3].Value = bound.X + bound.Width;
        this.updatePointsCommand.Parameters[4].Value = bound.Y + bound.Height;
        this.updatePointsCommand.Parameters[5].Value = toNodeId;
        
        if (await this.updatePointsCommand.ExecuteNonQueryAsync(ct) < 0)
        {
            throw new InvalidDataException(
                $"MovePoints: NodeId={nodeId}, TargetBound={bound}, ToNodeId={toNodeId}");
        }
    }

    public async ValueTask<QuadTreeNode<long>> DistributePointsAsync(
        long nodeId, Bound[] toBounds, CancellationToken ct)
    {
        var node = await ExecuteRead1Async(
            this.selectNodeMaxIdCommand,
            reader =>
            {
                var baseNodeId = reader.GetInt64(0);
                return new QuadTreeNode<long>(baseNodeId + 1, baseNodeId + 2, baseNodeId + 3, baseNodeId + 4);
            },
            () => throw new InvalidDataException(
                $"DistributePoints [1]: NodeId={nodeId}"),
            ct)!;

        this.updateNodeCommand.Parameters[0].Value = nodeId;
        this.updateNodeCommand.Parameters[1].Value = node.TopLeftId;
        this.updateNodeCommand.Parameters[2].Value = node.TopRightId;
        this.updateNodeCommand.Parameters[3].Value = node.BottomLeftId;
        this.updateNodeCommand.Parameters[4].Value = node.BottomRightId;

        if (await this.updateNodeCommand.ExecuteNonQueryAsync(ct) < 0)
        {
            throw new InvalidDataException(
                $"DistributePoints [2]: NodeId={nodeId}");
        }

        this.insertNodeCommand.Parameters[1].Value = DBNull.Value;
        this.insertNodeCommand.Parameters[2].Value = DBNull.Value;
        this.insertNodeCommand.Parameters[3].Value = DBNull.Value;
        this.insertNodeCommand.Parameters[4].Value = DBNull.Value;

        var toNodeIds = node.ChildIds;
        for (var index = 0; index < toBounds.Length; index++)
        {
            this.insertNodeCommand.Parameters[0].Value = toNodeIds[index];
            if (await this.insertNodeCommand.ExecuteNonQueryAsync(ct) < 0)
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

            this.deleteNodeCommand.Parameters[0].Value = nodeId;

            if (await this.deleteNodeCommand.ExecuteNonQueryAsync(ct) < 0)
            {
                throw new InvalidDataException(
                    $"AggregatePoints [1]: NodeId={nodeId}");
            }
        }

        this.updateNodeCommand.Parameters[0].Value = toNodeId;
        this.updateNodeCommand.Parameters[1].Value = DBNull.Value;
        this.updateNodeCommand.Parameters[2].Value = DBNull.Value;
        this.updateNodeCommand.Parameters[3].Value = DBNull.Value;
        this.updateNodeCommand.Parameters[4].Value = DBNull.Value;

        if (await this.updateNodeCommand.ExecuteNonQueryAsync(ct) < 0)
        {
            throw new InvalidDataException(
                $"AggregatePoints [2]: NodeId={toNodeId}");
        }
    }

    public async ValueTask LookupPointAsync(
        long nodeId, Point targetPoint, List<KeyValuePair<Point, TValue>> results, CancellationToken ct)
    {
        this.selectPointCommand.Parameters[0].Value = nodeId;
        this.selectPointCommand.Parameters[1].Value = targetPoint.X;
        this.selectPointCommand.Parameters[2].Value = targetPoint.Y;

        var reader = await this.selectPointCommand.ExecuteReaderAsync(
            CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, ct);
        try
        {
            while (await reader.ReadAsync(ct))
            {
                results.Add(new(new(reader.GetDouble(0), reader.GetDouble(1)), (TValue)reader.GetValue(2)));
            }
        }
        finally
        {
            if (reader is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }
    }

    public async ValueTask LookupBoundAsync(
        long nodeId, Bound targetBound, List<KeyValuePair<Point, TValue>> results, CancellationToken ct)
    {
        this.selectPointsCommand.Parameters[0].Value = nodeId;
        this.selectPointsCommand.Parameters[1].Value = targetBound.X;
        this.selectPointsCommand.Parameters[2].Value = targetBound.Y;
        this.selectPointsCommand.Parameters[3].Value = targetBound.X + targetBound.Width;
        this.selectPointsCommand.Parameters[4].Value = targetBound.Y + targetBound.Height;

        var reader = await this.selectPointsCommand.ExecuteReaderAsync(
            CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, ct);
        try
        {
            while (await reader.ReadAsync(ct))
            {
                results.Add(new(new(reader.GetDouble(0), reader.GetDouble(1)), (TValue)reader.GetValue(2)));
            }
        }
        finally
        {
            if (reader is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }
    }

    public async IAsyncEnumerable<KeyValuePair<Point, TValue>> EnumerateBoundAsync(
        long nodeId, Bound targetBound, [EnumeratorCancellation] CancellationToken ct)
    {
        this.selectPointsCommand.Parameters[0].Value = nodeId;
        this.selectPointsCommand.Parameters[1].Value = targetBound.X;
        this.selectPointsCommand.Parameters[2].Value = targetBound.Y;
        this.selectPointsCommand.Parameters[3].Value = targetBound.X + targetBound.Width;
        this.selectPointsCommand.Parameters[4].Value = targetBound.Y + targetBound.Height;

        var reader = await this.selectPointsCommand.ExecuteReaderAsync(
            CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, ct);
        try
        {
            while (await reader.ReadAsync(ct))
            {
                yield return new(new(reader.GetDouble(0), reader.GetDouble(1)), (TValue)reader.GetValue(2));
            }
        }
        finally
        {
            if (reader is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }
    }

    public async ValueTask<RemoveResults> RemovePointsAsync(
        long nodeId, Point point, bool includeRemains, CancellationToken ct)
    {
        this.deletePointCommand.Parameters[0].Value = nodeId;
        this.deletePointCommand.Parameters[1].Value = point.X;
        this.deletePointCommand.Parameters[2].Value = point.Y;

        var removed = await this.deletePointCommand.ExecuteNonQueryAsync(ct);
        if (removed < 0)
        {
            throw new InvalidDataException(
                $"RemovePoints: NodeId={nodeId}, TargetPoint={point}");
        }

        if (includeRemains)
        {
            var remains = await ExecuteRead1Async(
                this.selectPointCountCommand,
                reader => reader.GetInt32(0),
                () => throw new InvalidDataException($"RemovePoints: NodeId={nodeId}"),
                ct,
                nodeId);
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
        this.deleteBoundCommand.Parameters[0].Value = nodeId;
        this.deleteBoundCommand.Parameters[1].Value = bound.X;
        this.deleteBoundCommand.Parameters[2].Value = bound.Y;
        this.deleteBoundCommand.Parameters[3].Value = bound.X + bound.Width;
        this.deleteBoundCommand.Parameters[4].Value = bound.Y + bound.Height;

        var removed = await this.deleteBoundCommand.ExecuteNonQueryAsync(ct);
        if (removed < 0)
        {
            throw new InvalidDataException(
                $"RemoveBound: NodeId={nodeId}, TargetBound={bound}");
        }

        if (includeRemains)
        {
            var remains = await ExecuteRead1Async(
                this.selectPointCountCommand,
                reader => reader.GetInt32(0),
                () => throw new InvalidDataException($"RemovePoints: NodeId={nodeId}"),
                ct,
                nodeId);
            return new(removed, remains);
        }
        else
        {
            return new(removed, -1);
        }
    }
}
