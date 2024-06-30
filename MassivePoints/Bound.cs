////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Linq;

namespace MassivePoints;

public readonly struct Axis
{
    public readonly double Origin;
    public readonly double Size;

    public Axis(double origin, double size)
    {
        this.Origin = origin;
        this.Size = size;
    }

    public override string ToString() =>
        $"Axis: {this.Origin} ({this.Size})";

    public void Deconstruct(
        double origin,
        double size)
    {
        origin = this.Origin;
        size = this.Size;
    }
}

/// <summary>
/// This is a structure that defines a coordinate range.
/// </summary>
public struct Bound
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

    private static readonly object locker = new();
    private static int[] sizes = [1, 2, 4, 8, 16];

    public static int GetChildBoundCount(int dimension)
    {
        if (dimension >= sizes.Length)
        {
            lock (locker)
            {
                if (dimension >= sizes.Length)
                {
                    var newSizes = new int[dimension + 1];
                    var size = 1;
                    for (var index = 0; index <= dimension; index++)
                    {
                        newSizes[index] = size;
                        size *= 2;
                    }
                    sizes = newSizes;
                }
            }
        }
        return sizes[dimension];
    }

    private Bound[]? childBounds;

    public readonly Axis[] Axes;

    /// <summary>
    /// X
    /// </summary>
    public double X =>
        this.Axes[0].Origin;
    
    /// <summary>
    /// Y
    /// </summary>
    public double Y =>
        this.Axes[1].Origin;

    /// <summary>
    /// Range width
    /// </summary>
    public double Width =>
        this.Axes[0].Size;

    /// <summary>
    /// Range height
    /// </summary>
    public double Height =>
        this.Axes[1].Size;

    public Bound(double width, double height) =>
        this.Axes = [new(0, width), new(0, height)];

    public Bound(Point point, double width, double height) =>
        this.Axes = [new(point.X, width), new(point.Y, height)];

    public Bound(double x, double y, double width, double height) =>
        this.Axes = [new(x, width), new(y, height)];

    public Bound(Axis[] axes) =>
        this.Axes = axes;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Bound[] ChildBounds
    {
        get
        {
            if (this.childBounds == null)
            {
                var childBounds = new Bound[GetChildBoundCount(this.Axes.Length)];
                for (var childIndex = 0; childIndex < childBounds.Length; childIndex++)
                {
                    var axes = new Axis[this.Axes.Length];
                    var halfBits = childIndex;
                    for (var axisIndex = 0; axisIndex < axes.Length; axisIndex++, halfBits >>= 1)
                    {
                        var halfSize = this.Axes[axisIndex].Size / 2;
                        if ((halfBits & 0x01) == 0x01)
                        {
                            axes[axisIndex] = new Axis(
                                this.Axes[axisIndex].Origin + halfSize,
                                halfSize);
                        }
                        else
                        {
                            axes[axisIndex] = new Axis(
                                this.Axes[axisIndex].Origin,
                                halfSize);
                        }
                    }
                    childBounds[childIndex] = new Bound(axes);
                }
                this.childBounds = childBounds;
            }
            return this.childBounds;
        }
    }

    public override string ToString() =>
        $"Bound: [{string.Join(",", this.Axes.Select(axis => axis.Origin))} - {string.Join(",", this.Axes.Select(axis => axis.Origin + axis.Size))}), Size={string.Join(",", this.Axes.Select(axis => axis.Size))}";

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
        out Axis[] axes) =>
        axes = self.Axes;

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
        this Bound self, Point point)
    {
        if (self.Axes.Length != point.Elements.Length)
        {
            throw new ArgumentException(
                $"Could not compare difference dimension: {self.Axes.Length} != {point.Elements.Length}");
        }

        for (var index = 0; index < self.Axes.Length; index++)
        {
            var b = self.Axes[index];
            var e = point.Elements[index];
            if (!(b.Origin <= e && e < (b.Origin + b.Size)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>True is within.</returns>
    public static bool IsWithin(
        this Bound self, double x, double y) =>
        IsWithin(self, new Point(x, y));

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
