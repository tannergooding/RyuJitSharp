// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
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
#if TARGET_64BIT
    I = I64,
#else
    I = I32,
#endif
}
#endif
