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

public static class BoundExtension
{
    public static void Deconstruct(
        this Bound self,
        out Axis[] axes) =>
        axes = self.Axes;

    public static void Deconstruct(
        this Bound self,
        out Point origin,
        out double width,
        out double height)
    {
        origin = new(self.X, self.Y);
        width = self.Width;
        height = self.Height;
    }

    public static void Deconstruct(
        this Bound self,
        out double x,
        out double y,
        out double width,
        out double height)
    {
        x = self.X;
        y = self.Y;
        width = self.Width;
        height = self.Height;
    }

    public static Bound[] GetChildBounds(
        this Bound self)
    {
        var childBounds = new Bound[Bound.GetChildBoundCount(self.Axes.Length)];
        
        for (var childIndex = 0; childIndex < childBounds.Length; childIndex++)
        {
            var axes = new Axis[self.Axes.Length];
            var halfBits = childIndex;
            
            for (var axisIndex = 0; axisIndex < axes.Length; axisIndex++, halfBits >>= 1)
            {
                var halfSize = self.Axes[axisIndex].Size / 2;
                if ((halfBits & 0x01) == 0x01)
                {
                    axes[axisIndex] = new Axis(
                        self.Axes[axisIndex].Origin + halfSize,
                        halfSize);
                }
                else
                {
                    axes[axisIndex] = new Axis(
                        self.Axes[axisIndex].Origin,
                        halfSize);
                }
            }
            
            childBounds[childIndex] = new Bound(axes);
        }

        return childBounds;
    }

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="point">A coordinate point</param>
    /// <returns>True when within.</returns>
    public static bool IsWithin(
        this Bound self, Point point)
    {
        if (self.Axes.Length != point.Elements.Length)
        {
            throw new ArgumentException(
                $"Could not compare difference dimension: {self.Axes.Length} != {point.Elements.Length}");
        }

        for (var index = 0; index < self.Axes.Length; index++)
        {
            var l = self.Axes[index];
            var r = point.Elements[index];
            
            if (!(l.Origin <= r && r < (l.Origin + l.Size)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>True is within.</returns>
    public static bool IsWithin(
        this Bound self, double x, double y) =>
        IsWithin(self, new Point(x, y));

    /// <summary>
    /// Checks whether the specified range is intersects this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="bound">Coordinate range</param>
    /// <returns>True when intersected.</returns>
    public static bool IsIntersection(
        this Bound self, Bound bound)
    {
        if (self.Axes.Length != bound.Axes.Length)
        {
            throw new ArgumentException(
                $"Could not compare difference dimension: {self.Axes.Length} != {bound.Axes.Length}");
        }

        for (var index = 0; index < self.Axes.Length; index++)
        {
            var l = self.Axes[index];
            var r = bound.Axes[index];
            
            if (l.Origin > (r.Origin + r.Size) ||
                r.Origin > (l.Origin + l.Size))
            {
                return false;
            }
        }

        return true;
    }
}
