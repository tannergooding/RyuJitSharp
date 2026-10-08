// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

internal sealed class JitStdVector<T> : IDisposable, IEnumerable<T>
{
    internal readonly struct Iterator : IJitStdIterator, IEquatable<Iterator>
    {
        internal readonly JitStdVector<T>? _owner;
        internal readonly nint _index;
        internal readonly int _generation;

        internal Iterator(JitStdVector<T> owner, nint index)
        {
            _owner = owner;
            _index = index;
            _generation = owner._generation;
        }

        internal ref T value
        {
            get
            {
                var owner = getOwner();
                owner.validate(_index, _generation, allowEnd: false);
                return ref owner._data[(int)_index];
            }
        }

        internal nint index => _index;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.RandomAccess;

        public static Iterator operator +(Iterator iterator, nint offset) => new Iterator(iterator.getOwner(), checked(iterator._index + offset));

        public static Iterator operator -(Iterator iterator, nint offset) => iterator + checked(-offset);

        public static nint operator -(Iterator first, Iterator last)
        {
            first.ensureCompatible(last);
            return first._index - last._index;
        }

        public static Iterator operator ++(Iterator iterator) => iterator + 1;

        public static Iterator operator --(Iterator iterator) => iterator - 1;

        public static bool operator ==(Iterator left, Iterator right) => left.Equals(right);

        public static bool operator !=(Iterator left, Iterator right) => !left.Equals(right);

        public static bool operator <(Iterator left, Iterator right)
        {
            left.ensureCompatible(right);
            return left._index < right._index;
        }

        public static bool operator >(Iterator left, Iterator right) => right < left;

        public static bool operator <=(Iterator left, Iterator right) => !(left > right);

        public static bool operator >=(Iterator left, Iterator right) => !(left < right);

        public bool Equals(Iterator other)
        {
            return ReferenceEquals(_owner, other._owner)
                && _index == other._index
                && _generation == other._generation;
        }

        public override bool Equals(object? obj)
        {
            return obj is Iterator other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_owner, _index, _generation);
        }

        private JitStdVector<T> getOwner()
        {
            return _owner ?? throw new InvalidOperationException("The iterator is not initialized.");
        }

        private void ensureCompatible(Iterator other)
        {
            if (!ReferenceEquals(_owner, other._owner) || _generation != other._generation)
            {
                throw new InvalidOperationException("Vector iterators must refer to the same allocation.");
            }

            var owner = getOwner();
            owner.validate(_index, _generation, allowEnd: true);
            owner.validate(other._index, other._generation, allowEnd: true);
        }
    }

    internal readonly struct ConstIterator : IJitStdIterator, IEquatable<ConstIterator>
    {
        private readonly Iterator _iterator;

        internal ConstIterator(Iterator iterator)
        {
            _iterator = iterator;
        }

        internal ref readonly T value => ref _iterator.value;

        internal nint index => _iterator.index;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.RandomAccess;

        public static ConstIterator operator +(ConstIterator iterator, nint offset) => new ConstIterator(iterator._iterator + offset);

        public static ConstIterator operator -(ConstIterator iterator, nint offset) => iterator + checked(-offset);

        public static nint operator -(ConstIterator first, ConstIterator last) => first._iterator - last._iterator;

        public static ConstIterator operator ++(ConstIterator iterator) => iterator + 1;

        public static ConstIterator operator --(ConstIterator iterator) => iterator - 1;

        public static bool operator ==(ConstIterator left, ConstIterator right) => left.Equals(right);

        public static bool operator !=(ConstIterator left, ConstIterator right) => !left.Equals(right);

        public static bool operator <(ConstIterator left, ConstIterator right) => left._iterator < right._iterator;

        public static bool operator >(ConstIterator left, ConstIterator right) => right < left;

        public static bool operator <=(ConstIterator left, ConstIterator right) => !(left > right);

        public static bool operator >=(ConstIterator left, ConstIterator right) => !(left < right);

        public bool Equals(ConstIterator other)
        {
            return _iterator.Equals(other._iterator);
        }

        public override bool Equals(object? obj)
        {
            return obj is ConstIterator other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _iterator.GetHashCode();
        }
    }

    internal readonly struct ReverseIterator : IJitStdIterator, IEquatable<ReverseIterator>
    {
        internal readonly Iterator _iterator;

        internal ReverseIterator(Iterator iterator)
        {
            _iterator = iterator;
        }

        internal ref T value => ref _iterator.value;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.RandomAccess;

        public static ReverseIterator operator +(ReverseIterator iterator, nint offset) => new(iterator._iterator - offset);

        public static ReverseIterator operator -(ReverseIterator iterator, nint offset) => iterator + checked(-offset);

        public static nint operator -(ReverseIterator first, ReverseIterator last) => last._iterator - first._iterator;

        public static ReverseIterator operator ++(ReverseIterator iterator) => iterator + 1;

        public static ReverseIterator operator --(ReverseIterator iterator) => iterator - 1;

        public static bool operator ==(ReverseIterator left, ReverseIterator right) => left.Equals(right);

        public static bool operator !=(ReverseIterator left, ReverseIterator right) => !left.Equals(right);

        public static bool operator <(ReverseIterator left, ReverseIterator right) => right._iterator < left._iterator;

        public static bool operator >(ReverseIterator left, ReverseIterator right) => right < left;

        public static bool operator <=(ReverseIterator left, ReverseIterator right) => !(left > right);

        public static bool operator >=(ReverseIterator left, ReverseIterator right) => !(left < right);

        public bool Equals(ReverseIterator other)
        {
            return _iterator.Equals(other._iterator);
        }

        public override bool Equals(object? obj)
        {
            return obj is ReverseIterator other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _iterator.GetHashCode();
        }
    }

    internal readonly struct ConstReverseIterator : IJitStdIterator, IEquatable<ConstReverseIterator>
    {
        private readonly ConstIterator _iterator;

        internal ConstReverseIterator(ConstIterator iterator)
        {
            _iterator = iterator;
        }

        internal ref readonly T value => ref _iterator.value;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.RandomAccess;

        public static ConstReverseIterator operator +(ConstReverseIterator iterator, nint offset) => new ConstReverseIterator(iterator._iterator - offset);

        public static ConstReverseIterator operator -(ConstReverseIterator iterator, nint offset) => iterator + checked(-offset);

        public static nint operator -(ConstReverseIterator first, ConstReverseIterator last) => last._iterator - first._iterator;

        public static ConstReverseIterator operator ++(ConstReverseIterator iterator) => iterator + 1;

        public static ConstReverseIterator operator --(ConstReverseIterator iterator) => iterator - 1;

        public static bool operator ==(ConstReverseIterator left, ConstReverseIterator right) => left.Equals(right);

        public static bool operator !=(ConstReverseIterator left, ConstReverseIterator right) => !left.Equals(right);

        public static bool operator <(ConstReverseIterator left, ConstReverseIterator right) => right._iterator < left._iterator;

        public static bool operator >(ConstReverseIterator left, ConstReverseIterator right) => right < left;

        public static bool operator <=(ConstReverseIterator left, ConstReverseIterator right) => !(left > right);

        public static bool operator >=(ConstReverseIterator left, ConstReverseIterator right) => !(left < right);

        public bool Equals(ConstReverseIterator other)
        {
            return _iterator.Equals(other._iterator);
        }

        public override bool Equals(object? obj)
        {
            return obj is ConstReverseIterator other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _iterator.GetHashCode();
        }
    }

    private JitStdAllocator<T> _allocator;
    private T[] _data = [];
    private nuint _size;
    private int _generation;
    private bool _isDisposed;

    internal JitStdVector(JitStdAllocator<T> allocator)
    {
        _allocator = allocator;
        _allocator.scope.throwIfDisposed();
    }

    internal JitStdVector(nuint count, T value, JitStdAllocator<T> allocator)
        : this(allocator)
    {
        resize(count, value);
    }

    internal JitStdVector(IEnumerable<T> values, JitStdAllocator<T> allocator)
        : this(allocator)
    {
        constructRange(values);
    }

    internal JitStdVector(JitStdVector<T> source)
        : this(source._allocator)
    {
        constructRange(source);
    }

    internal nuint size()
    {
        throwIfUnavailable();
        return _size;
    }

    internal nuint capacity()
    {
        throwIfUnavailable();
        return (nuint)_data.Length;
    }

    internal bool empty()
    {
        return size() == 0;
    }

    internal nuint max_size()
    {
        return nuint.MaxValue >> 1;
    }

    internal ref T this[nuint index]
    {
        get
        {
            throwIfUnavailable();
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _size);

            return ref _data[(int)index];
        }
    }

    internal ref T at(nuint index)
    {
        return ref this[index];
    }

    internal ref T front()
    {
        return ref this[0];
    }

    internal ref T back()
    {
        throwIfUnavailable();
        if (_size == 0)
        {
            throw new InvalidOperationException("The vector is empty.");
        }

        return ref _data[(int)(_size - 1)];
    }

    internal JitStdAllocator<T> get_allocator()
    {
        throwIfUnavailable();
        return _allocator;
    }

    internal Span<T> data()
    {
        throwIfUnavailable();
        return _data.AsSpan(0, checked((int)_size));
    }

    internal ReadOnlySpan<T> const_data()
    {
        throwIfUnavailable();
        return _data.AsSpan(0, checked((int)_size));
    }

    internal Iterator begin()
    {
        throwIfUnavailable();
        return new Iterator(this, 0);
    }

    internal Iterator end()
    {
        throwIfUnavailable();
        return new Iterator(this, checked((nint)_size));
    }

    internal ConstIterator begin_const()
    {
        return new ConstIterator(begin());
    }

    internal ConstIterator end_const()
    {
        return new ConstIterator(end());
    }

    internal ConstIterator cbegin()
    {
        return begin_const();
    }

    internal ConstIterator cend()
    {
        return end_const();
    }

    internal ReverseIterator rbegin()
    {
        throwIfUnavailable();
        return new ReverseIterator(new Iterator(this, checked((nint)_size - 1)));
    }

    internal ReverseIterator rend()
    {
        throwIfUnavailable();
        return new ReverseIterator(new Iterator(this, -1));
    }

    internal ConstReverseIterator rbegin_const()
    {
        return new ConstReverseIterator(new ConstIterator(rbegin()._iterator));
    }

    internal ConstReverseIterator rend_const()
    {
        return new ConstReverseIterator(new ConstIterator(rend()._iterator));
    }

    internal void reserve(nuint requestedCapacity)
    {
        throwIfUnavailable();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(requestedCapacity, max_size());

        if (requestedCapacity > capacity())
        {
            reallocate(requestedCapacity);
        }
    }

    internal void clear()
    {
        throwIfUnavailable();
        destroyRange(0, _size);
        _size = 0;
    }

    internal void resize(nuint newSize)
    {
        resize(newSize, default!);
    }

    internal void resize(nuint newSize, T value)
    {
        throwIfUnavailable();
        if (newSize < _size)
        {
            destroyRange(newSize, _size);
            _size = newSize;
            return;
        }

        if (newSize == _size)
        {
            return;
        }

        ensureCapacity(newSize);
        for (var index = _size; index < newSize; index++)
        {
            _allocator.construct(ref _data[(int)index], in value);
        }

        _size = newSize;
    }

    internal void push_back(T value)
    {
        throwIfUnavailable();
        ensureCapacity(checked(_size + 1));
        _allocator.construct(ref _data[(int)_size], in value);
        _size++;
    }

    internal void emplace_back(Func<T> factory)
    {
        push_back(factory());
    }

    internal void pop_back()
    {
        throwIfUnavailable();
        if (_size == 0)
        {
            throw new InvalidOperationException("Cannot remove an element from an empty vector.");
        }

        --_size;
        _allocator.destroy(ref _data[(int)_size]);
    }

    internal Iterator insert(Iterator position, T value)
    {
        return insertValues(position, [value], constructInserted: true);
    }

    internal Iterator insert(Iterator position, nuint count, T value)
    {
        throwIfUnavailable();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (nuint)Array.MaxLength);

        var values = new T[(int)count];
        Array.Fill(values, value);
        return insertValues(position, values, constructInserted: true);
    }

    internal Iterator insert(Iterator position, Iterator first, Iterator last)
    {
        throwIfUnavailable();
        var source = first._owner ?? throw new InvalidOperationException("The first iterator is not initialized.");
        if (!ReferenceEquals(source, last._owner))
        {
            throw new InvalidOperationException("The range iterators must refer to the same vector.");
        }

        var firstIndex = source.validate(first, allowEnd: true);
        var lastIndex = source.validate(last, allowEnd: true);
        if (lastIndex < firstIndex)
        {
            throw new ArgumentException("The end iterator precedes the start iterator.");
        }

        var values = new T[checked((int)(lastIndex - firstIndex))];
        for (var index = firstIndex; index < lastIndex; index++)
        {
            values[(int)(index - firstIndex)] = source._data[(int)index];
        }

        return insertValues(position, values, constructInserted: false);
    }

    internal Iterator insert(Iterator position, IEnumerable<T> values)
    {
        throwIfUnavailable();
        var inserted = materialize(values);
        return insertValues(position, inserted, constructInserted: false);
    }

    internal Iterator erase(Iterator position)
    {
        return erase(position, position + 1);
    }

    internal Iterator erase(Iterator first, Iterator last)
    {
        throwIfUnavailable();
        var firstIndex = validate(first, allowEnd: true);
        var lastIndex = validate(last, allowEnd: true);
        if (lastIndex < firstIndex)
        {
            throw new ArgumentException("The end iterator precedes the start iterator.");
        }

        var removed = (nuint)(lastIndex - firstIndex);
        if (removed == 0)
        {
            return new Iterator(this, firstIndex);
        }

        var oldSize = _size;
        for (var source = (nuint)lastIndex; source < oldSize; source++)
        {
            var valueToMove = _data[(int)source];
            ref var destination = ref _data[(int)(source - removed)];
            _allocator.destroy(ref destination);
            _allocator.assign(ref destination, in valueToMove);
        }

        _size = oldSize - removed;
        return new Iterator(this, firstIndex);
    }

    internal void assign(nuint count, T value)
    {
        throwIfUnavailable();
        ensureCapacity(count);
        for (nuint index = 0; index < count; index++)
        {
            _allocator.assign(ref _data[(int)index], in value);
        }

        _size = count;
    }

    internal void assign(IEnumerable<T> values)
    {
        throwIfUnavailable();
        var assigned = materialize(values);
        ensureCapacity((nuint)assigned.Length);
        var assignedCount = (nuint)assigned.Length;
        for (nuint index = 0; index < assignedCount; index++)
        {
            _allocator.assign(ref _data[(int)index], in assigned[(int)index]);
        }

        _size = assignedCount;
    }

    internal void assign(Iterator first, Iterator last)
    {
        throwIfUnavailable();
        var source = first._owner ?? throw new InvalidOperationException("The first iterator is not initialized.");
        if (!ReferenceEquals(source, last._owner))
        {
            throw new InvalidOperationException("The range iterators must refer to the same vector.");
        }

        var firstIndex = source.validate(first, allowEnd: true);
        var lastIndex = source.validate(last, allowEnd: true);
        if (lastIndex < firstIndex)
        {
            throw new ArgumentException("The end iterator precedes the start iterator.");
        }

        var values = new T[checked((int)(lastIndex - firstIndex))];
        for (var index = firstIndex; index < lastIndex; index++)
        {
            values[(int)(index - firstIndex)] = source._data[(int)index];
        }

        assign(values);
    }

    internal void assign<TSource>(JitStdVector<TSource> source, Func<TSource, T> convert)
    {
        throwIfUnavailable();
        var sourceSize = source.size();
        var convertedValues = new T[checked((int)sourceSize)];
        for (nuint index = 0; index < sourceSize; index++)
        {
            convertedValues[(int)index] = convert(source[index]);
        }

        ensureCapacity(sourceSize);
        for (nuint index = 0; index < sourceSize; index++)
        {
            _allocator.assign(ref _data[(int)index], in convertedValues[(int)index]);
        }

        _size = sourceSize;
    }

    internal static JitStdVector<T> copyFrom<TSource>(JitStdVector<TSource> source, Func<TSource, T> convert)
    {
        var result = new JitStdVector<T>(new JitStdAllocator<T>(source._allocator.scope));
        result.constructConvertedRange(source, convert);
        return result;
    }

    internal void moveFrom(JitStdVector<T> source)
    {
        throwIfUnavailable();
        source.throwIfUnavailable();
        if (ReferenceEquals(this, source))
        {
            return;
        }

        clear();
        _allocator = source._allocator;
        _data = source._data;
        _size = source._size;
        source._data = [];
        source._size = 0;
        _generation++;
        source._generation++;
    }

    internal void swap(JitStdVector<T> other)
    {
        throwIfUnavailable();
        other.throwIfUnavailable();
        (_allocator, other._allocator) = (other._allocator, _allocator);
        (_data, other._data) = (other._data, _data);
        (_size, other._size) = (other._size, _size);
        _generation++;
        other._generation++;
    }

    public IEnumerator<T> GetEnumerator()
    {
        throwIfUnavailable();
        var generation = _generation;
        for (nuint index = 0; index < _size; index++)
        {
            if (generation != _generation)
            {
                throw new InvalidOperationException("The vector changed during enumeration.");
            }

            yield return _data[(int)index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _allocator.scope.throwIfDisposed();
        destroyRange(0, _size);
        _size = 0;
        _data = [];
        _generation++;
        _isDisposed = true;
    }

    private void ensureCapacity(nuint requiredCapacity)
    {
        if (requiredCapacity > capacity())
        {
            var allocationCapacity = _size * 2;
            if (allocationCapacity < requiredCapacity)
            {
                allocationCapacity = requiredCapacity;
            }

            reallocate(allocationCapacity);
        }
    }

    private void reallocate(nuint newCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newCapacity, max_size());

        var newData = _allocator.allocate(newCapacity);
        nuint copied = 0;

        try
        {
            for (; copied < _size; copied++)
            {
                var value = _data[(int)copied];
                _allocator.construct(ref newData[(int)copied], in value);
            }
        }
        catch
        {
            destroyRange(newData, 0, copied);
            throw;
        }

        _data = newData;
        _generation++;
    }

    private void constructRange(IEnumerable<T> values)
    {
        var constructed = materialize(values);
        ensureCapacity((nuint)constructed.Length);
        nuint count = 0;

        try
        {
            for (; count < (nuint)constructed.Length; count++)
            {
                _allocator.construct(ref _data[(int)count], in constructed[(int)count]);
            }
        }
        catch
        {
            destroyRange(0, count);
            throw;
        }

        _size = count;
    }

    private void constructConvertedRange<TSource>(JitStdVector<TSource> source, Func<TSource, T> convert)
    {
        var convertedValues = new T[checked((int)source.size())];
        for (var index = 0; index < convertedValues.Length; index++)
        {
            convertedValues[index] = convert(source[(nuint)index]);
        }

        constructRange(convertedValues);
    }

    private Iterator insertValues(Iterator position, T[] inserted, bool constructInserted)
    {
        throwIfUnavailable();
        var index = validate(position, allowEnd: true);
        if (inserted.Length == 0)
        {
            return new Iterator(this, index);
        }

        var oldSize = _size;
        var count = (nuint)inserted.Length;
        var newSize = checked(oldSize + count);
        ensureCapacity(newSize);

        for (var source = oldSize; source > (nuint)index; source--)
        {
            var destination = source + count - 1;
            var valueToMove = _data[(int)(source - 1)];
            _allocator.assign(ref _data[(int)destination], in valueToMove);
        }

        for (nuint insertedIndex = 0; insertedIndex < count; insertedIndex++)
        {
            var destination = (nuint)index + insertedIndex;
            var valueToInsert = inserted[(int)insertedIndex];
            if (constructInserted)
            {
                _allocator.construct(ref _data[(int)destination], in valueToInsert);
            }
            else
            {
                _allocator.assign(ref _data[(int)destination], in valueToInsert);
            }
        }

        _size = newSize;
        return new Iterator(this, index);
    }

    private void destroyRange(nuint first, nuint last)
    {
        destroyRange(_data, first, last);
    }

    private void destroyRange(T[] data, nuint first, nuint last)
    {
        for (var index = first; index < last; index++)
        {
            _allocator.destroy(ref data[(int)index]);
        }
    }

    private T[] materialize(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values is T[] array ? (T[])array.Clone() : [.. values];
    }

    private nint validate(Iterator iterator, bool allowEnd)
    {
        if (!ReferenceEquals(iterator._owner, this))
        {
            throw new InvalidOperationException("The iterator does not belong to this vector.");
        }

        validate(iterator._index, iterator._generation, allowEnd);
        return iterator._index;
    }

    private void validate(nint index, int generation, bool allowEnd)
    {
        throwIfUnavailable();
        if (generation != _generation)
        {
            throw new InvalidOperationException("The vector iterator was invalidated by reallocation.");
        }

        var limit = checked((nint)_size);
        if (index < 0 || index > limit || (!allowEnd && index == limit))
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private void throwIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _allocator.scope.throwIfDisposed();
    }
}
