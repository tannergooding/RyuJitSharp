// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly struct Pointer
{
    private readonly unsafe void* _value;

    public unsafe Pointer(void* value)
    {
        _value = value;
    }

    public static unsafe implicit operator Pointer(void* value) => new Pointer(value);

    public static unsafe implicit operator void*(Pointer pointer) => pointer._value;

    public unsafe void* Value => _value;
}
