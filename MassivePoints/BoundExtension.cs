////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;

// Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable CS1573

namespace MassivePoints;

public readonly struct ChildBound
{
    /// <summary>
    /// Child coordination range.
    /// </summary>
    public readonly Bound Bound;
    
    /// <summary>
    /// Is this bound terminal? (end of bound on each axis)
    /// </summary>
    public readonly bool[] IsTerminals;

    public ChildBound(Bound bound, bool[] isTerminals)
    {
        this.Bound = bound;
        this.IsTerminals = isTerminals;
    }
}

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

    public static void Deconstruct(
        this ChildBound self,
        out Bound bound,
        out bool[] isTerminals)
    {
        bound = self.Bound;
        isTerminals = self.IsTerminals;
    }

    /// <summary>
    /// Get dimension axis count.
    /// </summary>
    /// <returns>Dimension axis count</returns>
    public static int GetDimensionAxisCount(
        this Bound self) =>
        self.Axes.Length;

    /// <summary>
    /// Get child bound count.
    /// </summary>
    /// <returns>Child bound count</returns>
    public static int GetChildBoundCount(
        this Bound self) =>
        Bound.GetChildBoundCount(self.Axes.Length);

    /// <summary>
    /// Get child bounds.
    /// </summary>
    /// <returns>Child bounds</returns>
    public static ChildBound[] GetChildBounds(
        this Bound self)
    {
        var childBounds = new ChildBound[Bound.GetChildBoundCount(self.Axes.Length)];
        
        for (var childIndex = 0; childIndex < childBounds.Length; childIndex++)
        {
            var axes = new Axis[self.Axes.Length];
            var isTerminals = new bool[self.Axes.Length];
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

                var terminalMask = 0x01 << axisIndex;
                isTerminals[axisIndex] = (childIndex & terminalMask) != 0;
            }
            
            childBounds[childIndex] = new ChildBound(new(axes), isTerminals);
        }

        return childBounds;
    }

    public static bool[] IsTerminalsAnd(
        this ChildBound childBound, bool rhs)
    {
        var isTerminals = new bool[childBound.IsTerminals.Length];
        for (var index = 0; index < isTerminals.Length; index++)
        {
            isTerminals[index] = childBound.IsTerminals[index] && rhs;
        }
        return isTerminals;
    }

    public static bool[] IsTerminalsAnd(
        this ChildBound childBound, bool[] rhs)
    {
        if (childBound.IsTerminals.Length != rhs.Length)
        {
            throw new ArgumentException();
        }
        
        var isTerminals = new bool[childBound.IsTerminals.Length];
        for (var index = 0; index < isTerminals.Length; index++)
        {
            isTerminals[index] = childBound.IsTerminals[index] && rhs[index];
        }
        return isTerminals;
    }

    /// <summary>
    /// Checks whether the specified coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="point">A coordinate point</param>
    /// <param name="isRightClosed">Perform right-closed interval on coordinate range</param>
    /// <returns>True when within.</returns>
    public static bool IsWithin(
        this Bound self, Point point, bool isRightClosed)
    {
        if (self.Axes.Length != point.Elements.Length)
        {
            return false;
        }

        if (isRightClosed)
        {
            for (var index = 0; index < self.Axes.Length; index++)
            {
                var l = self.Axes[index];
                var r = point.Elements[index];
            
                if (l.Origin > r || r > l.To)
                {
                    return false;
                }
            }
        }
        else
        {
            for (var index = 0; index < self.Axes.Length; index++)
            {
                var l = self.Axes[index];
                var r = point.Elements[index];
            
                if (l.Origin > r || r >= l.To)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the specified 2D coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>True is within.</returns>
    public static bool IsWithin(
        this Bound self, double x, double y, bool isRightClosed) =>
        IsWithin(self, new Point(x, y), isRightClosed);

    /// <summary>
    /// Checks whether the specified 3D coordinate point is within this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <param name="z">Z</param>
    /// <returns>True is within.</returns>
    public static bool IsWithin(
        this Bound self, double x, double y, double z, bool isRightClosed) =>
        IsWithin(self, new Point(x, y, z), isRightClosed);

    /// <summary>
    /// Checks whether the specified range intersects this range.
    /// </summary>
    /// <param name="self">`Bound`</param>
    /// <param name="bound">Coordinate range</param>
    /// <param name="isRightClosedEachAxis">Perform right-closed interval on coordinate range</param>
    /// <returns>True when intersected.</returns>
    public static bool IsIntersection(
        this Bound self, Bound bound, bool[]? isRightClosedEachAxis = null)
    {
        if (self.Axes.Length != bound.Axes.Length)
        {
            return false;
        }

        if (isRightClosedEachAxis != null)
        {
            if (self.Axes.Length != isRightClosedEachAxis.Length)
            {
                throw new ArgumentException("Could not interpret different dimension bound.");
            }
            
            for (var index = 0; index < self.Axes.Length; index++)
            {
                var l = self.Axes[index];
                var r = bound.Axes[index];
            
                if (l.Origin > r.To)
                {
                    return false;
                }
                
                if (isRightClosedEachAxis[index])
                {
                    if (r.Origin > l.To)
                    {
                        return false;
                    }
                }
                else
                {
                    if (r.Origin >= l.To)
                    {
                        return false;
                    }
                }
            }
        }
        else
        {
            for (var index = 0; index < self.Axes.Length; index++)
            {
                var l = self.Axes[index];
                var r = bound.Axes[index];
            
                if (l.Origin > r.To || r.Origin >= l.To)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Add an axis.
    /// </summary>
    /// <param name="axis">Axis</param>
    /// <returns>New bound definition</returns>
    public static Bound AddAxis(
        this Bound self, Axis axis) =>
        new Bound([..self.Axes, axis]);
}
