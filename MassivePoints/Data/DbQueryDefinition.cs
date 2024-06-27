////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

namespace MassivePoints.Data;

internal readonly struct DbQueryDefinition
{
    public readonly string CommandText;
    public readonly string[] ParameterNames;

    public DbQueryDefinition(
        string commandText, params string[] parameterNames)
    {
        this.CommandText = commandText;
        this.ParameterNames = parameterNames;
    }

    public override string ToString() =>
        $"Query: {this.CommandText}, Parameters={this.ParameterNames.Length}";
}
