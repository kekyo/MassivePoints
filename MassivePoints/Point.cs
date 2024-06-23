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
    public double X { get; }
    public double Y { get; }

    public Point(double x, double y)
    {
        this.X = x;
        this.Y = y;
    }

    public bool Equals(Point rhs) =>
        this.X == rhs.X && this.Y == rhs.Y;

    public override bool Equals(object? obj) =>
        obj is Point rhs && this.Equals(rhs);

    public override int GetHashCode()
    {
        unchecked
        {
            return (this.X.GetHashCode() * 397) ^
                   this.Y.GetHashCode();
        }
    }

    public void Deconstruct(
        out double x,
        out double y)
    {
        x = this.X;
        y = this.Y;
    }

    public override string ToString() =>
        $"[{this.X},{this.Y}]";

    public static implicit operator Point((double x, double y) point) =>
        new(point.x, point.y);
}
