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

    //public Bound[] ChildBounds =>
    //    this.GetChildBounds();

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
