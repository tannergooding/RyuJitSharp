// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    private const int WASM_REG_TYPE_BITS = 3;
    private const int WASM_REG_TYPE_SHIFT = 32 - WASM_REG_TYPE_BITS;
    private const uint WASM_REG_TYPE_MASK = uint.MaxValue << WASM_REG_TYPE_SHIFT;

    public static regNumber MakeWasmReg(uint index, var_types type)
    {
        return MakeWasmReg(index, TypeToWasmValueType(type));
    }

    public static regNumber MakeWasmReg(uint index, WasmValueType type)
    {
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

    public static bool IsValidWasmReg(regNumber reg)
    {
        _ = UnpackWasmReg(reg, out var type);
        return (WasmValueType.Invalid < type) && (type < WasmValueType.Count);
    }

    public static bool IsValidWasmIntReg(regNumber reg)
    {
        _ = UnpackWasmReg(reg, out var type);
        return (type is WasmValueType.I32) || (type is WasmValueType.I64);
    }

    public static bool IsValidWasmFloatReg(regNumber reg)
    {
        _ = UnpackWasmReg(reg, out var type);
        return (type is WasmValueType.F32) || (type is WasmValueType.F64) || (type is WasmValueType.V128);
    }

    public static string GetWasmRegName(regNumber reg)
    {
        if (reg is REG_NA)
        {
            return "NA";
        }

        ReadOnlySpan<string> names = [
            "$0", "$1", "$2", "$3", "$4", "$5", "$6", "$7", "$8", "$9",
            "$10", "$11", "$12", "$13", "$14", "$15", "$16", "$17", "$18", "$19",
        ];
        var index = UnpackWasmReg(reg, out _);
        if (index < (uint)names.Length)
        {
            return names[(int)index];
        }

        return "$<too large to print>";
    }

    private static WasmValueType InvalidWasmType()
    {
        unreached();
        return WasmValueType.Invalid;
    }

    extension(regNumber regNum)
    {
        public string Name => GetWasmRegName(regNum);
    }
}
#endif
