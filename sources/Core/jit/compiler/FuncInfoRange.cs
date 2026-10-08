// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly ref struct FuncInfoRange
{
    private readonly Span<FuncInfoDsc> _functions;

    public FuncInfoRange(Span<FuncInfoDsc> functions)
    {
        _functions = functions;
    }

    public readonly Span<FuncInfoDsc>.Enumerator GetEnumerator()
    {
        return _functions.GetEnumerator();
    }

    public readonly ReverseView Reverse()
    {
        return new ReverseView(_functions);
    }

    public readonly ref struct ReverseView
    {
        private readonly Span<FuncInfoDsc> _functions;

        public ReverseView(Span<FuncInfoDsc> functions)
        {
            _functions = functions;
        }

        public readonly ReverseIterator GetEnumerator()
        {
            return new ReverseIterator(_functions);
        }
    }

    public ref struct ReverseIterator
    {
        private readonly Span<FuncInfoDsc> _functions;
        private int _index;

        public ReverseIterator(Span<FuncInfoDsc> functions)
        {
            _functions = functions;
            _index = functions.Length;
        }

        public readonly ref FuncInfoDsc Current => ref _functions[_index];

        public bool MoveNext()
        {
            if (_index == 0)
            {
                return false;
            }

            _index--;

            return true;
        }
    }
}
