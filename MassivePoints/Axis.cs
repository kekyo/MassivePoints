////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;

namespace MassivePoints;

/// <summary>
/// The axis definition.
/// </summary>
public readonly struct Axis : IEquatable<Axis>
{
    /// <summary>
    /// Axis origin point.
    /// </summary>
    public readonly double Origin;
    
    /// <summary>
    /// Axis size.
    /// </summary>
    public readonly double Size;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="origin">Axis origin point</param>
    /// <param name="size">Axis size</param>
    public Axis(double origin, double size)
    {
        this.Origin = origin;
        this.Size = size;
    }

    public bool Equals(Axis other) =>
        this.Origin == other.Origin &&
        this.Size == other.Size;

    bool IEquatable<Axis>.Equals(Axis other) =>
        this.Equals(other);

    public override bool Equals(object? obj) =>
        obj is Axis rhs && this.Equals(rhs);

    public override int GetHashCode() =>
        (this.Origin.GetHashCode() * 397) ^
        this.Size.GetHashCode();

    public override string ToString() =>
        $"Axis: {this.Origin} ({this.Size})";

    public static implicit operator Axis((double origin, double size) axis) =>
        new Axis(axis.origin, axis.size);
}

public static class AxisExtension
{
    public static void Deconstruct(
        this Axis self,
        double origin,
        double size)
    {
        origin = self.Origin;
        size = self.Size;
    }
}
