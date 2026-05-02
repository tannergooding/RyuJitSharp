// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly struct Pointer<T>
    where T : unmanaged
{
    private readonly unsafe T* _value;

    public unsafe Pointer(T* value)
    {
        _value = value;
    }

    public static unsafe implicit operator Pointer<T>(T* value) => new Pointer<T>(value);

    public static unsafe implicit operator T*(Pointer<T> pointer) => pointer._value;

    public unsafe T* Value => _value;
}
