// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Diagnostics;
#endif

namespace RyuJitSharp;

#if DEBUG
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
#endif
internal sealed class JitStdList<T> : IDisposable, IEnumerable<T>
{
#if DEBUG
    private string DebuggerDisplayText => _size is 0 ? "Empty" : $"Size={_size}";
#endif

    internal sealed class Node : IJitStdScopeAllocation
    {
        internal T value = default!;
        internal Node? next;
        internal Node? previous;
        internal JitStdList<T>? owner;
        internal bool isAlive = true;

        public void release()
        {
            value = default!;
            next = null;
            previous = null;
            owner = null;
            isAlive = false;
        }
    }

    // Match the native Node value layout without the managed object's header or owner/lifetime fields.
    private struct NodeLayout
    {
        internal T value;
        internal Node? next;
        internal Node? previous;
    }

    internal readonly struct Iterator : IJitStdIterator, IEquatable<Iterator>
    {
        internal readonly Node? _node;
        internal readonly JitStdList<T>? _endOwner;

        internal Iterator(Node? node, JitStdList<T>? endOwner)
        {
            _node = node;
            _endOwner = endOwner;
        }

        internal ref T value
        {
            get
            {
                var node = getNode();
                return ref node.value;
            }
        }

        internal Node? node => _node;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.Bidirectional;

        public static Iterator operator ++(Iterator iterator)
        {
            var node = iterator.getNodeOrEnd();
            return node is null
                ? iterator
                : new Iterator(node.next, node.owner);
        }

        public static Iterator operator --(Iterator iterator)
        {
            var node = iterator.getNodeOrEnd();
            if (node is null)
            {
                return iterator._endOwner is null ? iterator : new Iterator(iterator._endOwner._tail, iterator._endOwner);
            }

            return node.previous is null ? iterator : new Iterator(node.previous, node.owner);
        }

        public static Iterator operator +(Iterator iterator, nint offset)
        {
            while (offset > 0)
            {
                iterator = ++iterator;
                offset--;
            }

            while (offset < 0)
            {
                iterator = --iterator;
                offset++;
            }

            return iterator;
        }

        public static Iterator operator -(Iterator iterator, nint offset) => iterator + checked(-offset);

        public static nint operator -(Iterator first, Iterator last)
        {
            if (first.Equals(last))
            {
                return 0;
            }

            nint distance = 0;
            for (var current = last; ; current++)
            {
                if (current.Equals(first))
                {
                    return distance;
                }

                if (current._node is null)
                {
                    break;
                }

                distance++;
            }

            distance = 0;
            for (var current = last; ; current--)
            {
                if (current.Equals(first))
                {
                    return -distance;
                }

                if (current._node is null)
                {
                    break;
                }

                if (current._node.previous is null)
                {
                    break;
                }

                distance++;
            }

            throw new InvalidOperationException("The list iterators are not part of the same sequence.");
        }

        public static bool operator ==(Iterator left, Iterator right) => left.Equals(right);

        public static bool operator !=(Iterator left, Iterator right) => !left.Equals(right);

        public bool Equals(Iterator other)
        {
            return ReferenceEquals(_node, other._node);
        }

        public override bool Equals(object? obj)
        {
            return obj is Iterator other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _node is null ? 0 : _node.GetHashCode();
        }

        private Node getNode()
        {
            var node = getNodeOrEnd() ?? throw new InvalidOperationException("The end iterator cannot be dereferenced.");
            if (!node.isAlive)
            {
                throw new InvalidOperationException("The iterator refers to an erased node.");
            }

            return node;
        }

        private Node? getNodeOrEnd()
        {
            if (_node is not null && !_node.isAlive)
            {
                throw new InvalidOperationException("The iterator refers to an erased node.");
            }

            return _node;
        }

        internal Iterator reverseIncrement()
        {
            var node = getNodeOrEnd();
            return new Iterator(node?.previous, node?.owner ?? _endOwner);
        }

        internal Iterator reverseDecrement()
        {
            var node = getNodeOrEnd();
            return node is null
                ? new Iterator(_endOwner?._head, _endOwner)
                : new Iterator(node.next, node.owner);
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

        internal Node? node => _iterator.node;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.Bidirectional;

        internal ConstIterator reverseIncrement()
        {
            return new ConstIterator(_iterator.reverseIncrement());
        }

        internal ConstIterator reverseDecrement()
        {
            return new ConstIterator(_iterator.reverseDecrement());
        }

        public static ConstIterator operator ++(ConstIterator iterator) => new ConstIterator(iterator._iterator + 1);

        public static ConstIterator operator --(ConstIterator iterator) => new ConstIterator(iterator._iterator - 1);

        public static ConstIterator operator +(ConstIterator iterator, nint offset) => new ConstIterator(iterator._iterator + offset);

        public static ConstIterator operator -(ConstIterator iterator, nint offset) => iterator + checked(-offset);

        public static nint operator -(ConstIterator first, ConstIterator last) => first._iterator - last._iterator;

        public static bool operator ==(ConstIterator left, ConstIterator right) => left.Equals(right);

        public static bool operator !=(ConstIterator left, ConstIterator right) => !left.Equals(right);

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
        private readonly Iterator _iterator;

        internal ReverseIterator(Iterator iterator)
        {
            _iterator = iterator;
        }

        internal ref T value => ref _iterator.value;

        internal Node? node => _iterator.node;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.Bidirectional;

        public static ReverseIterator operator ++(ReverseIterator iterator) => new(iterator._iterator.reverseIncrement());

        public static ReverseIterator operator --(ReverseIterator iterator) => new(iterator._iterator.reverseDecrement());

        public static ReverseIterator operator +(ReverseIterator iterator, nint offset) => new(iterator._iterator - offset);

        public static ReverseIterator operator -(ReverseIterator iterator, nint offset) => iterator + checked(-offset);

        public static bool operator ==(ReverseIterator left, ReverseIterator right) => left.Equals(right);

        public static bool operator !=(ReverseIterator left, ReverseIterator right) => !left.Equals(right);

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

        internal Node? node => _iterator.node;

        public JitStdIteratorCategory Category => JitStdIteratorCategory.Bidirectional;

        public static ConstReverseIterator operator ++(ConstReverseIterator iterator) => new ConstReverseIterator(iterator._iterator.reverseIncrement());

        public static ConstReverseIterator operator --(ConstReverseIterator iterator) => new ConstReverseIterator(iterator._iterator.reverseDecrement());

        public static ConstReverseIterator operator +(ConstReverseIterator iterator, nint offset) => new ConstReverseIterator(iterator._iterator - offset);

        public static ConstReverseIterator operator -(ConstReverseIterator iterator, nint offset) => iterator + checked(-offset);

        public static bool operator ==(ConstReverseIterator left, ConstReverseIterator right) => left.Equals(right);

        public static bool operator !=(ConstReverseIterator left, ConstReverseIterator right) => !left.Equals(right);

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
    private Node? _head;
    private Node? _tail;
    private nuint _size;
    private bool _isDisposed;

    internal JitStdList(JitStdAllocator<T> allocator)
    {
        _allocator = allocator;
        _allocator.scope.throwIfDisposed();
    }

    internal JitStdList(IEnumerable<T> values, JitStdAllocator<T> allocator)
        : this(allocator)
    {
        assign(values);
    }

    internal JitStdList(nuint count, T value, JitStdAllocator<T> allocator)
        : this(allocator)
    {
        assign(count, value);
    }

    internal nuint size()
    {
        throwIfUnavailable();
        return _size;
    }

    internal bool empty()
    {
        return size() == 0;
    }

    internal ref T front()
    {
        throwIfUnavailable();
        return ref (_head ?? throw new InvalidOperationException("The list is empty.")).value;
    }

    internal ref T back()
    {
        throwIfUnavailable();
        return ref (_tail ?? throw new InvalidOperationException("The list is empty.")).value;
    }

    internal Iterator backPosition()
    {
        throwIfUnavailable();
        return new Iterator(_tail, this);
    }

    internal nuint max_size()
    {
        throwIfUnavailable();
        return (nuint.MaxValue >> 1) / (nuint)Unsafe.SizeOf<NodeLayout>();
    }

    internal JitStdAllocator<T> get_allocator()
    {
        throwIfUnavailable();
        return _allocator;
    }

    internal Iterator begin()
    {
        throwIfUnavailable();
        return new Iterator(_head, this);
    }

    internal Iterator end()
    {
        throwIfUnavailable();
        return new Iterator(null, this);
    }

    internal ConstIterator begin_const()
    {
        return new ConstIterator(begin());
    }

    internal ConstIterator end_const()
    {
        return new ConstIterator(end());
    }

    internal ReverseIterator rbegin()
    {
        throwIfUnavailable();
        return new ReverseIterator(new Iterator(_tail, this));
    }

    internal ReverseIterator rend()
    {
        throwIfUnavailable();
        return new ReverseIterator(new Iterator(null, this));
    }

    internal ConstReverseIterator rbegin_const()
    {
        return new ConstReverseIterator(new ConstIterator(new Iterator(_tail, this)));
    }

    internal ConstReverseIterator rend_const()
    {
        return new ConstReverseIterator(new ConstIterator(new Iterator(null, this)));
    }

    internal Iterator insert(Iterator position, T value)
    {
        throwIfUnavailable();
        var positionNode = validatePosition(position);
        var node = createNode(value);
        linkBefore(positionNode, node);
        return new Iterator(node, this);
    }

    internal Iterator insert(Iterator position, IEnumerable<T> values)
    {
        throwIfUnavailable();
        var positionNode = validatePosition(position);
        var snapshot = materialize(values);
        Node? firstInserted = null;

        foreach (var value in snapshot)
        {
            var node = createNode(value);
            linkBefore(positionNode, node);
            firstInserted ??= node;
        }

        return firstInserted is null ? new Iterator(positionNode, this) : new Iterator(firstInserted, this);
    }

    internal Iterator insert(Iterator position, Iterator first, Iterator last)
    {
        throwIfUnavailable();
        var values = new List<T>();
        for (var current = first; !current.Equals(last); current++)
        {
            if (current.node is null)
            {
                throw new ArgumentException("The end iterator is not reachable from the start iterator.");
            }

            values.Add(current.value);
        }

        return insert(position, values);
    }

    internal void insert(Iterator position, nuint count, T value)
    {
        throwIfUnavailable();
        var positionNode = validatePosition(position);
        for (nuint index = 0; index < count; index++)
        {
            var node = createNode(value);
            linkBefore(positionNode, node);
        }
    }

    internal Iterator emplace(Iterator position, Func<T> factory)
    {
        return insert(position, factory());
    }

    internal void push_front(T value)
    {
        _ = insert(begin(), value);
    }

    internal void push_back(T value)
    {
        _ = insert(end(), value);
    }

    internal void emplace_front(Func<T> factory)
    {
        push_front(factory());
    }

    internal void emplace_back(Func<T> factory)
    {
        push_back(factory());
    }

    internal void pop_front()
    {
        throwIfUnavailable();
        if (_head is null)
        {
            throw new InvalidOperationException("Cannot remove an element from an empty list.");
        }

        eraseNode(_head, callElementDestructor: true);
    }

    internal void pop_back()
    {
        throwIfUnavailable();
        if (_tail is null)
        {
            throw new InvalidOperationException("Cannot remove an element from an empty list.");
        }

        eraseNode(_tail, callElementDestructor: true);
    }

    internal Iterator erase(Iterator position)
    {
        throwIfUnavailable();
        var node = validateElement(position);
        var next = node.next;
        eraseNode(node, callElementDestructor: false);
        return new Iterator(next, this);
    }

    internal Iterator erase(Iterator first, Iterator last)
    {
        throwIfUnavailable();
        var current = validatePosition(first);
        var lastNode = validatePosition(last);
        while (!ReferenceEquals(current, lastNode))
        {
            var node = current ?? throw new ArgumentException("The end iterator is not reachable from the start iterator.");
            current = node.next;
            eraseNode(node, callElementDestructor: false);
        }

        return new Iterator(lastNode, this);
    }

    internal void clear()
    {
        throwIfUnavailable();
        while (_head is not null)
        {
            eraseNode(_head, callElementDestructor: true);
        }
    }

    internal void assign(IEnumerable<T> values)
    {
        var snapshot = materialize(values);
        clear();
        foreach (var value in snapshot)
        {
            push_back(value);
        }
    }

    internal void assign(Iterator first, Iterator last)
    {
        var values = new List<T>();
        for (var current = first; !current.Equals(last); current++)
        {
            if (current.node is null)
            {
                throw new ArgumentException("The end iterator is not reachable from the start iterator.");
            }

            values.Add(current.value);
        }

        assign(values);
    }

    internal void assign(nuint count, T value)
    {
        clear();
        for (nuint index = 0; index < count; index++)
        {
            push_back(value);
        }
    }

#if DEBUG
    internal void init(JitStdAllocator<T> allocator)
    {
        _head = null;
        _tail = null;
        _size = 0;
        _allocator = allocator;
        _isDisposed = false;
    }
#endif

    internal void resize(nuint newSize)
    {
        resize(newSize, default!);
    }

    internal void resize(nuint newSize, T value)
    {
        throwIfUnavailable();
        while (_size > newSize)
        {
            pop_back();
        }

        while (_size < newSize)
        {
            push_back(value);
        }
    }

    internal void remove(T value, IEqualityComparer<T>? comparer = null)
    {
        removeIf(element => comparer is null
            ? JitStdComparison<T>.Equal(element, value)
            : comparer.Equals(element, value));
    }

    internal void removeIf(Predicate<T> predicate)
    {
        throwIfUnavailable();
        for (var node = _head; node is not null;)
        {
            var next = node.next;
            if (predicate(node.value))
            {
                eraseNode(node, callElementDestructor: false);
            }

            node = next;
        }
    }

    internal void unique(IEqualityComparer<T>? comparer = null)
    {
        throwIfUnavailable();
        _ = comparer;
        assert(false, "false && !\"template method not implemented.\"");
    }

    internal void reverse()
    {
        throwIfUnavailable();
        var node = _head;
        while (node is not null)
        {
            (node.next, node.previous) = (node.previous, node.next);
            node = node.previous;
        }

        (_head, _tail) = (_tail, _head);
    }

    internal void sort(IComparer<T>? comparer = null)
    {
        throwIfUnavailable();
        _ = comparer;
        assert(false, "false && !\"template method not implemented.\"");
    }

    internal void merge(JitStdList<T> other, IComparer<T>? comparer = null)
    {
        throwIfUnavailable();
        other.throwIfUnavailable();

        var size = unchecked((int)other._size);
        var current = _head;
        var otherCurrent = other._head;
        while (current is not null && otherCurrent is not null)
        {
            var shouldInsert = comparer is null
                ? JitStdComparison<T>.Greater(current.value, otherCurrent.value)
                : comparer.Compare(current.value, otherCurrent.value) > 0;
            if (shouldInsert)
            {
                current = insert(new Iterator(current, this), otherCurrent.value).node;
                otherCurrent = otherCurrent.next;
                size = unchecked(size - 1);
            }
            else
            {
                current = current.next;
            }
        }

        if (otherCurrent is not null)
        {
            // Match native list::merge: the source metadata remains unchanged after this tail link.
            if (_tail is not null)
            {
                _tail.next = otherCurrent;
            }
            else
            {
                _head = otherCurrent;
            }

            _tail = other._tail;
            _size = unchecked(_size + (nuint)size);
        }
    }

    internal void splice(Iterator position, JitStdList<T> other)
    {
        throwIfUnavailable();
        other.throwIfUnavailable();
        ensureSharedScope(other);
        _ = validatePosition(position);
        if (ReferenceEquals(this, other) || other._head is null || _head is not null)
        {
            return;
        }

        (_head, other._head) = (other._head, _head);
        (_tail, other._tail) = (other._tail, _tail);
        (_size, other._size) = (other._size, _size);
        updateOwners(_head, this);
        updateOwners(other._head, other);
    }

    internal void splice(Iterator position, JitStdList<T> other, Iterator element)
    {
        throwIfUnavailable();
        other.throwIfUnavailable();
    }

    internal void splice(Iterator position, JitStdList<T> other, Iterator first, Iterator last)
    {
        throwIfUnavailable();
        other.throwIfUnavailable();
    }

    internal Node? nodeIdentity(Iterator iterator)
    {
        return iterator.node;
    }

    internal void moveFrom(JitStdList<T> other)
    {
        throwIfUnavailable();
        other.throwIfUnavailable();
        if (ReferenceEquals(this, other))
        {
            return;
        }

        clear();
        ensureSharedScope(other);
        _allocator = other._allocator;
        _head = other._head;
        _tail = other._tail;
        _size = other._size;
        updateOwners(_head, this);
        other._head = null;
        other._tail = null;
        other._size = 0;
    }

    internal void swap(JitStdList<T> other)
    {
        throwIfUnavailable();
        other.throwIfUnavailable();
        (_allocator, other._allocator) = (other._allocator, _allocator);
        (_head, other._head) = (other._head, _head);
        (_tail, other._tail) = (other._tail, _tail);
        (_size, other._size) = (other._size, _size);
        updateOwners(_head, this);
        updateOwners(other._head, other);
    }

    public IEnumerator<T> GetEnumerator()
    {
        throwIfUnavailable();
        for (var node = _head; node is not null; node = node.next)
        {
            yield return node.value;
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
        clear();
        _isDisposed = true;
    }

    private Node createNode(T value)
    {
        var node = new Node { owner = this };
        _allocator.scope.register(node);
        _allocator.construct(ref node.value, in value);
        return node;
    }

    private void linkBefore(Node? position, Node node)
    {
        if (position is null)
        {
            node.previous = _tail;
            node.next = null;
            if (_tail is null)
            {
                _head = node;
            }
            else
            {
                _tail.next = node;
            }

            _tail = node;
        }
        else
        {
            node.next = position;
            node.previous = position.previous;
            if (position.previous is null)
            {
                _head = node;
            }
            else
            {
                position.previous.next = node;
            }

            position.previous = node;
        }

        node.owner = this;
        _size++;
    }

    private void unlink(Node node)
    {
        if (node.previous is null)
        {
            _head = node.next;
        }
        else
        {
            node.previous.next = node.next;
        }

        if (node.next is null)
        {
            _tail = node.previous;
        }
        else
        {
            node.next.previous = node.previous;
        }

        node.previous = null;
        node.next = null;
        _size--;
    }

    private void eraseNode(Node node, bool callElementDestructor)
    {
        unlink(node);
        if (callElementDestructor)
        {
            _allocator.destroy(ref node.value);
        }

        node.value = default!;
        node.owner = null;
        node.isAlive = false;
    }

    private Node? validatePosition(Iterator iterator)
    {
        var node = iterator.node;
        if (node is null)
        {
            if (!ReferenceEquals(iterator._endOwner, this))
            {
                throw new InvalidOperationException("The end iterator does not belong to this list.");
            }

            return null;
        }

        if (!node.isAlive || !ReferenceEquals(node.owner, this))
        {
            throw new InvalidOperationException("The iterator does not refer to a live node in this list.");
        }

        return node;
    }

    private Node validateElement(Iterator iterator)
    {
        return validatePosition(iterator) ?? throw new InvalidOperationException("The end iterator cannot be dereferenced.");
    }

    private static void updateOwners(Node? node, JitStdList<T> owner)
    {
        while (node is not null)
        {
            node.owner = owner;
            node = node.next;
        }
    }

    private void ensureSharedScope(JitStdList<T> other)
    {
        if (!ReferenceEquals(_allocator.scope, other._allocator.scope))
        {
            throw new InvalidOperationException("List node transfer requires a shared allocation scope.");
        }
    }

    private static T[] materialize(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values is T[] array ? (T[])array.Clone() : [.. values];
    }

    private void throwIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _allocator.scope.throwIfDisposed();
    }
}
