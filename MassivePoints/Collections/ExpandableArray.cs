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
/// Simple and efficient expandable array interface.
/// </summary>
/// <typeparam name="T">Value type</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IExpandableArray<T> : IReadOnlyArray<T>
{
    void Add(T value);
    void AddRange(IReadOnlyArray<T> values);
    void AddRange(IReadOnlyArray<T> values, int offset, int count);
    void AddRangePredicate(IReadOnlyArray<T> values, Func<T, bool> predicate);
    void AddRangePredicate(IReadOnlyArray<T> values, int offset, Func<T, bool> predicate);
    void RemoveAt(int index);
    void Clear();
}

/// <summary>
/// Simple and efficient expandable array interface.
/// </summary>
/// <typeparam name="T">Value type</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class ExpandableArray<T> : IExpandableArray<T>
{
    private T[] values;
    private int exactLength;

    public ExpandableArray(int capacity = 1024) =>
        this.values = new T[capacity];

    public T this[int index]
    {
        get
        {
            Debug.Assert(index < this.exactLength);
            return this.values[index];
        }
    }

    public int Count =>
        this.exactLength;

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < this.exactLength; index++)
        {
            yield return this.values[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        for (var index = 0; index < this.exactLength; index++)
        {
            yield return this.values[index];
        }
    }

    private void EnsureCapacity(int minimumRequired)
    {
        if (minimumRequired > this.values.Length)
        {
            var required = Math.Max(minimumRequired, this.values.Length * 2);
            var newValues = new T[required];
            Array.Copy(this.values, newValues, this.exactLength);
            this.values = newValues;
        }
    }

    public void Add(T value)
    {
        if (this.exactLength >= this.values.Length)
        {
            this.EnsureCapacity(this.exactLength + 1024);
        }
        this.values[this.exactLength++] = value;
    }

    public void AddRange(IReadOnlyArray<T> values)
    {
        var remains = this.values.Length - this.exactLength;
        if (values.Count > remains)
        {
            this.EnsureCapacity(this.exactLength + values.Count + 1024);
        }
        values.CopyTo(0, this.values, this.exactLength, values.Count);
        this.exactLength += values.Count;
    }

    public void AddRange(IReadOnlyArray<T> values, int offset, int count)
    {
        var remains = this.values.Length - this.exactLength;
        if (count > remains)
        {
            this.EnsureCapacity(this.exactLength + count + 1024);
        }
        values.CopyTo(offset, this.values, this.exactLength, count);
        this.exactLength += count;
    }

    public void AddRangePredicate(IReadOnlyArray<T> values, int offset, Func<T, bool> predicate)
    {
        for (var index = offset; index < values.Count; index++)
        {
            var item = values[index];
            if (predicate(item))
            {
                if (this.exactLength >= this.values.Length)
                {
                    this.EnsureCapacity(this.exactLength + 1024);
                }
                this.values[this.exactLength] = item;
                this.exactLength++;
            }
        }
    }

    public void AddRangePredicate(IReadOnlyArray<T> values, Func<T, bool> predicate)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var item = values[index];
            if (predicate(item))
            {
                if (this.exactLength >= this.values.Length)
                {
                    this.EnsureCapacity(this.exactLength + 1024);
                }
                this.values[this.exactLength] = item;
                this.exactLength++;
            }
        }
    }

    public void RemoveAt(int index)
    {
        Debug.Assert(index < this.exactLength);
        Array.Copy(this.values, index + 1, this.values, index, this.exactLength - index - 1);
        this.exactLength--;
    }

    public void Clear() =>
        this.exactLength = 0;

    public void CopyTo(int index, T[] array, int toIndex, int count) =>
        Array.Copy(this.values, index, array, toIndex, count);

    public T[] AsArray()
    {
        if (this.values.Length == this.exactLength)
        {
            return this.values;
        }
        else
        {
            var array = new T[this.exactLength];
            Array.Copy(this.values, array, array.Length);
            return array;
        }
    }
}
