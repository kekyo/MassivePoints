////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

namespace MassivePoints.DataProvider;

/// <summary>
/// The QuadTree node information.
/// </summary>
/// <typeparam name="TNodeId">Type indicating the ID of the index node managed by the data provider</typeparam>
public sealed class QuadTreeNode<TNodeId>
{
    //         - ------ X ------> +
    // -  +-------------+-------------+
    // |  |             |             |
    // |  | TopLeft     | TopRight    |
    // |  | ChildIds[0] | ChildIds[1] |
    // |  |             |             |
    // Y  +-------------+-------------+
    // |  |             |             |
    // |  | BottomLeft  | BottomRight |
    // |  | ChildIds[2] | ChildIds[3] |
    // |  |             |             |
    // v  +-------------+-------------+

    /// <summary>
    /// Child node id list.
    /// </summary>
    public readonly TNodeId[] ChildIds;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="childIds">Child not id list</param>
    public QuadTreeNode(TNodeId[] childIds) =>
        this.ChildIds = childIds;

    public TNodeId TopLeftId =>
        this.ChildIds is [var id,_,_,_] ? id : default!;

    public TNodeId TopRightId =>
        this.ChildIds is [_,var id,_,_] ? id : default!;

    public TNodeId BottomLeftId =>
        this.ChildIds is [_,_,var id,_] ? id : default!;

    public TNodeId BottomRightId =>
        this.ChildIds is [_,_,_,var id] ? id : default!;

    public override string ToString() =>
        $"Node: {string.Join(",", this.ChildIds)}";
}
