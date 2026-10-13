// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class Globals
{
    public static int TARGET_POINTER_SIZE => Configuration.TARGET_POINTER_SIZE;

    public static int REGSIZE_BYTES => Configuration.REGSIZE_BYTES;

    public static int MAX_MULTIREG_COUNT => Configuration.MAX_MULTIREG_COUNT;

    public static int MAX_REG_ARG => Configuration.MAX_REG_ARG;

    public static int MAX_RET_REG_COUNT => Configuration.MAX_RET_REG_COUNT;

    public static regMask SRBM_ALLMASK_INIT => TargetRuntimeMetadata.Select(
        SRBM_ALLMASK_INIT_X86,
        SRBM_ALLMASK_INIT_AMD64,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE);

    public static regMask SRBM_ALLMASK_EVEX => TargetRuntimeMetadata.Select(
        SRBM_ALLMASK_EVEX_X86,
        SRBM_ALLMASK_EVEX_AMD64,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE);

    public static regMask SRBM_MSK_CALLEE_TRASH_INIT => TargetRuntimeMetadata.Select(
        SRBM_MSK_CALLEE_TRASH_INIT_X86,
        SRBM_MSK_CALLEE_TRASH_INIT_AMD64,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE);

    public static regMask SRBM_MSK_CALLEE_TRASH_EVEX => TargetRuntimeMetadata.Select(
        SRBM_MSK_CALLEE_TRASH_EVEX_X86,
        SRBM_MSK_CALLEE_TRASH_EVEX_AMD64,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE,
        SRBM_NONE);

    public static regMask SRBM_INTRET => TargetRuntimeMetadata.IntegerReturnMask;

    public static regMask SRBM_LNGRET => TargetRuntimeMetadata.LongReturnMask;

    public static regMask SRBM_FLOATRET => TargetRuntimeMetadata.FloatingReturnMask;

    public static regMask SRBM_DOUBLERET => TargetRuntimeMetadata.DoubleReturnMask;

    public static regMask SRBM_FLT_CALLEE_SAVED => TargetRuntimeMetadata.FloatingCalleeSavedMask;

    public static regMask SRBM_INT_CALLEE_SAVED => TargetRuntimeMetadata.IntegerCalleeSavedMask;

    public static regMask SRBM_MSK_CALLEE_SAVED => TargetRuntimeMetadata.MaskCalleeSavedMask;

    public static regMask SRBM_ENC_CALLEE_SAVED => TargetRuntimeMetadata.EncodedCalleeSavedMask;

    public static regMask SRBM_WRITE_BARRIER_DST => TargetRuntimeMetadata.Select(
        SRBM_WRITE_BARRIER_DST_X86,
        SRBM_WRITE_BARRIER_DST_AMD64,
        SRBM_WRITE_BARRIER_DST_ARM64,
        SRBM_WRITE_BARRIER_DST_ARM,
        SRBM_WRITE_BARRIER_DST_LOONGARCH64,
        SRBM_WRITE_BARRIER_DST_RISCV64,
        SRBM_NONE);

    public static regMask SRBM_WRITE_BARRIER_SRC => TargetRuntimeMetadata.Select(
        SRBM_WRITE_BARRIER_SRC_X86,
        SRBM_WRITE_BARRIER_SRC_AMD64,
        SRBM_WRITE_BARRIER_SRC_ARM64,
        SRBM_WRITE_BARRIER_SRC_ARM,
        SRBM_WRITE_BARRIER_SRC_LOONGARCH64,
        SRBM_WRITE_BARRIER_SRC_RISCV64,
        SRBM_NONE);

    public static regNumber REG_INT_FIRST => TargetRuntimeMetadata.IntegerRegisterFirst;

    public static regNumber REG_FP_FIRST => TargetRuntimeMetadata.FloatingRegisterFirst;

    public static regNumber REG_FP_LAST => TargetRuntimeMetadata.FloatingRegisterLast;

    public static regNumber REG_MASK_FIRST => TargetRuntimeMetadata.MaskRegisterFirst;

    public static regNumber REG_MASK_LAST => TargetRuntimeMetadata.MaskRegisterLast;

    public static regNumber REG_ARG_0 => TargetRuntimeMetadata.FirstIntegerArgumentRegister;

    public static regNumber REG_ARG_1 => TargetRuntimeMetadata.SecondIntegerArgumentRegister;

    public static regNumber REG_INTRET => TargetRuntimeMetadata.IntegerReturnRegister;

    public static regNumber REG_ASYNC_CONTINUATION_RET => TargetRuntimeMetadata.AsyncContinuationReturnRegister;

    public static regNumber REG_DEFAULT_HELPER_CALL_TARGET => TargetRuntimeMetadata.DefaultHelperCallTargetRegister;

    public static regNumber REG_DISPATCH_INDIRECT_CALL_ADDR => TargetRuntimeMetadata.DispatchIndirectCallAddressRegister;

    public static regNumber REG_FPBASE => TargetRuntimeMetadata.FramePointerBaseRegister;

    public static regNumber REG_R2R_INDIRECT_PARAM => TargetRuntimeMetadata.R2RIndirectParameterRegister;

    public static regNumber REG_SCRATCH => TargetRuntimeMetadata.ScratchRegister;

    public static regNumber REG_SECRET_STUB_PARAM => TargetRuntimeMetadata.SecretStubParameterRegister;

    public static regNumber REG_SPBASE => TargetRuntimeMetadata.StackPointerBaseRegister;

    public static regNumber REG_STACK_PROBE_HELPER_ARG => TargetRuntimeMetadata.StackProbeHelperArgumentRegister;

    public static regNumber REG_WRITE_BARRIER_DST => TargetRuntimeMetadata.WriteBarrierDestinationRegister;

    public static regNumber REG_WRITE_BARRIER_SRC => TargetRuntimeMetadata.WriteBarrierSourceRegister;

    public static string STR_SPBASE => TargetRuntimeMetadata.StackPointerBaseName;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_VAR_ORDER_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_VAR_ORDER_AMD64;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_VAR_ORDER_ARM64;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_VAR_ORDER_ARM;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_VAR_ORDER_LOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_VAR_ORDER_RISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_VAR_ORDER_WASM;
            }

            throw new FatalJitException("Register order is unavailable for this target.");
        }
    }

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_VAR_ORDER_FLT_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return Configuration.FEATURE_MASKED_HW_INTRINSICS
                    ? REG_VAR_ORDER_FLT_EVEX
                    : REG_VAR_ORDER_FLT_AMD64;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_VAR_ORDER_FLT_ARM64;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_VAR_ORDER_FLT_ARM;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_VAR_ORDER_FLT_LOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_VAR_ORDER_FLT_RISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_VAR_ORDER_FLT_WASM;
            }

            throw new FatalJitException("Floating-point register order is unavailable for this target.");
        }
    }

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_MSK
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_VAR_ORDER_MSK_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_VAR_ORDER_MSK_AMD64;
            }

            if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
            {
                return [];
            }

            throw new FatalJitException("Mask-register allocation order is unavailable for this target.");
        }
    }
}
