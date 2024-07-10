////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using MassivePoints.Collections;
using MassivePoints.DataProvider;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MassivePoints.Data;

/// <summary>
/// Database configuration.
/// </summary>
public sealed class DbDataProviderConfiguration
{
    public readonly Bound Entire;

    public readonly int MaxNodePoints;

    /// <summary>
    /// Database metadata prefix name.
    /// </summary>
    public readonly string Prefix;

    public readonly Func<string, string> ParameterNameInQuery;
    public readonly Func<string, string> ParameterNameInArgument;
    public readonly Func<int, string> NodePointColumnName;
    public readonly Func<int, string> NodeColumnName;

    public readonly DbQueryDefinition selectNodeQuery;
    public readonly DbQueryDefinition selectPointCountQuery;
    public readonly DbQueryDefinition insertPointQuery;
    public readonly DbQueryDefinition selectNodeMaxIdQuery;
    public readonly DbQueryDefinition updateNodeQuery;
    public readonly DbQueryDefinition insertNodeQuery;
    public readonly DbQueryDefinition deleteNodeQuery;
    public readonly DbQueryDefinition updatePointsQuery;
    public readonly DbQueryDefinition selectPointQuery;
    public readonly DbQueryDefinition selectPointsQuery;
    public readonly DbQueryDefinition selectPointsInclusiveQuery;
    public readonly DbQueryDefinition deletePointQuery;
    public readonly DbQueryDefinition deleteBoundQuery;
    public readonly DbQueryDefinition deleteBoundInclusiveQuery;

    public DbDataProviderConfiguration(
        Bound entire,
        int maxNodePoints = 1024,
        string prefix = "quadtree",
        Func<string, string>? parameterNameInQuery = null,
        Func<string, string>? parameterNameInArgument = null,
        Func<int, string>? nodePointColumnName = null,
        Func<int, string>? nodeColumnName = null)
    {
        this.Entire = entire;
        this.MaxNodePoints = maxNodePoints;
        this.Prefix = prefix;
        this.ParameterNameInQuery = parameterNameInQuery ?? GetParameterName;
        this.ParameterNameInArgument = parameterNameInArgument ?? GetParameterName;
        this.NodePointColumnName = nodePointColumnName ?? GetNodePointColumnName;
        this.NodeColumnName = nodeColumnName ?? GetNodeColumnName;

        var childBoundCount = this.Entire.GetChildBoundCount();
        var dimensionAxisCount = this.Entire.GetDimensionAxisCount();
        
        var nodeColumnNamesJoined = string.Join(",",
            Enumerable.Range(0, childBoundCount).
            Select(this.NodeColumnName));
        var nodeParameterNamesInQueryJoined = string.Join(",",
            Enumerable.Range(0, childBoundCount).
            Select(index => this.ParameterNameInQuery(this.NodeColumnName(index))));
        var nodeParameterNamesInUpdateQueryJoined = string.Join(",",
            Enumerable.Range(0, childBoundCount).
            Select(index => $"{this.NodeColumnName(index)}={this.ParameterNameInQuery(this.NodeColumnName(index))}"));
        var nodeParameterNamesInArgument = Enumerable.Range(0, childBoundCount).
            Select(index => this.ParameterNameInArgument(this.NodeColumnName(index))).
            ToArray();

        var nodePointColumnNamesJoined = string.Join(",",
            Enumerable.Range(0, dimensionAxisCount).
            Select(this.NodePointColumnName));
        var nodePointParameterNamesInQueryJoined = string.Join(",",
            Enumerable.Range(0, dimensionAxisCount).
            Select(index => this.ParameterNameInQuery(this.NodePointColumnName(index))));
        var nodePointParameterNamesInArgument = Enumerable.Range(0, dimensionAxisCount).
            Select(index => this.ParameterNameInArgument(this.NodePointColumnName(index))).
            ToArray();

        var nodePointColumnNamesInPointWhereJoined = string.Join(" AND ",
            Enumerable.Range(0, dimensionAxisCount).
            Select(index => $"{this.ParameterNameInQuery(this.NodePointColumnName(index))}={this.NodePointColumnName(index)}"));
        var nodePointColumnNamesInRangeWhereJoined = string.Join(" AND ",
            Enumerable.Range(0, dimensionAxisCount).
            Select(index => $"{this.ParameterNameInQuery(this.NodePointColumnName(index))}0<={this.NodePointColumnName(index)} AND {this.NodePointColumnName(index)}<{this.ParameterNameInQuery(this.NodePointColumnName(index))}1"));
        var nodePointColumnNamesInRangeInclusiveWhereJoined = string.Join(" AND ",
            Enumerable.Range(0, dimensionAxisCount).
            Select(index => $"{this.ParameterNameInQuery(this.NodePointColumnName(index))}0<={this.NodePointColumnName(index)} AND {this.NodePointColumnName(index)}<={this.ParameterNameInQuery(this.NodePointColumnName(index))}1"));
        var pointParameterNamesInArgument = Enumerable.Range(0, dimensionAxisCount).
            Select(index => this.ParameterNameInArgument(this.NodePointColumnName(index))).
            ToArray();
        var pointRangeParameterNamesInArgument = Enumerable.Range(0, dimensionAxisCount).
            SelectMany(index => new[] { $"{this.ParameterNameInArgument(this.NodePointColumnName(index))}0", $"{this.ParameterNameInArgument(this.NodePointColumnName(index))}1" }).
            ToArray();

        this.selectNodeQuery = new(
            $"SELECT {nodeColumnNamesJoined} FROM {this.Prefix}_nodes WHERE id={this.ParameterNameInQuery("id")}",
            this.ParameterNameInArgument("id"));
        this.selectPointCountQuery = new(
            $"SELECT COUNT(*) FROM {this.Prefix}_node_points WHERE node_id={this.ParameterNameInQuery("node_id")}",
            this.ParameterNameInArgument("node_id"));
        this.insertPointQuery = new(
            $"INSERT INTO {this.Prefix}_node_points (node_id,{nodePointColumnNamesJoined},[value]) VALUES ({this.ParameterNameInQuery("node_id")},{nodePointParameterNamesInQueryJoined},{this.ParameterNameInQuery("value")})",
            [ this.ParameterNameInArgument("node_id"), ..nodePointParameterNamesInArgument, this.ParameterNameInArgument("value")]);
        this.selectNodeMaxIdQuery = new(
            $"SELECT MAX(id) FROM {this.Prefix}_nodes");
        this.updateNodeQuery = new(
            $"UPDATE {this.Prefix}_nodes SET {nodeParameterNamesInUpdateQueryJoined} WHERE id={this.ParameterNameInQuery("id")}",
            [ this.ParameterNameInArgument("id"), ..nodeParameterNamesInArgument ]);
        this.insertNodeQuery = new(
            $"INSERT INTO {this.Prefix}_nodes (id,{nodeColumnNamesJoined}) VALUES ({this.ParameterNameInQuery("id")},{nodeParameterNamesInQueryJoined})",
            [ this.ParameterNameInArgument("id"), ..nodeParameterNamesInArgument ]);
        this.deleteNodeQuery = new(
            $"DELETE FROM {this.Prefix}_nodes WHERE id={this.ParameterNameInQuery("id")}",
            this.ParameterNameInArgument("id"));
        this.updatePointsQuery = new(
            $"UPDATE {this.Prefix}_node_points SET node_id={this.ParameterNameInQuery("to_node_id")} WHERE node_id={this.ParameterNameInQuery("node_id")} AND {nodePointColumnNamesInRangeWhereJoined}",
            [ this.ParameterNameInArgument("node_id"), ..pointRangeParameterNamesInArgument, this.ParameterNameInArgument("to_node_id") ]);
        this.selectPointQuery = new(
            $"SELECT {nodePointColumnNamesJoined},[value] FROM {this.Prefix}_node_points WHERE node_id={this.ParameterNameInQuery("node_id")} AND {nodePointColumnNamesInPointWhereJoined}",
            [ this.ParameterNameInArgument("node_id"), ..pointParameterNamesInArgument ]);
        this.selectPointsQuery = new(
            $"SELECT {nodePointColumnNamesJoined},[value] FROM {this.Prefix}_node_points WHERE node_id={this.ParameterNameInQuery("node_id")} AND {nodePointColumnNamesInRangeWhereJoined}",
            [ this.ParameterNameInArgument("node_id"), ..pointRangeParameterNamesInArgument ]);
        this.selectPointsInclusiveQuery = new(
            $"SELECT {nodePointColumnNamesJoined},[value] FROM {this.Prefix}_node_points WHERE node_id={this.ParameterNameInQuery("node_id")} AND {nodePointColumnNamesInRangeInclusiveWhereJoined}",
            [ this.ParameterNameInArgument("node_id"), ..pointRangeParameterNamesInArgument ]);
        this.deletePointQuery = new(
            $"DELETE FROM {this.Prefix}_node_points WHERE node_id={this.ParameterNameInQuery("node_id")} AND {nodePointColumnNamesInPointWhereJoined}",
            [ this.ParameterNameInArgument("node_id"), ..pointParameterNamesInArgument ]);
        this.deleteBoundQuery = new(
            $"DELETE FROM {this.Prefix}_node_points WHERE node_id={this.ParameterNameInQuery("node_id")} AND {nodePointColumnNamesInRangeWhereJoined}",
            [ this.ParameterNameInArgument("node_id"), ..pointRangeParameterNamesInArgument ]);
        this.deleteBoundInclusiveQuery = new(
            $"DELETE FROM {this.Prefix}_node_points WHERE node_id={this.ParameterNameInQuery("node_id")} AND {nodePointColumnNamesInRangeInclusiveWhereJoined}",
            [ this.ParameterNameInArgument("node_id"), ..pointRangeParameterNamesInArgument ]);
    }

    private static string GetParameterName(string baseName) =>
        $"@{baseName}";
    
    private static string GetNodePointColumnName(int index) =>
        index switch
        {
            0 => "x",
            1 => "y",
            2 => "z",
            _ => $"axis{index}",
        };
    
    private static string GetNodeColumnName(int index) =>
        $"child_id{index}";
}

/// <summary>
/// Non volatile QuadTree ADO.NET data provider.
/// </summary>
/// <typeparam name="TValue">Coordinate point related value type</typeparam>
public class DbDataProvider<TValue> : IDataProvider<TValue, long>
{
    private readonly Func<CancellationToken, ValueTask<DbConnection>> connectionFactory;
    private readonly DbDataProviderConfiguration configuration;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="connectionFactory">`DbConnection` factory</param>
    /// <param name="configuration">Database configuration</param>
    /// <remarks>The connection instance returned by the connection factory must be open.</remarks>
    public DbDataProvider(
        Func<CancellationToken, ValueTask<DbConnection>> connectionFactory,
        DbDataProviderConfiguration configuration)
    {
        this.connectionFactory = connectionFactory;
        this.configuration = configuration;
    }

    public DbDataProviderConfiguration Configuration =>
        this.configuration;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ValueTask<DbConnection> OpenTemporaryConnectionAsync(
        CancellationToken ct) =>
        this.connectionFactory(ct);

    /// <summary>
    /// Create DataProviderSession instance.
    /// </summary>
    /// <param name="connectionCache">`DbConnectionCache`</param>
    /// <returns>`DataProviderSession`</returns>
    /// <remarks>You can derive this class to provide your own `DataProviderSession`.
    /// This is primarily intended to be a proprietary implementation of BulkInsert.</remarks>
    protected virtual DataProviderSession OnCreateDataProviderSession(
        DbConnectionCache connectionCache) =>
        new DataProviderSession(this, connectionCache);

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
        return new(this.OnCreateDataProviderSession(connectionCache));
    }

    /// <summary>
    /// ADO.NET QuadTree data provider session.
    /// </summary>
    protected class DataProviderSession :
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

        public ValueTask FlushAsync() =>
            this.connectionCache.CommitAsync();

        public ValueTask FinishAsync() =>
            this.connectionCache.CommitAsync();

        /// <summary>
        /// This indicates the overall range of the coordinate points managed by data provider.
        /// </summary>
        public Bound Entire =>
            this.parent.Configuration.Entire;

        /// <summary>
        /// Maximum number of coordinate points in each node.
        /// </summary>
        public int MaxNodePoints =>
            this.parent.Configuration.MaxNodePoints;

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
                this.parent.configuration.selectNodeQuery, ct);
            return await command.ExecuteReadOneRecordAsync(
                record =>
                {
                    if (record.IsDBNull(0))
                    {
                        return null;
                    }
                    var chibiIds = new long[this.parent.Configuration.Entire.GetChildBoundCount()];
                    for (var index = 0; index < chibiIds.Length; index++)
                    {
                        chibiIds[index] = record.GetInt64(index);
                    }
                    return new QuadTreeNode<long>(chibiIds);
                },
                ct, nodeId);
        }

        public async ValueTask<int> GetPointCountAsync(
            long nodeId, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.selectPointCountQuery, ct);
            return await command.ExecuteReadOneRecordAsync(
                record => record.GetInt32(0),
                ct,
                nodeId);
        }

        /// <summary>
        /// Inserts the specified coordinate points.
        /// </summary>
        /// <param name="nodeId">Node ID</param>
        /// <param name="points">Coordinate points</param>
        /// <param name="offset">Coordinate point list offset</param>
        /// <param name="isForceInsert">Force insert all points</param>
        /// <param name="ct">`CancellationToken`</param>
        /// <returns>Inserted points</returns>
        /// <remarks>You can override this method to provide your own bulk insertion.</remarks>
        public virtual async ValueTask<int> InsertPointsAsync(
            long nodeId, IReadOnlyArray<PointItem<TValue>> points, int offset, bool isForceInsert, CancellationToken ct)
        {
            using var selectCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.selectPointCountQuery, ct);
            var pointCount = await selectCommand.ExecuteReadOneRecordAsync(
                record => record.GetInt32(0),
                ct, nodeId);

            var insertCount = isForceInsert ?
                points.Count - offset :
                Math.Min(points.Count - offset, this.MaxNodePoints - pointCount);
            if (insertCount <= 0)
            {
                return 0;
            }

            using var insertCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.insertPointQuery, ct);

            var args = new object[1 + this.parent.Configuration.Entire.GetDimensionAxisCount() + 1];
            args[0] = nodeId;
            for (var index = 0; index < insertCount; index++)
            {
                var pointItem = points[index + offset];

                var elements = pointItem.Point.Elements;
                for (var index2 = 0; index2 < elements.Length; index2++)
                {
                    args[1 + index2] = elements[index2];
                }
                args[^1] = (object?)pointItem.Value ?? DBNull.Value;

                if (await insertCommand.ExecuteNonQueryAsync(
                    ct, args) != 1)
                {
                    throw new InvalidDataException(
                        $"AddPoint: NodeId={nodeId}, Point={pointItem.Point}");
                }
            }

            return insertCount;
        }

        private async ValueTask MovePointsAsync(
            long nodeId, Bound bound, long toNodeId, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.updatePointsQuery, ct);

            var args = new object[1 + bound.Axes.Length * 2 + 1];
            args[0] = nodeId;
            for (var index = 0; index < bound.Axes.Length; index++)
            {
                var axis = bound.Axes[index];
                args[1 + index * 2] = axis.Origin;
                args[1 + index * 2 + 1] = axis.To;
            }
            args[^1] = toNodeId;

            var moved = await command.ExecuteNonQueryAsync(
                ct, args);
            if (moved < 0)
            {
                throw new InvalidDataException(
                    $"MovePoints: NodeId={nodeId}, TargetBound={bound}, ToNodeId={toNodeId}");
            }
            
#if DEBUG
            if (moved >= 1)
            {
                Debug.WriteLine($"Moved: From={nodeId}, To={toNodeId}, Bound={bound}, Count={moved}");
            }
#endif
        }

        public async ValueTask<QuadTreeNode<long>> DistributePointsAsync(
            long nodeId, Bound[] toBounds, CancellationToken ct)
        {
            using var selectCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.selectNodeMaxIdQuery, ct);
            if (await selectCommand.ExecuteReadOneRecordAsync(
                recored =>
                {
                    var baseNodeId = recored.GetInt64(0) + 1;
                    var childIds = new long[this.parent.Configuration.Entire.GetChildBoundCount()];
                    for (var index = 0; index < childIds.Length; index++)
                    {
                        childIds[index] = baseNodeId + index;
                    }
                    return new QuadTreeNode<long>(childIds);
                },
                ct) is not { } node)
            {
                throw new InvalidDataException(
                    $"DistributePoints [1]: NodeId={nodeId}");
            }
            
            using var updateCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.updateNodeQuery, ct);

            var childIds = node.ChildIds;
            var args = new object[1 + childIds.Length];
            args[0] = nodeId;
            for (var index = 0; index < childIds.Length; index++)
            {
                args[1 + index] = childIds[index];
            }
            
            if (await updateCommand.ExecuteNonQueryAsync(
                ct, args) < 0)
            {
                throw new InvalidDataException(
                    $"DistributePoints [2]: NodeId={nodeId}");
            }

            using var insertCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.insertNodeQuery, ct);
            var toNodeIds = node.ChildIds;
            
            for (var index = 1; index < args.Length; index++)
            {
                args[index] = DBNull.Value;
            }

            for (var index = 0; index < toBounds.Length; index++)
            {
                args[0] = toNodeIds[index];
                
                if (await insertCommand.ExecuteNonQueryAsync(
                    ct, args) < 0)
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
                    this.parent.configuration.deleteNodeQuery, ct);
                if (await deleteCommand.ExecuteNonQueryAsync(
                    ct, nodeId) < 0)
                {
                    throw new InvalidDataException(
                        $"AggregatePoints [1]: NodeId={nodeId}");
                }
            }

            using var updateCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.updateNodeQuery, ct);
            
            var args = new object[1 + this.parent.Configuration.Entire.GetDimensionAxisCount() * 2];
            args[0] = toNodeId;
            for (var index = 1; index < args.Length; index++)
            {
                args[index] = DBNull.Value;
            }

            if (await updateCommand.ExecuteNonQueryAsync(
                ct, args) < 0)
            {
                throw new InvalidDataException(
                    $"AggregatePoints [2]: NodeId={toNodeId}");
            }
        }

        public async ValueTask<IReadOnlyArray<PointItem<TValue>>> LookupPointAsync(
            long nodeId, Point targetPoint, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.selectPointQuery, ct);
            
            var args = new object[1 + targetPoint.Elements.Length];
            args[0] = nodeId;
            for (var index = 0; index < targetPoint.Elements.Length; index++)
            {
                args[1 + index] = targetPoint.Elements[index];
            }

            var results = new ExpandableArray<PointItem<TValue>>(Math.Max(4, this.MaxNodePoints / 4));
            await command.ExecuteReadRecordsAsync(
                record =>
                {
                    var rps = new double[targetPoint.Elements.Length];
                    for (var index = 0; index < rps.Length; index++)
                    {
                        rps[index] = record.GetDouble(index);
                    }
                    results.Add(new PointItem<TValue>(
                        new Point(rps),
                        (TValue)record.GetValue(rps.Length)));
                },
                ct, args);
            return results;
        }

        public async ValueTask<IReadOnlyArray<PointItem<TValue>>> LookupBoundAsync(
            long nodeId, Bound targetBound, bool inclusiveBoundTo, CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                inclusiveBoundTo ?
                    this.parent.configuration.selectPointsInclusiveQuery :
                    this.parent.configuration.selectPointsQuery,
                ct);
            
            var args = new object[1 + targetBound.GetDimensionAxisCount() * 2];
            args[0] = nodeId;
            for (var index = 0; index < targetBound.Axes.Length; index++)
            {
                var axis = targetBound.Axes[index];
                args[1 + index * 2] = axis.Origin;
                args[1 + index * 2 + 1] = axis.To;
            }

            var results = new ExpandableArray<PointItem<TValue>>(Math.Max(4, this.MaxNodePoints / 4));
            await command.ExecuteReadRecordsAsync(
                record =>
                {
                    var rps = new double[targetBound.GetDimensionAxisCount()];
                    for (var index = 0; index < rps.Length; index++)
                    {
                        rps[index] = record.GetDouble(index);
                    }
                    results.Add(new PointItem<TValue>(
                        new Point(rps),
                        (TValue)record.GetValue(rps.Length)));
                },
                ct, args);
            return results;
        }

        public async IAsyncEnumerable<PointItem<TValue>> EnumerateBoundAsync(
            long nodeId, Bound targetBound, bool inclusiveBoundTo, [EnumeratorCancellation] CancellationToken ct)
        {
            using var command = await this.connectionCache.GetPreparedCommandAsync(
                inclusiveBoundTo ?
                    this.parent.configuration.selectPointsInclusiveQuery :
                    this.parent.configuration.selectPointsQuery,
                ct);
            
            var args = new object[1 + targetBound.GetDimensionAxisCount() * 2];
            args[0] = nodeId;
            for (var index = 0; index < targetBound.GetDimensionAxisCount(); index++)
            {
                var axis = targetBound.Axes[index];
                args[1 + index * 2] = axis.Origin;
                args[1 + index * 2 + 1] = axis.To;
            }

            await foreach (var result in command.ExecuteEnumerateAsync(
               record =>
               {
                   var rps = new double[targetBound.Axes.Length];
                   for (var index = 0; index < rps.Length; index++)
                   {
                       rps[index] = record.GetDouble(index);
                   }
                   return new PointItem<TValue>(
                       new Point(rps),
                       (TValue)record.GetValue(rps.Length));
               },
               ct, args))
            {
                yield return result;
            }
        }

        public async ValueTask<RemoveResults> RemovePointAsync(
            long nodeId, Point point, bool includeRemains, CancellationToken ct)
        {
            using var deleteCommand = await this.connectionCache.GetPreparedCommandAsync(
                this.parent.configuration.deletePointQuery, ct);
            
            var args = new object[1 + point.Elements.Length];
            args[0] = nodeId;
            for (var index = 0; index < point.Elements.Length; index++)
            {
                args[1 + index] = point.Elements[index];
            }

            var removed = await deleteCommand.ExecuteNonQueryAsync(
                ct, args);
            if (removed < 0)
            {
                throw new InvalidDataException(
                    $"RemovePoints: NodeId={nodeId}, TargetPoint={point}");
            }

            if (includeRemains)
            {
                using var selectCommand = await this.connectionCache.GetPreparedCommandAsync(
                    this.parent.configuration.selectPointCountQuery, ct);
                if (await selectCommand.ExecuteReadOneRecordAsync(
                    record => (int?)record.GetInt32(0),
                    ct, nodeId) is not { } remains)
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
            long nodeId, Bound bound, bool isRightClosed, bool includeRemains, CancellationToken ct)
        {
            using var deleteCommand = await this.connectionCache.GetPreparedCommandAsync(
                isRightClosed ?
                    this.parent.configuration.deleteBoundInclusiveQuery :
                    this.parent.configuration.deleteBoundQuery,
                ct);
            
            var args = new object[1 + bound.GetDimensionAxisCount() * 2];
            args[0] = nodeId;
            for (var index = 0; index < bound.GetDimensionAxisCount(); index++)
            {
                var axis = bound.Axes[index];
                args[1 + index * 2] = axis.Origin;
                args[1 + index * 2 + 1] = axis.To;
            }

            var removed = await deleteCommand.ExecuteNonQueryAsync(
                ct, args);
            if (removed < 0)
            {
                throw new InvalidDataException(
                    $"RemoveBound: NodeId={nodeId}, TargetBound={bound}");
            }

            if (includeRemains)
            {
                using var selectCommand = await this.connectionCache.GetPreparedCommandAsync(
                    this.parent.configuration.selectPointCountQuery, ct);
                if (await selectCommand.ExecuteReadOneRecordAsync(
                    record => (int?)record.GetInt32(0),
                    ct, nodeId) is not { } remains)
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
