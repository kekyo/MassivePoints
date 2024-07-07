////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;

namespace MassivePoints;

/// <summary>
/// This is a structure that defines a coordinate range.
/// </summary>
public readonly struct Bound : IEquatable<Bound>
{
    //         - ------ X ------> +
    // -  +-------------+-------------+
    // |  |             |             |
    // |  | TopLeft     | TopRight    |
    // |  |   Axes[0]   |   Axes[1]   |
    // |  |             |             |
    // Y  +-------------+-------------+
    // |  |             |             |
    // |  | BottomLeft  | BottomRight |
    // v  |   Axes[2]   |   Axes[3]   |
    // |  |             |             |
    // +  +-------------+-------------+

    private static readonly object locker = new();
    private static int[] sizes = [1, 2, 4, 8, 16];

    /// <summary>
    /// Get child bound count.
    /// </summary>
    /// <param name="dimension">Target dimension</param>
    /// <returns>Child bound count.</returns>
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

    /// <summary>
    /// The earth globe (2D) bound.
    /// </summary>
    public static readonly Bound TheGlobe2D =
        new(0.0, -90.0, 360.0, 180);

    /// <summary>
    /// The axis definitions.
    /// </summary>
    public readonly Axis[] Axes;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="width">Range width</param>
    /// <param name="height">Range height</param>
    /// <remarks>This constructor will create 2D range based on zero origin.</remarks>
    public Bound(double width, double height) =>
        this.Axes = [new Axis(0, width), new Axis(0, height)];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="origin">Origin point</param>
    /// <param name="width">Range width</param>
    /// <param name="height">Range height</param>
    /// <remarks>This constructor will create 2D range.</remarks>
    public Bound(Point origin, double width, double height)
    {
        if (origin.Elements is not [var x, var y])
        {
            throw new ArgumentException($"Could not create non 2D range: {origin.Elements.Length}");
        }
        this.Axes = [new Axis(x, width), new Axis(y, height)];
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">Origin X point</param>
    /// <param name="y">Origin Y point</param>
    /// <param name="width">Range width</param>
    /// <param name="height">Range height</param>
    /// <remarks>This constructor will create 2D range.</remarks>
    public Bound(double x, double y, double width, double height) =>
        this.Axes = [new Axis(x, width), new Axis(y, height)];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="width">Range width</param>
    /// <param name="height">Range height</param>
    /// <param name="depth">Range depth</param>
    /// <remarks>This constructor will create 3D range based on zero origin.</remarks>
    public Bound(double width, double height, double depth) =>
        this.Axes = [new Axis(0, width), new Axis(0, height), new Axis(0, depth)];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="origin">Origin point</param>
    /// <param name="width">Range width</param>
    /// <param name="height">Range height</param>
    /// <param name="depth">Range depth</param>
    /// <remarks>This constructor will create 2D range.</remarks>
    public Bound(Point origin, double width, double height, double depth)
    {
        if (origin.Elements is not [var x, var y, var z])
        {
            throw new ArgumentException($"Could not create non 3D range: {origin.Elements.Length}");
        }
        this.Axes = [new Axis(x, width),new Axis(y, height),new Axis(z, depth)];
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">Origin X point</param>
    /// <param name="y">Origin Y point</param>
    /// <param name="z">Origin Z point</param>
    /// <param name="width">Range width</param>
    /// <param name="height">Range height</param>
    /// <param name="depth">Range depth</param>
    /// <remarks>This constructor will create 2D range.</remarks>
    public Bound(double x, double y, double z, double width, double height, double depth) =>
        this.Axes = [new Axis(x, width), new Axis(y, height), new Axis(z, depth)];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="axes">The axes define a range</param>
    public Bound(params Axis[] axes) =>
        this.Axes = axes;

    /// <summary>
    /// X axis origin.
    /// </summary>
    public double X =>
        this.Axes is [var x,..] ? x.Origin : double.NaN;
    
    /// <summary>
    /// Y axis origin.
    /// </summary>
    public double Y =>
        this.Axes is [_,var y,..] ? y.Origin : double.NaN;
    
    /// <summary>
    /// Z axis origin.
    /// </summary>
    public double Z =>
        this.Axes is [_,_,var z,..] ? z.Origin : double.NaN;

    /// <summary>
    /// Range width.
    /// </summary>
    public double Width =>
        this.Axes is [var x,..] ? x.Size : double.NaN;

    /// <summary>
    /// Range height.
    /// </summary>
    public double Height =>
        this.Axes is [_,var y,..] ? y.Size : double.NaN;

    /// <summary>
    /// Range depth.
    /// </summary>
    public double Depth =>
        this.Axes is [_,_,var z,..] ? z.Size : double.NaN;

    /// <summary>
    /// Is size valid.
    /// </summary>
    public bool IsValidSize
    {
        get
        {
            foreach (var axis in this.Axes)
            {
                if (!axis.IsValidSize)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public bool Equals(Bound other)
    {
        if (this.Axes.Length != other.Axes.Length)
        {
            return false;
        }
        for (var index = 0; index < this.Axes.Length; index++)
        {
            if (this.Axes[index].Equals(other.Axes[index]) == false)
            {
                return false;
            }
        }
        return true;
    }

    bool IEquatable<Bound>.Equals(Bound other) =>
        this.Equals(other);

    public override bool Equals(object? obj) =>
        obj is Bound rhs && this.Equals(rhs);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 0;
            for (var index = 0; index < this.Axes.Length; index++)
            {
                hash ^= this.Axes[index].GetHashCode() * 397;
            }
            return hash;
        }
    }

    public override string ToString() =>
        $"Bound: [{string.Join(",", this.Axes.Select(axis => axis.Origin))} - {string.Join(",", this.Axes.Select(axis => axis.Origin + axis.Size))}), Size={string.Join(",", this.Axes.Select(axis => axis.Size))}";

    public static implicit operator Bound((double width, double height) size) =>
        new Bound(size.width, size.height);

    public static implicit operator Bound((Point point, double width, double height) bound) =>
        new Bound(bound.point, bound.width, bound.height);

    public static implicit operator Bound((double x, double y, double width, double height) bound) =>
        new Bound(bound.x, bound.y, bound.width, bound.height);

    public static implicit operator Bound((double width, double height, double depth) size) =>
        new Bound(size.width, size.height, size.depth);

    public static implicit operator Bound((Point point, double width, double height, double depth) bound) =>
        new Bound(bound.point, bound.width, bound.height, bound.depth);

    public static implicit operator Bound((double x, double y, double z, double width, double height, double depth) bound) =>
        new Bound(bound.x, bound.y, bound.z, bound.width, bound.height, bound.depth);

    public static Bound Create(double width, double height) =>
        new Bound(width, height);

    public static Bound Create(Point point, double width, double height) =>
        new Bound(point, width, height);

    public static Bound Create(double x, double y, double width, double height) =>
        new Bound(x, y, width, height);

    public static Bound Create(double width, double height, double depth) =>
        new Bound(width, height, depth);

    public static Bound Create(Point point, double width, double height, double depth) =>
        new Bound(point, width, height, depth);

    public static Bound Create(double x, double y, double z, double width, double height, double depth) =>
        new Bound(x, y, z, width, height, depth);
}
