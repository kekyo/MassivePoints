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
    /// <remarks>[-180.0,-90.0 - 180.0,90.0)</remarks>
    public static readonly Bound TheGlobe2D =
        new(-180.0, -90.0, 360.0, 180.0);

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
    /// <param name="to">To point</param>
    /// <remarks>This constructor will create rectangle range between two points.</remarks>
    public Bound(Point origin, Point to)
    {
        if (origin.Elements.Length != to.Elements.Length)
        {
            throw new ArgumentException(
                $"Dimensions does not match: {origin.Elements.Length} != {to.Elements.Length}");
        }
        var axes = new Axis[origin.Elements.Length];
        for (var index = 0; index < origin.Elements.Length; index++)
        {
            axes[index] = new(origin.Elements[index], to.Elements[index]);
        }
        this.Axes = axes;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x0">Origin X point</param>
    /// <param name="y0">Origin Y point</param>
    /// <param name="x1">To X point</param>
    /// <param name="y1">To Y point</param>
    /// <remarks>This constructor will create 2D range.</remarks>
    public Bound(double x0, double y0, double x1, double y1) =>
        this.Axes = [new Axis(x0, x1), new Axis(y0, y1)];

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
    /// <param name="x0">Origin X point</param>
    /// <param name="y0">Origin Y point</param>
    /// <param name="z0">Origin Z point</param>
    /// <param name="x1">To X point</param>
    /// <param name="y1">To Y point</param>
    /// <param name="z1">To Z point</param>
    /// <remarks>This constructor will create 2D range.</remarks>
    public Bound(double x0, double y0, double z0, double x1, double y1, double z1) =>
        this.Axes = [new Axis(x0, x1), new Axis(y0, y1), new Axis(z0, z1)];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="axes">The axes define a range</param>
    public Bound(params Axis[] axes) =>
        this.Axes = axes;

    /// <summary>
    /// X axis origin.
    /// </summary>
    public double X0 =>
        this.Axes is [var x,..] ? x.Origin : double.NaN;
    
    /// <summary>
    /// Y axis origin.
    /// </summary>
    public double Y0 =>
        this.Axes is [_,var y,..] ? y.Origin : double.NaN;
    
    /// <summary>
    /// Z axis origin.
    /// </summary>
    public double Z0 =>
        this.Axes is [_,_,var z,..] ? z.Origin : double.NaN;

    /// <summary>
    /// X axis to (exclusive).
    /// </summary>
    public double X1 =>
        this.Axes is [var x,..] ? x.To : double.NaN;
    
    /// <summary>
    /// Y axis to (exclusive).
    /// </summary>
    public double Y1 =>
        this.Axes is [_,var y,..] ? y.To : double.NaN;
    
    /// <summary>
    /// Z axis to (exclusive).
    /// </summary>
    public double Z1 =>
        this.Axes is [_,_,var z,..] ? z.To : double.NaN;

    /// <summary>
    /// Range width (hint).
    /// </summary>
    public double WidthHint =>
        this.Axes is [var x,..] ? x.SizeHint : double.NaN;

    /// <summary>
    /// Range height (hint).
    /// </summary>
    public double HeightHint =>
        this.Axes is [_,var y,..] ? y.SizeHint : double.NaN;

    /// <summary>
    /// Range depth (hint).
    /// </summary>
    public double DepthHint =>
        this.Axes is [_,_,var z,..] ? z.SizeHint : double.NaN;

    /// <summary>
    /// Is size empty.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            foreach (var axis in this.Axes)
            {
                // Be ruined.
                if (axis.IsEmpty)
                {
                    return true;
                }
            }
            return false;
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
        $"[{string.Join(",", this.Axes.Select(axis => axis.Origin))} - {string.Join(",", this.Axes.Select(axis => axis.To))}), Size={string.Join(",", this.Axes.Select(axis => axis.SizeHint))}";

    public static implicit operator Bound((double width, double height) size) =>
        new Bound(size.width, size.height);

    public static implicit operator Bound((Point origin, Point to) bound) =>
        new Bound(bound.origin, bound.to);

    public static implicit operator Bound((double x0, double y0, double x1, double y1) bound) =>
        new Bound(bound.x0, bound.y0, bound.x1, bound.y1);

    public static implicit operator Bound((double width, double height, double depth) size) =>
        new Bound(size.width, size.height, size.depth);

    public static implicit operator Bound((double x0, double y0, double z0, double x1, double y1, double z1) bound) =>
        new Bound(bound.x0, bound.y0, bound.z0, bound.x1, bound.y1, bound.z1);

    public static Bound Create(double width, double height) =>
        new Bound(width, height);

    public static Bound Create(Point origin, Point to) =>
        new Bound(origin, to);

    public static Bound Create(double x0, double y0, double x1, double y1) =>
        new Bound(x0, y0, x1, y1);

    public static Bound Create(double width, double height, double depth) =>
        new Bound(width, height, depth);

    public static Bound Create(double x0, double y0, double z0, double x1, double y1, double z1) =>
        new Bound(x0, y0, z0, x1, y1, z1);
}
