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

public static class PointExtension
{
    public static void Deconstruct(
        this Point self,
        out double[] elements) =>
        elements = self.Elements;

    public static void Deconstruct(
        this Point self,
        out double x,
        out double y)
    {
        if (self.Elements is [var sx, var sy])
        {
            x = sx;
            y = sy;
        }
        else
        {
            x = double.NaN;
            y = double.NaN;
        }
    }

    public static void Deconstruct(
        this Point self,
        out double x,
        out double y,
        out double z)
    {
        if (self.Elements is [var sx, var sy,var sz])
        {
            x = sx;
            y = sy;
            z = sz;
        }
        else
        {
            x = double.NaN;
            y = double.NaN;
            z = double.NaN;
        }
    }
}
