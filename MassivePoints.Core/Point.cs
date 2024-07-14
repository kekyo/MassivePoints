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
    /// <param name="x">X axis point</param>
    /// <param name="y">Y axis point</param>
    /// <remarks>This constructor will create 2D coordinate point.</remarks>
    public Point(double x, double y) =>
        this.Elements = [x, y];

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">X axis point</param>
    /// <param name="y">Y axis point</param>
    /// <param name="z">Z axis point</param>
    /// <remarks>This constructor will create 3D coordinate point.</remarks>
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
    
    /// <summary>
    /// X point.
    /// </summary>
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

    /// <summary>
    /// Create a 2D coordinate point.
    /// </summary>
    /// <param name="x">X axis point</param>
    /// <param name="y">Y axis point</param>
    /// <remarks>This constructor will create 2D point.</remarks>
    public static Point Create(double x, double y) =>
        new Point(x, y);

    /// <summary>
    /// Create a 3D coordinate point.
    /// </summary>
    /// <param name="x">X axis point</param>
    /// <param name="y">Y axis point</param>
    /// <param name="z">Z axis point</param>
    /// <remarks>This constructor will create 3D point.</remarks>
    public static Point Create(double x, double y, double z) =>
        new Point(x, y, z);
}
