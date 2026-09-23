// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace RyuJitSharp;

public ref struct MarshaledUtf8String : IDisposable
{
    private const int InlineBufferSize = 256;

    private readonly InlineBuffer _inline;
    private byte[]? _array;
    private int _length;

    public MarshaledUtf8String(scoped ReadOnlySpan<char> source)
    {
        var destination = (Span<byte>)(_inline);
        var operationStatus = Utf8.FromUtf16(source, destination[..^1], out var charsRead, out var bytesWritten);

        if (operationStatus != OperationStatus.Done)
        {
            source = source[charsRead..];

            var array = ArrayPool<byte>.Shared.Rent(bytesWritten + Encoding.UTF8.GetByteCount(source) + 1);
            destination[..bytesWritten].CopyTo(array);

            destination = array.AsSpan(bytesWritten);
            operationStatus = Utf8.FromUtf16(source, destination[..^1], out _, out bytesWritten);

            assert(operationStatus == OperationStatus.Done);

            _array = array;
        }

        destination[bytesWritten] = 0;
        _length = bytesWritten;
    }

    public readonly int Length => _length;

    public void Dispose()
    {
        if (_array is not null)
        {
            ArrayPool<byte>.Shared.Return(_array);
            _array = null;
        }
        _length = 0;
    }

    [UnscopedRef]
    public readonly ref readonly byte GetPinnableReference()
    {
        var array = _array;
        return ref ((array is not null) ? ref array[0] : ref _inline[0]);
    }

    [InlineArray(InlineBufferSize)]
    private struct InlineBuffer
    {
        private byte _value0;
    }
}
