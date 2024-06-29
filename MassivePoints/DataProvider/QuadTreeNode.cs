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
    private TNodeId[]? childIds;
    
    public readonly TNodeId TopLeftId;
    public readonly TNodeId TopRightId;
    public readonly TNodeId BottomLeftId;
    public readonly TNodeId BottomRightId;
    
    public QuadTreeNode(
        TNodeId topLeftNodeId, TNodeId topRightNodeId, TNodeId bottomLeftNodeId, TNodeId bottomRightNodeId)
    {
        this.TopLeftId = topLeftNodeId;
        this.TopRightId = topRightNodeId;
        this.BottomLeftId = bottomLeftNodeId;
        this.BottomRightId = bottomRightNodeId;
    }

    public TNodeId[] ChildIds
    {
        get
        {
            if (this.childIds == null)
            {
                this.childIds = new[] { this.TopLeftId, TopRightId, BottomLeftId, BottomRightId };
            }
            return this.childIds;
        }
    }

    public override string ToString() =>
        $"Node: {this.TopLeftId},{this.TopRightId},{this.BottomLeftId},{this.BottomRightId}";
}
