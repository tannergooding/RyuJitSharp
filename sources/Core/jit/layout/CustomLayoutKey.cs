// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public readonly struct CustomLayoutKey : IEquatable<CustomLayoutKey>
{
    private readonly int _size;

    private readonly CorInfoGCType[]? _gcPtrs;

    private readonly InlineArrayTargetPointerSize<CorInfoGCType> _inlineGCPtrs;

    public CustomLayoutKey(ClassLayout layout)
    {
        _size = layout.Size;
        _gcPtrs = layout._gcPtrs;
        _inlineGCPtrs = layout._inlineGCPtrs;
    }

    public CustomLayoutKey(in ClassLayoutBuilder builder)
    {
        _size = builder._size;
        _gcPtrs = builder._gcPtrs;
    }

    public static bool operator ==(CustomLayoutKey left, CustomLayoutKey right) => left.Equals(right);

    public static bool operator !=(CustomLayoutKey left, CustomLayoutKey right) => !left.Equals(right);

    public override bool Equals([NotNullWhen(true)] object? obj) => false;

    public unsafe bool Equals(CustomLayoutKey other)
    {
        if (_size != other._size)
        {
            return false;
        }
        var gcPtrs = _gcPtrs ?? (ReadOnlySpan<CorInfoGCType>)(_inlineGCPtrs);
        return gcPtrs.SequenceEqual(other._gcPtrs ?? (ReadOnlySpan<CorInfoGCType>)(other._inlineGCPtrs));
    }

    public override unsafe int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_size);

        var gcPtrs = _gcPtrs ?? (ReadOnlySpan<CorInfoGCType>)(_inlineGCPtrs);
        hashCode.AddBytes(MemoryMarshal.AsBytes(gcPtrs));

        return hashCode.ToHashCode();
    }
}
