////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using MassivePoints.Internal;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints;

public static class BoundExtension
{
    public static void Deconstruct(
        this Bound self,
        out Axis[] axes) =>
        axes = self.Axes;

    public static void Deconstruct(
        this Bound self,
        out Point origin,
        out Point to)
    {
        if (self.Axes is [var x, var y])
        {
            origin = new(x.Origin, y.Origin);
            to = new(x.To, y.To);
        }
        else
        {
            origin = new(double.NaN, double.NaN);
            to = new(double.NaN, double.NaN);
        }
    }

    public static void Deconstruct(
        this Bound self,
        out double x0,
        out double y0,
        out double x1,
        out double y1)
    {
        if (self.Axes is [var sx, var sy])
        {
            x0 = sx.Origin;
            y0 = sy.Origin;
            x1 = sx.To;
            y1 = sy.To;
        }
        else
        {
            x0 = double.NaN;
            y0 = double.NaN;
            x1 = double.NaN;
            y1 = double.NaN;
        }
    }

    /// <summary>
    /// Get dimension axis count.
    /// </summary>
    /// <returns>Dimension axis count</returns>
    public static int GetDimensionAxisCount(
        this Bound self) =>
        InternalBound.GetDimensionAxisCount(self);

    /// <summary>
    /// Get child bound count.
    /// </summary>
    /// <returns>Child bound count</returns>
    public static int GetChildBoundCount(
        this Bound self) =>
        InternalBound.GetChildBoundCount(self.Axes.Length);

    /// <summary>
    /// Get child bounds.
    /// </summary>
    /// <returns>Child bounds</returns>
    public static Bound[] GetChildBounds(
        this Bound self) =>
        InternalBound.GetChildBounds(self);

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="point">A coordinate point</param>
    /// <returns>True when within.</returns>
    public static bool IsWithin(
        this Bound self, Point point) =>
        InternalBound.IsWithin(self, point);

    /// <summary>
    /// Checks whether the specified 2D coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>True is within.</returns>
    public static bool IsWithin(
        this Bound self, double x, double y) =>
        InternalBound.IsWithin(self, new Point(x, y));

    /// <summary>
    /// Checks whether the specified 3D coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <param name="z">Z</param>
    /// <returns>True is within.</returns>
    public static bool IsWithin(
        this Bound self, double x, double y, double z) =>
        InternalBound.IsWithin(self, new Point(x, y, z));

    /// <summary>
    /// Checks whether the specified range is intersects this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="bound">Coordinate range</param>
    /// <returns>True when intersected.</returns>
    public static bool IsIntersection(
        this Bound self, Bound bound) =>
        InternalBound.IsIntersection(self, bound);

    /// <summary>
    /// Add an axis.
    /// </summary>
    /// <param name="axis">Axis</param>
    /// <returns>New bound definition</returns>
    public static Bound AddAxis(
        this Bound self, Axis axis) =>
        InternalBound.AddAxis(self, axis);
}
