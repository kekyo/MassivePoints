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
    public readonly double[] Elements;

    public Point(double[] elements) =>
        this.Elements = elements;

    public Point(double x, double y) =>
        this.Elements = [x, y];

    //public double X =>
    //    this.Elements[0];
    //public double Y =>
    //    this.Elements[1];

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
        new(point.x, point.y);

    public static Point Create(double x, double y) =>
        new Point(x, y);
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
        x = self.Elements[0];
        y = self.Elements[1];
    }
}
