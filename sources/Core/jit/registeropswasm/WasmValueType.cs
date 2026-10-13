// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum WasmValueType : uint
{
    Invalid,
    I32,
    I64,
    F32,
    F64,
    V128,
    ExnRef,
    Count,

    First = I32,
    // This implementation supports Wasm32, whose native integer and reference type is i32.
    I = I32,
}
