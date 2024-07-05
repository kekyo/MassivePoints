////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;

namespace MassivePoints;

/// <summary>
/// The coordinate point definition.
/// </summary>
public readonly struct Point : IEquatable<Point>
{
    /// <summary>
    /// Point elements.
    /// </summary>
    public readonly double[] Elements;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">X coordinate point.</param>
    /// <param name="y">Y coordinate point.</param>
    /// <remarks>This constructor will create 2D point.</remarks>
    public Point(double x, double y) =>
        this.Elements = [x, y];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">X coordinate point.</param>
    /// <param name="y">Y coordinate point.</param>
    /// <param name="z">Z coordinate point.</param>
    /// <remarks>This constructor will create 3D point.</remarks>
    public Point(double x, double y, double z) =>
        this.Elements = [x, y, z];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="elements">Elements</param>
    public Point(params double[] elements) =>
        this.Elements = elements;

    /// <summary>
    /// X point.
    /// </summary>
    public double X =>
        this.Elements[0];
    
    public double Y =>
        this.Elements[1];

    public bool Equals(Point rhs)
    {
        if (this.Elements.Length != rhs.Elements.Length)
        {
            return false;
        }
        for (var index = 0; index < this.Elements.Length; index++)
        {
            if (this.Elements[index] != rhs.Elements[index])
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) =>
        obj is Point rhs && this.Equals(rhs);

    public override int GetHashCode()
    {
        unchecked
        {
            var sum = 0;
            foreach (var element in this.Elements)
            {
                sum ^= element.GetHashCode() * 397;
            }
            return sum;
        }
    }

    public override string ToString() =>
        $"[{string.Join(",", this.Elements)}]";

    public static implicit operator Point((double x, double y) point) =>
        new Point(point.x, point.y);

    public static implicit operator Point((double x, double y, double z) point) =>
        new Point(point.x, point.y, point.z);

    public static Point Create(double x, double y) =>
        new Point(x, y);

    public static Point Create(double x, double y, double z) =>
        new Point(x, y, z);
}

public static class PointExtension
{
    public static void Deconstruct(
        this Point self,
        out double[] elements) =>
        elements = self.Elements;

    public static void Deconstruct(
        this Point self,
        out double x,
        out double y)
    {
        if (self.Elements is [var sx, var sy])
        {
            x = sx;
            y = sy;
        }
        else
        {
            x = double.NaN;
            y = double.NaN;
        }
    }

    public static void Deconstruct(
        this Point self,
        out double x,
        out double y,
        out double z)
    {
        if (self.Elements is [var sx, var sy,var sz])
        {
            x = sx;
            y = sy;
            z = sz;
        }
        else
        {
            x = double.NaN;
            y = double.NaN;
            z = double.NaN;
        }
    }
}
