////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

namespace MassivePoints;

public sealed class QuadTreeNode<TNodeId>
{
    public readonly TNodeId TopLeftId;
    public readonly TNodeId TopRightId;
    public readonly TNodeId BottomLeftId;
    public readonly TNodeId BottomRightId;

    public TNodeId[] ChildIds =>
        new[] { this.TopLeftId, TopRightId, BottomLeftId, BottomRightId };
    
    public QuadTreeNode(
        TNodeId topLeftNodeId, TNodeId topRightNodeId, TNodeId bottomLeftNodeId, TNodeId bottomRightNodeId)
    {
        this.TopLeftId = topLeftNodeId;
        this.TopRightId = topRightNodeId;
        this.BottomLeftId = bottomLeftNodeId;
        this.BottomRightId = bottomRightNodeId;
    }

    public override string ToString() =>
        $"Node: {this.TopLeftId},{this.TopRightId},{this.BottomLeftId},{this.BottomRightId}";
}
