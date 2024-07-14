////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;

namespace MassivePoints.Internal;

internal static class InternalBound
{
    private static readonly object locker = new();
    private static int[] sizes = [1, 2, 4, 8, 16];

    /// <summary>
    /// Get child bound count.
    /// </summary>
    /// <param name="dimension">Target dimension</param>
    /// <returns>Child bound count.</returns>
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

    /// <summary>
    /// Get child bound count.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <returns>Child bound count</returns>
    public static int GetChildBoundCount(
        Bound self) =>
        GetChildBoundCount(self.Axes.Length);

    /// <summary>
    /// Get dimension axis count.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <returns>Dimension axis count</returns>
    public static int GetDimensionAxisCount(
        Bound self) =>
        self.Axes.Length;

    /// <summary>
    /// Get child bounds.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <returns>Child bounds</returns>
    public static Bound[] GetChildBounds(
        Bound self)
    {
        var childBounds = new Bound[GetChildBoundCount(self.Axes.Length)];
        
        for (var childIndex = 0; childIndex < childBounds.Length; childIndex++)
        {
            var axes = new Axis[self.Axes.Length];
            var halfBits = childIndex;
            
            for (var axisIndex = 0; axisIndex < axes.Length; axisIndex++, halfBits >>= 1)
            {
                var axis = self.Axes[axisIndex];
                var halfSizeHint = axis.SizeHint / 2;
                var halfOrigin = axis.Origin + halfSizeHint;
                if ((halfBits & 0x01) == 0x01)
                {
                    axes[axisIndex] = new Axis(
                        halfOrigin,
                        axis.To);
                }
                else
                {
                    axes[axisIndex] = new Axis(
                        axis.Origin,
                        halfOrigin);
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
        Bound self, Point point)
    {
        if (self.Axes.Length != point.Elements.Length)
        {
            return false;
        }

        for (var index = 0; index < self.Axes.Length; index++)
        {
            var l = self.Axes[index];
            var r = point.Elements[index];
            
            if (!(l.Origin <= r && r < l.To))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the specified range is intersects this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="bound">Coordinate range</param>
    /// <returns>True when intersected.</returns>
    public static bool IsIntersection(
        Bound self, Bound bound)
    {
        if (self.Axes.Length != bound.Axes.Length)
        {
            return false;
        }

        for (var index = 0; index < self.Axes.Length; index++)
        {
            var l = self.Axes[index];
            var r = bound.Axes[index];
            
            if (l.Origin > r.To || r.Origin >= l.To)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Add an axis.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="axis">Axis</param>
    /// <returns>New bound definition</returns>
    public static Bound AddAxis(
        Bound self, Axis axis) =>
        new Bound([..self.Axes, axis]);
}
