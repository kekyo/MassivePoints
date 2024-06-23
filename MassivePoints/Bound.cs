////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.ComponentModel;

namespace MassivePoints;

/// <summary>
/// This is a structure that defines a coordinate range.
/// </summary>
public sealed class Bound
{
    //         - ------ X ------> +
    // -  +-------------+-------------+
    // |  |             |             |
    // |  | TopLeft     | TopRight    |
    // |  |             |             |
    // Y  +-------------+-------------+
    // |  |             |             |
    // |  | BottomLeft  | BottomRight |
    // v  |             |             |
    // +  +-------------+-------------+

    private Bound? topLeft;
    private Bound? topRight;
    private Bound? bottomLeft;
    private Bound? bottomRight;
    
    /// <summary>
    /// X
    /// </summary>
    public readonly double X;
    
    /// <summary>
    /// Y
    /// </summary>
    public readonly double Y;
    
    /// <summary>
    /// Range width
    /// </summary>
    public readonly double Width;

    /// <summary>
    /// Range height
    /// </summary>
    public readonly double Height;

    public Bound(double width, double height)
    {
        this.X = 0;
        this.Y = 0;
        this.Width = width;
        this.Height = height;
    }

    public Bound(Point point, double width, double height)
    {
        this.X = point.X;
        this.Y = point.Y;
        this.Width = width;
        this.Height = height;
    }

    public Bound(double x, double y, double width, double height)
    {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Bound TopLeft
    {
        get
        {
            if (this.topLeft == null)
            {
                var wh = this.Width / 2;
                var hh = this.Height / 2;
                this.topLeft = new(this.X, this.Y, wh, hh);
            }
            return this.topLeft;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Bound TopRight
    {
        get
        {
            if (this.topRight == null)
            {
                var wh = this.Width / 2;
                var hh = this.Height / 2;
                this.topRight = new(this.X + wh, this.Y, wh, hh);
            }
            return this.topRight;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Bound BottomLeft
    {
        get
        {
            if (this.bottomLeft == null)
            {
                var wh = this.Width / 2;
                var hh = this.Height / 2;
                this.bottomLeft = new(this.X, this.Y + hh, wh, hh);
            }
            return this.bottomLeft;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Bound BottomRight
    {
        get
        {
            if (this.bottomRight == null)
            {
                var wh = this.Width / 2;
                var hh = this.Height / 2;
                this.bottomRight = new(this.X + wh, this.Y + hh, wh, hh);
            }
            return this.bottomRight;
        }
    }

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>True is within.</returns>
    public bool IsWithin(double x, double y) =>
        this.X <= x && x < (this.X + this.Width) &&
        this.Y <= y && y < (this.Y + this.Height);

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="point">A coordinate point</param>
    /// <returns>True when within.</returns>
    public bool IsWithin(Point point) =>
        this.IsWithin(point.X, point.Y);

    /// <summary>
    /// Checks whether the specified range is intersects this range.
    /// </summary>
    /// <param name="bound">Coordinate range</param>
    /// <returns>True when intersected.</returns>
    public bool IsIntersection(Bound bound)
    {
        var lx1 = this.X;
        var lx2 = this.X + this.Width;
        var rx1 = bound.X;
        var rx2 = bound.X + bound.Width;

        if (lx1 > rx2 || rx1 > lx2)
        {
            return false;
        }

        var ly1 = this.Y;
        var ly2 = this.Y + this.Height;
        var ry1 = bound.Y;
        var ry2 = bound.Y + bound.Height;

        if (ly1 > ry2 || ry1 > ly2)
        {
            return false;
        }

        return true;
    }

    public void Deconstruct(
        out double x,
        out double y,
        out double width,
        out double height)
    {
        x = this.X;
        y = this.Y;
        width = this.Width;
        height = this.Height;
    }

    public override string ToString() =>
        $"Bound: [{this.X},{this.Y} - {this.X + this.Width},{this.Y + this.Height}), Size={this.Width},{this.Height}";

    public static implicit operator Bound((double width, double height) size) =>
        new(size.width, size.height);

    public static implicit operator Bound((Point point, double width, double height) bound) =>
        new(bound.point, bound.width, bound.height);

    public static implicit operator Bound((double x, double y, double width, double height) bound) =>
        new(bound.x, bound.y, bound.width, bound.height);
}
