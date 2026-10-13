// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    public static regNumber MakeWasmReg(uint index, var_types type)
    {
        return MakeWasmReg(index, TypeToWasmValueType(type));
    }

    public static regNumber MakeWasmReg(uint index, WasmValueType type)
    {
        EnsureWasmTarget();
        assert((WasmValueType.Invalid < type) && (type < WasmValueType.Count));
        if ((index & WASM_REG_TYPE_MASK) != 0)
        {
            IMPL_LIMITATION("Too many locals");
        }

        var value = index | ((uint)type << WASM_REG_TYPE_SHIFT);
        return (regNumber)value;
    }

    public static WasmValueType TypeToWasmValueType(var_types type)
    {
        EnsureWasmTarget();
        return type switch {
            TYP_INT => WasmValueType.I32,
            TYP_LONG => WasmValueType.I64,
            TYP_FLOAT => WasmValueType.F32,
            TYP_DOUBLE => WasmValueType.F64,
            TYP_REF or TYP_BYREF => WasmValueType.I,
            TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 => WasmValueType.V128,
            _ => InvalidWasmType(),
        };
    }

    public static WasmValueType ActualTypeToWasmValueType(var_types type)
    {
        EnsureWasmTarget();
        return type switch {
            TYP_BYTE or TYP_UBYTE or TYP_SHORT or TYP_USHORT or TYP_INT => WasmValueType.I32,
            TYP_LONG => WasmValueType.I64,
            TYP_FLOAT => WasmValueType.F32,
            TYP_DOUBLE => WasmValueType.F64,
            TYP_REF or TYP_BYREF => WasmValueType.I,
            TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 => WasmValueType.V128,
            _ => InvalidWasmType(),
        };
    }

    public static uint UnpackWasmReg(regNumber reg, out WasmValueType type)
    {
        EnsureWasmTarget();
        var value = (uint)reg;
        type = (WasmValueType)(value >> WASM_REG_TYPE_SHIFT);
        return value & ~WASM_REG_TYPE_MASK;
    }

    public static uint WasmRegToIndex(regNumber reg)
    {
        var index = UnpackWasmReg(reg, out var type);
        assert((WasmValueType.Invalid < type) && (type < WasmValueType.Count));
        return index;
    }

    public static WasmValueType WasmRegToType(regNumber reg)
    {
        _ = UnpackWasmReg(reg, out var type);
        assert((WasmValueType.Invalid < type) && (type < WasmValueType.Count));
        return type;
    }

    private static WasmValueType InvalidWasmType()
    {
        unreached();
        return WasmValueType.Invalid;
    }

    private static void EnsureWasmTarget()
    {
        if (!Configuration.TARGET_WASM)
        {
            throw new FatalJitException("WebAssembly register operations require the WebAssembly target.");
        }
    }
}
