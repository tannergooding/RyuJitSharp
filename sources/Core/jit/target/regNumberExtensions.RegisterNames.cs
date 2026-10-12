// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    private const int WASM_REG_TYPE_BITS = 3;
    private const int WASM_REG_TYPE_SHIFT = 32 - WASM_REG_TYPE_BITS;
    private const uint WASM_REG_TYPE_MASK = uint.MaxValue << WASM_REG_TYPE_SHIFT;

    private static string GetName(regNumber regNum)
    {
        if (Configuration.TARGET_AMD64)
        {
            if (regNum == REG_NA_AMD64)
            {
                return "NA";
            }

            assert((uint)regNum < (uint)REG_COUNT_AMD64, conditionExpression: "(unsigned)reg < ArrLen(regNames)");
            return GetNameAMD64(regNum);
        }

        if (Configuration.TARGET_X86)
        {
            if (regNum == REG_NA_X86)
            {
                return "NA";
            }

            assert((uint)regNum < (uint)REG_COUNT_X86, conditionExpression: "(unsigned)reg < ArrLen(regNames)");
            return GetNameX86(regNum);
        }

        if (Configuration.TARGET_ARM64)
        {
            if (regNum == REG_NA_ARM64)
            {
                return "NA";
            }

            assert((uint)regNum < (uint)REG_COUNT_ARM64, conditionExpression: "(unsigned)reg < ArrLen(regNames)");
            return GetNameARM64(regNum);
        }

        if (Configuration.TARGET_ARM)
        {
            if (regNum == REG_NA_ARM)
            {
                return "NA";
            }

            assert((uint)regNum < (uint)REG_COUNT_ARM, conditionExpression: "(unsigned)reg < ArrLen(regNames)");
            return GetNameARM(regNum);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            if (regNum == REG_NA_LOONGARCH64)
            {
                return "NA";
            }

            assert((uint)regNum < (uint)REG_COUNT_LOONGARCH64, conditionExpression: "(unsigned)reg < ArrLen(regNames)");
            return GetNameLOONGARCH64(regNum);
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (regNum == REG_NA_RISCV64)
            {
                return "NA";
            }

            assert((uint)regNum < (uint)REG_COUNT_RISCV64, conditionExpression: "(unsigned)reg < ArrLen(regNames)");
            return GetNameRISCV64(regNum);
        }

        if (Configuration.TARGET_WASM)
        {
            return GetWasmRegNameCore(regNum);
        }

        throw new FatalJitException("Unsupported target architecture.");
    }

    public static string GetWasmRegName(regNumber reg)
    {
        if (!Configuration.TARGET_WASM)
        {
            throw new FatalJitException("WebAssembly register names require the WebAssembly target.");
        }

        return GetWasmRegNameCore(reg);
    }

    private static string GetWasmRegNameCore(regNumber reg)
    {
        if (reg == REG_NA_WASM)
        {
            return "NA";
        }

        ReadOnlySpan<string> names = [
            "$0", "$1", "$2", "$3", "$4", "$5", "$6", "$7", "$8", "$9",
            "$10", "$11", "$12", "$13", "$14", "$15", "$16", "$17", "$18", "$19",
        ];
        var index = unchecked((uint)reg) & ~WASM_REG_TYPE_MASK;
        if (index < (uint)names.Length)
        {
            return names[(int)index];
        }

        return "$<too large to print>";
    }
}
