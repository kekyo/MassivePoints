////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace MassivePoints;

public readonly struct PointItem<TValue> : IEquatable<PointItem<TValue>>
{
    public Point Point { get; }
    public TValue Value { get; }

    public double X =>
        this.Point.X;
    public double Y =>
        this.Point.Y;

    public PointItem(Point point, TValue value)
    {
        this.Point = point;
        this.Value = value;
    }

    public PointItem(double x, double y, TValue value)
    {
        this.Point = new(x, y);
        this.Value = value;
    }

    public PointItem(KeyValuePair<Point, TValue> pointItem)
    {
        this.Point = pointItem.Key;
        this.Value = pointItem.Value;
    }

    public bool Equals(PointItem<TValue> rhs) =>
        this.Point.Equals(rhs.Point) &&
        (this.Value?.Equals(rhs.Value) ?? ((object?)rhs) == null);

    public override bool Equals(object? obj) =>
        obj is PointItem<TValue> rhs && this.Equals(rhs);

    public override int GetHashCode()
    {
        unchecked
        {
            return (this.Point.GetHashCode() * 397) ^
                   (this.Value?.GetHashCode() ?? 0);
        }
    }

    public override string ToString() =>
        $"{this.Point}: {this.Value}";

    public static implicit operator PointItem<TValue>((double x, double y, TValue value) pointItem) =>
        new(new(pointItem.x, pointItem.y), pointItem.value);

    public static implicit operator PointItem<TValue>((Point point, TValue value) pointItem) =>
        new(pointItem.point, pointItem.value);

    public static implicit operator PointItem<TValue>(KeyValuePair<Point, TValue> pointItem) =>
        new(pointItem.Key, pointItem.Value);
}

public static class PointItem
{
    public static PointItem<TValue> Create<TValue>(Point point, TValue value) =>
        new(point, value);

    public static PointItem<TValue> Create<TValue>(double x, double y, TValue value) =>
        new(x, y, value);

    public static PointItem<TValue> Create<TValue>(KeyValuePair<Point, TValue> pointItem) =>
        new(pointItem.Key, pointItem.Value);

}

public static class PointItemExtension
{
    public static void Deconstruct<TValue>(
        this PointItem<TValue> self,
        out Point point,
        out TValue value)
    {
        point = self.Point;
        value = self.Value;
    }

    public static void Deconstruct<TValue>(
        this PointItem<TValue> self,
        out double x,
        out double y,
        out TValue value)
    {
        x = self.Point.X;
        y = self.Point.Y;
        value = self.Value;
    }
}
