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
public readonly struct Bound
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
    
    /// <summary>
    /// X
    /// </summary>
    public double X { get; }
    
    /// <summary>
    /// Y
    /// </summary>
    public double Y { get; }

    /// <summary>
    /// Range width
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Range height
    /// </summary>
    public double Height { get; }

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
    public Bound[] ChildBounds
    {
        get
        {
            var wh = this.Width / 2;
            var hh = this.Height / 2;
            var topLeft = new Bound(this.X, this.Y, wh, hh);
            var topRight = new Bound(this.X + wh, this.Y, wh, hh);
            var bottomLeft = new Bound(this.X, this.Y + hh, wh, hh);
            var bottomRight = new Bound(this.X + wh, this.Y + hh, wh, hh);
            return new[] { topLeft, topRight, bottomLeft, bottomRight };
        }
    }

    public override string ToString() =>
        $"Bound: [{this.X},{this.Y} - {this.X + this.Width},{this.Y + this.Height}), Size={this.Width},{this.Height}";

    public static implicit operator Bound((double width, double height) size) =>
        new(size.width, size.height);

    public static implicit operator Bound((Point point, double width, double height) bound) =>
        new(bound.point, bound.width, bound.height);

    public static implicit operator Bound((double x, double y, double width, double height) bound) =>
        new(bound.x, bound.y, bound.width, bound.height);

    public static Bound Create(double width, double height) =>
        new(width, height);

    public static Bound Create(Point point, double width, double height) =>
        new(point, width, height);

    public static Bound Create(double x, double y, double width, double height) =>
        new(x, y, width, height);
}

public static class BoundExtension
{
    public static void Deconstruct(
        this Bound self,
        out double x,
        out double y,
        out double width,
        out double height)
    {
        x = self.X;
        y = self.Y;
        width = self.Width;
        height = self.Height;
    }

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="point">A coordinate point</param>
    /// <returns>True when within.</returns>
    public static bool IsWithin(
        this Bound self, Point point) =>
        self.X <= point.X && point.X < (self.X + self.Width) &&
        self.Y <= point.Y && point.Y < (self.Y + self.Height);

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>True is within.</returns>
    public static bool IsWithin(
        this Bound self, double x, double y) =>
        self.X <= x && x < (self.X + self.Width) &&
        self.Y <= y && y < (self.Y + self.Height);

    /// <summary>
    /// Checks whether the specified range is intersects this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="bound">Coordinate range</param>
    /// <returns>True when intersected.</returns>
    public static bool IsIntersection(
        this Bound self, Bound bound)
    {
        var lx1 = self.X;
        var lx2 = self.X + self.Width;
        var rx1 = bound.X;
        var rx2 = bound.X + bound.Width;

        if (lx1 > rx2 || rx1 > lx2)
        {
            return false;
        }

        var ly1 = self.Y;
        var ly2 = self.Y + self.Height;
        var ry1 = bound.Y;
        var ry2 = bound.Y + bound.Height;

        if (ly1 > ry2 || ry1 > ly2)
        {
            return false;
        }

        return true;
    }
}
