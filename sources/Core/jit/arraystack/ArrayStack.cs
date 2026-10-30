// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

internal sealed class ArrayStack<T>
{
    private const int BuiltinSize = 8;

    private T[] _data;
    private int _count;

    internal ArrayStack(int initialCapacity = BuiltinSize)
    {
        var capacity = initialCapacity > BuiltinSize ? initialCapacity : BuiltinSize;
        _data = new T[capacity];
    }

    internal void Push(T item)
    {
        if (_count == _data.Length)
        {
            Realloc();
        }

        _data[_count] = item;
        _count++;
        IncrementVersion();
    }

    internal T Pop()
    {
        Debug.Assert(_count > 0);
        _count--;
        IncrementVersion();

        return _data[_count];
    }

    internal void Pop(int count)
    {
        Debug.Assert(_count >= count);
        _count = unchecked(_count - count);
        IncrementVersion();
    }

    internal T Top(int i = 0)
    {
        Debug.Assert(_count > i);
        return _data[_count - 1 - i];
    }

    internal ref T TopRef(int i = 0)
    {
        Debug.Assert(_count > i);
        return ref _data[_count - 1 - i];
    }

    internal int Height()
    {
        return _count;
    }

    internal bool Empty()
    {
        return _count == 0;
    }

    internal T Bottom(int i = 0)
    {
        Debug.Assert(_count > i);
        return _data[i];
    }

    internal void Reset()
    {
        _count = 0;
        IncrementVersion();
    }

    internal void Reverse()
    {
        Array.Reverse(_data, 0, _count);
        IncrementVersion();
    }

    internal Span<T> Data()
    {
        return _data.AsSpan(0, _count);
    }

    internal BottomUpView BottomUpOrder()
    {
        return new BottomUpView(this, _count, GetVersion());
    }

    internal TopDownView TopDownOrder()
    {
        return new TopDownView(this, _count, GetVersion());
    }

    private void Realloc()
    {
        var newCapacity = checked(_data.Length * 2);
        Array.Resize(ref _data, newCapacity);
    }

    private uint GetVersion()
    {
        if (!Configuration.DEBUG)
        {
            return 0;
        }

        return Compiler.DebugData.Get(this).Version;
    }

    [Conditional("DEBUG")]
    private void IncrementVersion()
    {
        var debugData = Compiler.DebugData.Get(this);
        debugData.Version = unchecked(debugData.Version + 1);
    }

    internal readonly ref struct BottomUpView
    {
        private readonly ArrayStack<T> _stack;
        private readonly int _count;
        private readonly uint _version;

        internal BottomUpView(ArrayStack<T> stack, int count, uint version)
        {
            _stack = stack;
            _count = count;
            _version = Configuration.DEBUG ? version : 0;
        }

        public BottomUpEnumerator GetEnumerator()
        {
            return new BottomUpEnumerator(_stack, _count, _version);
        }
    }

    internal ref struct BottomUpEnumerator
    {
        private readonly ArrayStack<T> _stack;
        private readonly int _count;
        private readonly uint _version;

        private int _nextIndex;
        private int _currentIndex;

        internal BottomUpEnumerator(ArrayStack<T> stack, int count, uint version)
        {
            _stack = stack;
            _count = count;
            _version = Configuration.DEBUG ? version : 0;
            _nextIndex = 0;
            _currentIndex = -1;
        }

        public readonly ref T Current => ref _stack._data[_currentIndex];

        public bool MoveNext()
        {
            if (_nextIndex == _count)
            {
                return false;
            }

            _currentIndex = _nextIndex;
            _nextIndex++;
            return true;
        }

        [Conditional("DEBUG")]
        public readonly void Dispose()
        {
            Debug.Assert(_stack.GetVersion() == _version, "ArrayStack was modified during BottomUpOrder iteration");
        }
    }

    internal readonly ref struct TopDownView
    {
        private readonly ArrayStack<T> _stack;
        private readonly int _count;
        private readonly uint _version;

        internal TopDownView(ArrayStack<T> stack, int count, uint version)
        {
            _stack = stack;
            _count = count;
            _version = Configuration.DEBUG ? version : 0;
        }

        public ReverseIterator GetEnumerator()
        {
            return new ReverseIterator(_stack, _count, 0, _version);
        }
    }

    internal ref struct ReverseIterator
    {
        private readonly ArrayStack<T> _stack;
        private readonly int _endIndex;
        private readonly uint _version;

        private int _nextIndex;
        private int _currentIndex;

        internal ReverseIterator(ArrayStack<T> stack, int index, int endIndex, uint version)
        {
            _stack = stack;
            _endIndex = endIndex;
            _version = Configuration.DEBUG ? version : 0;
            _nextIndex = index;
            _currentIndex = -1;
        }

        public readonly ref T Current => ref _stack._data[_currentIndex];

        public bool MoveNext()
        {
            if (_nextIndex == _endIndex)
            {
                return false;
            }

            _nextIndex--;
            _currentIndex = _nextIndex;
            return true;
        }

        [Conditional("DEBUG")]
        public readonly void Dispose()
        {
            Debug.Assert(_stack.GetVersion() == _version, "ArrayStack was modified during TopDownOrder iteration");
        }
    }
}
