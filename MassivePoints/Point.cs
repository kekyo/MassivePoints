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

public readonly struct Point : IEquatable<Point>
{
    private readonly double[] elements;

    public double X =>
        this.elements[0];
    public double Y =>
        this.elements[1];
    public double[] Elements =>
        this.elements;

    public Point(double x, double y) =>
        this.elements = [ x, y ];

    public bool Equals(Point rhs)
    {
        if (this.elements.Length != rhs.elements.Length)
        {
            return false;
        }
        for (var index = 0; index < this.elements.Length; index++)
        {
            if (this.elements[index] != rhs.elements[index])
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
            foreach (var element in this.elements)
            {
                sum ^= element.GetHashCode() * 397;
            }
            return sum;
        }
    }

    public override string ToString() =>
        $"[{string.Join(",", this.elements)}]";

    public static implicit operator Point((double x, double y) point) =>
        new(point.x, point.y);

    public static Point Create(double x, double y) =>
        new(x, y);
}

public static class PointExtension
{
    public static void Deconstruct(
        this Point self,
        out double x,
        out double y)
    {
        x = self.X;
        y = self.Y;
    }
}
