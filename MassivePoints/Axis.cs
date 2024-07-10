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

/// <summary>
/// The axis definition.
/// </summary>
public readonly struct Axis : IEquatable<Axis>
{
    /// <summary>
    /// Minimum valid size.
    /// </summary>
    public static readonly double MinSize;

    static Axis()
    {
        // Calculate machine epsilon
        var epsilon = 1.0;
        while (1.0 + epsilon / 2.0 != 1.0)
        {
            epsilon /= 2.0;
        }

        MinSize = epsilon * 2;
    }
    
    /// <summary>
    /// Axis origin point.
    /// </summary>
    public readonly double Origin;
        
    /// <summary>
    /// Axis to point (exclusive, right-opened).
    /// </summary>
    public readonly double To;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="origin">Axis origin point</param>
    /// <param name="to">Axis to point (exclusive, right-opened)</param>
    public Axis(double origin, double to)
    {
        this.Origin = origin;
        this.To = to;
    }

    /// <summary>
    /// Axis size (hint).
    /// </summary>
    public double SizeHint =>
        this.To - this.Origin;

    /// <summary>
    /// Is size empty.
    /// </summary>
    public bool IsEmpty =>
        this.SizeHint < MinSize;

    public bool Equals(Axis other) =>
        this.Origin == other.Origin &&
        this.To == other.To;

    bool IEquatable<Axis>.Equals(Axis other) =>
        this.Equals(other);

    public override bool Equals(object? obj) =>
        obj is Axis rhs && this.Equals(rhs);

    public override int GetHashCode() =>
        (this.Origin.GetHashCode() * 397) ^
        this.To.GetHashCode();

    public override string ToString() =>
        $"[{this.Origin} - {this.To})";

    public static implicit operator Axis((double origin, double to) axis) =>
        new Axis(axis.origin, axis.to);
}

public static class AxisExtension
{
    public static void Deconstruct(
        this Axis self,
        out double origin,
        out double to)
    {
        origin = self.Origin;
        to = self.To;
    }
}
