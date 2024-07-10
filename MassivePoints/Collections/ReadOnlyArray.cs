////////////////////////////////////////////////////////////////////////////
//
// MassivePoints - .NET implementation of QuadTree.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace MassivePoints.Collections;

/// <summary>
/// Simple and efficient read only array interface.
/// </summary>
/// <typeparam name="T">Value type</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IReadOnlyArray<T> : IReadOnlyList<T>
{
    void CopyTo(int index, T[] array, int toIndex, int count);
    T[] AsArray();
}

/// <summary>
/// Simple and efficient read only array implementation.
/// </summary>
/// <typeparam name="T">Value type</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class ReadOnlyArray<T> : IReadOnlyArray<T>
{
    private readonly IReadOnlyList<T> values;

    public ReadOnlyArray(IReadOnlyList<T> values) =>
        this.values = values;

    public T this[int index] =>
        this.values[index];

    public int Count =>
        this.values.Count;

    public void CopyTo(int index, T[] array, int toIndex, int count)
    {
        if (this.values is T[] values)
        {
            Array.Copy(values, index, array, toIndex, count);
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                array[toIndex] = this.values[index];
                index++;
                toIndex++;
            }
        }
    }

    public IEnumerator<T> GetEnumerator() =>
        this.values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        this.values.GetEnumerator();

    public T[] AsArray()
    {
        if (this.values is T[] array)
        {
            return array;
        }
        else
        {
            array = new T[this.values.Count];
            for (var index = 0; index< array.Length; index++)
            {
                array[index] = this.values[index];
            }
            return array;
        }
    }

    public static readonly ReadOnlyArray<T> Empty = new(Array.Empty<T>());
}
