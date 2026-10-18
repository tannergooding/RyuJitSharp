// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.Globals;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public static partial class Globals
{
    public static bool hasFixedRetBuffReg(CorInfoCallConvExtension callConv)
    {
        if (Configuration.TARGET_ARM64)
        {
            return !TargetOS.IsWindows || !callConvIsInstanceMethodCallConv(callConv);
        }

        if (Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT)
        {
            return callConv is CorInfoCallConvExtension.Swift;
        }

        return false;
    }

    public static regNumber theFixedRetBuffReg(CorInfoCallConvExtension callConv)
    {
        assert(hasFixedRetBuffReg(callConv));
        if (Configuration.TARGET_ARM64)
        {
            return REG_ARM64_R8;
        }

        if (Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT)
        {
            assert(callConv is CorInfoCallConvExtension.Swift);
            return REG_X64_RAX;
        }

        return regNumberExtensions.NotAvailable;
    }

    public static regMaskTP theFixedRetBuffMask(CorInfoCallConvExtension callConv)
    {
        assert(hasFixedRetBuffReg(callConv));
        if (Configuration.TARGET_ARM64)
        {
            return new regMaskTP(SRBM_ARM64_R8);
        }

        if (Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT)
        {
            assert(callConv == CorInfoCallConvExtension.Swift, "callConv == CorInfoCallConvExtension::Swift");
            return new regMaskTP(SRBM_X64_RAX);
        }

        return RBM_NONE;
    }

    public static int theFixedRetBuffArgNum(CorInfoCallConvExtension callConv)
    {
        assert(hasFixedRetBuffReg(callConv));
        if (Configuration.TARGET_ARM64)
        {
            return 8;
        }

        if (Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT)
        {
            assert(callConv is CorInfoCallConvExtension.Swift);
            return 6;
        }

        return BAD_VAR_NUM;
    }

    public static regMaskTP fullIntArgRegMask(CorInfoCallConvExtension callConv)
    {
        var result = new regMaskTP(SRBM_ARG_REGS);
        if (hasFixedRetBuffReg(callConv))
        {
            result |= theFixedRetBuffMask(callConv);
        }

        if (Configuration.SWIFT_SUPPORT && (callConv is CorInfoCallConvExtension.Swift))
        {
            if (Configuration.TARGET_ARM64)
            {
                result |= new regMaskTP(SRBM_ARM64_R20);
                // SwiftError is initially marked as a register argument, then excluded from allocation.
                result |= new regMaskTP(SRBM_ARM64_R21);
            }
            else if (Configuration.TARGET_AMD64)
            {
                result |= new regMaskTP(SRBM_X64_R13);
                // SwiftError is initially marked as a register argument, then excluded from allocation.
                result |= new regMaskTP(SRBM_X64_R12);
            }
            else
            {
                throw new FatalJitException("Swift calling conventions are not supported for this target.");
            }
        }

        return result;
    }

    // The following are human readable names for the target architectures
    public static string TARGET_READABLE_NAME
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return "X86";
            }

            if (Configuration.TARGET_AMD64)
            {
                return "AMD64";
            }

            if (Configuration.TARGET_ARM)
            {
                return "ARM";
            }

            if (Configuration.TARGET_ARM64)
            {
                return "ARM64";
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return "LOONGARCH64";
            }

            if (Configuration.TARGET_RISCV64)
            {
                return "RISCV64";
            }

            if (Configuration.TARGET_WASM)
            {
                return "WASM32";
            }

            return UnsupportedTargetValue<string>("Unsupported or unset target architecture.");
        }
    }

    public static int CSE_CONST_SHARED_LOW_BITS => Configuration.TARGET_XARCH ? 16 : 12;

    public static string REG_MASK_INT_FMT =>
        Configuration.REGMASK_BITS_32 ? "{0:X8}" : "{0:X4}";

    public static string REG_MASK_ALL_FMT =>
        Configuration.REGMASK_BITS_32 ? "{0:X8}" : "{0:X16}";

    public const int REG_LOW_BASE = 0;

    public const int REG_HIGH_BASE = 64;

    public const int DSP_SRC_OPER_LEFT  = 0;

    public const int DSP_SRC_OPER_RIGHT = 1;

    public const int DSP_DST_OPER_LEFT  = 1;

    public const int DSP_DST_OPER_RIGHT = 0;

    // Thumb branch limits and minimum sizes from targetarm.h.
    public static int JMP_DIST_SMALL_MAX_NEG =>
        Configuration.TARGET_ARM ? -2048 :
        Configuration.TARGET_XARCH ? -128 :
        UnsupportedTargetValue<int>("Short unconditional branches are not supported for this target.");

    public static int JMP_DIST_SMALL_MAX_POS =>
        Configuration.TARGET_ARM ? 2046 :
        Configuration.TARGET_XARCH ? 127 :
        UnsupportedTargetValue<int>("Short unconditional branches are not supported for this target.");

    public static int JCC_DIST_SMALL_MAX_NEG =>
        Configuration.TARGET_ARM ? -256 :
        Configuration.TARGET_XARCH ? -128 :
        Configuration.TARGET_ARM64 ? -1048576 :
        UnsupportedTargetValue<int>("Short conditional branches are not supported for this target.");

    public static int JCC_DIST_SMALL_MAX_POS =>
        Configuration.TARGET_ARM ? 254 :
        Configuration.TARGET_XARCH ? 127 :
        Configuration.TARGET_ARM64 ? 1048575 :
        UnsupportedTargetValue<int>("Short conditional branches are not supported for this target.");

    public static int JCC_DIST_MEDIUM_MAX_NEG =>
        Configuration.TARGET_ARM
            ? -1048576
            : UnsupportedTargetValue<int>("Medium conditional branches are not supported for this target.");

    public static int JCC_DIST_MEDIUM_MAX_POS =>
        Configuration.TARGET_ARM
            ? 1048574
            : UnsupportedTargetValue<int>("Medium conditional branches are not supported for this target.");

    public static int JMP_SIZE_SMALL =>
        (Configuration.TARGET_ARM || Configuration.TARGET_XARCH) ? 2 :
        Configuration.TARGET_ARM64 ? 4 :
        UnsupportedTargetValue<int>("Short unconditional branches are not supported for this target.");

    public static int JMP_SIZE_LARGE =>
        Configuration.TARGET_ARM ? 4 :
        Configuration.TARGET_XARCH ? 5 :
        UnsupportedTargetValue<int>("Long unconditional branches are not supported for this target.");

    public static int JCC_SIZE_SMALL =>
        (Configuration.TARGET_ARM || Configuration.TARGET_XARCH) ? 2 :
        Configuration.TARGET_ARM64 ? 4 :
        UnsupportedTargetValue<int>("Short conditional branches are not supported for this target.");

    public static int JCC_SIZE_LARGE =>
        (Configuration.TARGET_ARM || Configuration.TARGET_XARCH) ? 6 :
        Configuration.TARGET_ARM64 ? 8 :
        UnsupportedTargetValue<int>("Long conditional branches are not supported for this target.");

    public static int JCC_SIZE_MEDIUM =>
        Configuration.TARGET_ARM
            ? 4
            : UnsupportedTargetValue<int>("Medium conditional branches are not supported for this target.");

    public static int PUSH_INST_SIZE =>
        Configuration.TARGET_XARCH
            ? 5
            : UnsupportedTargetValue<int>("Push instruction sizes are not defined for this target.");

    public static int CALL_INST_SIZE =>
        Configuration.TARGET_XARCH
            ? 5
            : UnsupportedTargetValue<int>("Call instruction sizes are not defined for this target.");

    private static T UnsupportedTargetValue<T>(string message)
    {
        throw new FatalJitException(message);
    }

    public const int BITS_PER_BYTE = 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compFeatureVarArg()
    {
        // Native Varargs are not supported on Unix (all architectures) and Windows ARM
        return TargetOS.IsWindows && !TargetArchitecture.IsArm32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compAppleArm64Abi()
    {
        return TargetArchitecture.IsArm64 && TargetOS.IsApplePlatform;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compFeatureArgSplit()
    {
        return TargetArchitecture.IsLoongArch64
            || TargetArchitecture.IsArm32
            || TargetArchitecture.IsRiscV64
            || (TargetArchitecture.IsArm64 && TargetOS.IsWindows);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compUnixX86Abi()
    {
        return TargetArchitecture.IsX86 && TargetOS.IsUnix;
    }

    /// <summary>Return true if the register number is valid</summary>
    /// <param name="reg"></param>
    /// <returns></returns>
    public static bool genIsValidReg(regNumber reg)
    {
        return Configuration.TARGET_WASM
            ? regNumberExtensions.IsValidWasmReg(reg)
            : reg < regNumberExtensions.NotAvailable;
    }

    public static bool genIsValidFloatReg(regNumber reg)
    {
        return Configuration.TARGET_WASM
            ? regNumberExtensions.IsValidWasmFloatReg(reg)
            : reg.IsFltReg;
    }

    public static bool genIsValidIntReg(regNumber reg)
    {
        return Configuration.TARGET_WASM
            ? regNumberExtensions.IsValidWasmIntReg(reg)
            : reg.IsIntReg;
    }

    public static bool genIsValidIntOrFakeReg(regNumber reg)
    {
        return genIsValidIntReg(reg) || (Configuration.TARGET_ARM64 && (reg is REG_ARM64_SP));
    }

    public static bool genIsValidDoubleReg(regNumber reg) =>
        regNumberExtensions.IsValidArmDoubleRegister(reg);

    public static bool genIsValidMaskReg(regNumber reg) =>
        Configuration.FEATURE_MASKED_HW_INTRINSICS && reg.IsMskReg;
}
