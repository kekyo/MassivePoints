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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS1998

namespace MassivePoints.Internal;

internal static class Utilities
{
    private sealed class AsyncEmptyEnumerator<T> : IAsyncEnumerator<T>
    {
        public ValueTask DisposeAsync() =>
            default;

        public ValueTask<bool> MoveNextAsync() =>
            new(false);

        public T Current =>
            throw new InvalidOperationException();

        public static readonly AsyncEmptyEnumerator<T> Instance = new();
    }
    
    private sealed class AsyncEmptyEnumerable<T> : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(
            CancellationToken cancellationToken) =>
            AsyncEmptyEnumerator<T>.Instance;

        public static readonly AsyncEmptyEnumerable<T> Instance = new();
    }

    public static IAsyncEnumerable<T> AsyncEmpty<T>() =>
        AsyncEmptyEnumerable<T>.Instance;
    
    public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(
        this IEnumerable<T> enumerable,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var item in enumerable)
        {
            yield return item;
        }
    }

    public static async IAsyncEnumerable<T> Concat<T>(
        this IAsyncEnumerable<T> enumerable,
        IAsyncEnumerable<T> second,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in enumerable.WithCancellation(ct))
        {
            yield return item;
        }
        await foreach (var item in second.WithCancellation(ct))
        {
            yield return item;
        }
    }
}
