////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.ComponentModel;

namespace MassivePoints.DataProvider;

/// <summary>
/// The QuadTree node information.
/// </summary>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
public sealed class QuadTreeNode<TNodeId>
{
    public readonly TNodeId[] ChildIds;

    public QuadTreeNode(TNodeId[] childIds) =>
        this.ChildIds = childIds;

    public QuadTreeNode(
        TNodeId topLeftNodeId, TNodeId topRightNodeId, TNodeId bottomLeftNodeId, TNodeId bottomRightNodeId) =>
        this.ChildIds = [topLeftNodeId,topRightNodeId,bottomLeftNodeId,bottomRightNodeId];

    //public TNodeId TopLeftId =>
    //    this.ChildIds[0];

    //public TNodeId TopRightId =>
    //    this.ChildIds[1];

    //public TNodeId BottomLeftId =>
    //    this.ChildIds[2];

    //public TNodeId BottomRightId =>
    //    this.ChildIds[3];

    public override string ToString() =>
        $"Node: {string.Join(",", this.ChildIds)}";
}
