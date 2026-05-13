// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public readonly struct CustomLayoutKey : IEquatable<CustomLayoutKey>
{
    private readonly int _size;

    // Array of CorInfoGCType (as BYTE) that describes the GC layout of the class.
    // For small classes the array is stored inline, avoiding an extra allocation and the pointer size overhead.
    private readonly nint _anonymous;

    public CustomLayoutKey(ClassLayout layout)
    {
        _size = layout.Size;
        _anonymous = layout.GCPtrCount > 0 ? layout.GcPtrs : 0;
    }

    public CustomLayoutKey(in ClassLayoutBuilder builder)
    {
        _size = builder._size;
        _anonymous = builder._anonymous;
    }

    public static bool operator ==(CustomLayoutKey left, CustomLayoutKey right) => left.Equals(right);

    public static bool operator !=(CustomLayoutKey left, CustomLayoutKey right) => !left.Equals(right);

    public override bool Equals([NotNullWhen(true)] object? obj) => (obj is CustomLayoutKey other) && Equals(other);

    public unsafe bool Equals(CustomLayoutKey other)
    {
        if (_size != other._size)
        {
            return false;
        }

        var gcPtrCount = _size / TARGET_POINTER_SIZE;

        if (gcPtrCount < TARGET_POINTER_SIZE)
        {
            return _anonymous == other._anonymous;
        }

        var otherSpan = new ReadOnlySpan<byte>(unchecked((byte*)(other._anonymous)), gcPtrCount);
        return new ReadOnlySpan<byte>(unchecked((byte*)(_anonymous)), gcPtrCount).SequenceEqual(otherSpan);
    }

    public override unsafe int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_size);

        var gcPtrCount = _size / TARGET_POINTER_SIZE;

        if (gcPtrCount < TARGET_POINTER_SIZE)
        {
            hashCode.Add(_anonymous);
        }
        else
        {
            var gcPtrs = new ReadOnlySpan<byte>(unchecked((byte*)(_anonymous)), gcPtrCount);
            hashCode.AddBytes(gcPtrs);
        }
        return hashCode.ToHashCode();
    }
}
