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
    /// <summary>
    /// Point.
    /// </summary>
    public readonly Point Point;
    
    /// <summary>
    /// Value.
    /// </summary>
    public readonly TValue Value;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="point">Point</param>
    /// <param name="value">Value</param>
    public PointItem(Point point, TValue value)
    {
        this.Point = point;
        this.Value = value;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">Point X</param>
    /// <param name="y">Point Y</param>
    /// <param name="value">Value</param>
    /// <remarks>This constructor will create 2D point.
    public PointItem(double x, double y, TValue value)
    {
        this.Point = new(x, y);
        this.Value = value;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">Point X</param>
    /// <param name="y">Point Y</param>
    /// <param name="z">Point Z</param>
    /// <param name="value">Value</param>
    /// <remarks>This constructor will create 3D point.
    public PointItem(double x, double y, double z, TValue value)
    {
        this.Point = new(x, y, z);
        this.Value = value;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="pointItem">Point with value</param>
    public PointItem(KeyValuePair<Point, TValue> pointItem)
    {
        this.Point = pointItem.Key;
        this.Value = pointItem.Value;
    }

    /// <summary>
    /// Point X.
    /// </summary>
    public double X =>
        this.Point.Elements is [var x,..] ? x : double.NaN;
    
    /// <summary>
    /// Point Y.
    /// </summary>
    public double Y =>
        this.Point.Elements is [_,var y,..] ? y : double.NaN;
    
    /// <summary>
    /// Point Z.
    /// </summary>
    public double Z =>
        this.Point.Elements is [_,_,var z,..] ? z : double.NaN;

    public bool Equals(PointItem<TValue> rhs) =>
        this.Point.Equals(rhs.Point) &&
        (this.Value?.Equals(rhs.Value) ?? ((object?)rhs) == null);

    bool IEquatable<PointItem<TValue>>.Equals(PointItem<TValue> rhs) =>
        this.Equals(rhs);

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

    public static implicit operator PointItem<TValue>((double x, double y, double z, TValue value) pointItem) =>
        new(new(pointItem.x, pointItem.y, pointItem.z), pointItem.value);

    public static implicit operator PointItem<TValue>((Point point, TValue value) pointItem) =>
        new(pointItem.point, pointItem.value);

    public static implicit operator PointItem<TValue>(KeyValuePair<Point, TValue> pointItem) =>
        new(pointItem.Key, pointItem.Value);
}

public static class PointItem
{
    /// <summary>
    /// Create a point with a value.
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    /// <param name="point">Point</param>
    /// <param name="value">Value</param>
    /// <returns>`PointItem`</returns>
    public static PointItem<TValue> Create<TValue>(Point point, TValue value) =>
        new PointItem<TValue>(point, value);

    /// <summary>
    /// Create a point with a value.
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    /// <param name="x">Point X</param>
    /// <param name="y">Point Y</param>
    /// <param name="value">Value</param>
    /// <returns>`PointItem`</returns>
    /// <remarks>This constructor will create 2D point.
    public static PointItem<TValue> Create<TValue>(double x, double y, TValue value) =>
        new PointItem<TValue>(x, y, value);

    /// <summary>
    /// Create a point with a value.
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    /// <param name="x">Point X</param>
    /// <param name="y">Point Y</param>
    /// <param name="z">Point Z</param>
    /// <param name="value">Value</param>
    /// <returns>`PointItem`</returns>
    /// <remarks>This constructor will create 3D point.
    public static PointItem<TValue> Create<TValue>(double x, double y, double z, TValue value) =>
        new PointItem<TValue>(x, y, z, value);

    /// <summary>
    /// Create a point with a value.
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    /// <param name="pointItem">Point with value</param>
    /// <returns>`PointItem`</returns>
    public static PointItem<TValue> Create<TValue>(KeyValuePair<Point, TValue> pointItem) =>
        new PointItem<TValue>(pointItem.Key, pointItem.Value);
}
