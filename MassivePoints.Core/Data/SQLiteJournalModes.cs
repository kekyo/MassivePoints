////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

namespace MassivePoints.Data;

/// <summary>
/// SQLite journal modes.
/// </summary>
public enum SQLiteJournalModes
{
    Delete,
    Truncate,
    Persist,
    Memory,
    Wal,
    Off,
}
