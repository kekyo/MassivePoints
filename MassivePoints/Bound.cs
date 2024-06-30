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

public readonly struct Axis : IEquatable<Axis>
{
    public readonly double Origin;
    public readonly double Size;

    public Axis(double origin, double size)
    {
        this.Origin = origin;
        this.Size = size;
    }

    public bool Equals(Axis other) =>
        this.Origin == other.Origin &&
        this.Size == other.Size;

    bool IEquatable<Axis>.Equals(Axis other) =>
        this.Equals(other);

    public override bool Equals(object? obj) =>
        obj is Axis rhs && this.Equals(rhs);

    public override int GetHashCode() =>
        (this.Origin.GetHashCode() * 397) ^
        this.Size.GetHashCode();

    public override string ToString() =>
        $"Axis: {this.Origin} ({this.Size})";

    public void Deconstruct(
        double origin,
        double size)
    {
        origin = this.Origin;
        size = this.Size;
    }

    public static implicit operator Axis((double origin, double size) axis) =>
        new Axis(axis.origin, axis.size);
}

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

    // <summary>
    // X
    // </summary>
    //public double X =>
    //    this.Axes[0].Origin;
    
    // <summary>
    // Y
    // </summary>
    //public double Y =>
    //    this.Axes[1].Origin;

    // <summary>
    // Range width
    // </summary>
    //public double Width =>
    //    this.Axes[0].Size;

    // <summary>
    // Range height
    // </summary>
    //public double Height =>
    //    this.Axes[1].Size;

    public Bound(double width, double height) =>
        this.Axes = [new Axis(0, width), new Axis(0, height)];

    public Bound(Point point, double width, double height) =>
        this.Axes = [new Axis(point.Elements[0], width), new Axis(point.Elements[1], height)];

    public Bound(double x, double y, double width, double height) =>
        this.Axes = [new Axis(x, width), new Axis(y, height)];

    public Bound(Axis[] axes) =>
        this.Axes = axes;

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

    public static Bound Create(double width, double height) =>
        new Bound(width, height);

    public static Bound Create(Point point, double width, double height) =>
        new Bound(point, width, height);

    public static Bound Create(double x, double y, double width, double height) =>
        new Bound(x, y, width, height);
}
