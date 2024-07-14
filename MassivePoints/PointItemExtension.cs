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
        if (self.Point.Elements is [var sx, var sy])
        {
            x = self.Point.Elements[0];
            y = self.Point.Elements[1];
            value = self.Value;
        }
        else
        {
            x = double.NaN;
            y = double.NaN;
            value = default!;
        }
    }
}
