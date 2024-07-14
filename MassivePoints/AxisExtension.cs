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
