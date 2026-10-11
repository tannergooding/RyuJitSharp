// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoWasmType;

namespace RyuJitSharp;

/// <summary>Used by Wasm RyuJIT to represent native WebAssembly types and exchanged via some JIT-EE APIs</summary>
public enum CorInfoWasmType
{
    CORINFO_WASM_TYPE_VOID = 0x40,

    CORINFO_WASM_TYPE_V128 = 0x7B,

    CORINFO_WASM_TYPE_F64 = 0x7C,

    CORINFO_WASM_TYPE_F32 = 0x7D,

    CORINFO_WASM_TYPE_I64 = 0x7E,

    CORINFO_WASM_TYPE_I32 = 0x7F
}

internal static class WasmTypeMapper
{
    internal static var_types ToJitType(CorInfoWasmType wasmType)
    {
        return wasmType switch {
            CORINFO_WASM_TYPE_I32 => TYP_INT,
            CORINFO_WASM_TYPE_I64 => TYP_LONG,
            CORINFO_WASM_TYPE_F32 => TYP_FLOAT,
            CORINFO_WASM_TYPE_F64 => TYP_DOUBLE,
            CORINFO_WASM_TYPE_V128 => TYP_SIMD16,
            _ => InvalidWasmType(),
        };
    }

    private static var_types InvalidWasmType()
    {
        unreached();
        return TYP_UNDEF;
    }
}
