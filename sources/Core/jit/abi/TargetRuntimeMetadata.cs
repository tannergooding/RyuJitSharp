// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

internal static class TargetRuntimeMetadata
{
    internal static CorInfoArch CorInfoArchitecture => Select(
        CORINFO_ARCH_TARGET_X86,
        CORINFO_ARCH_TARGET_AMD64,
        CORINFO_ARCH_TARGET_ARM64,
        CORINFO_ARCH_TARGET_ARM,
        CORINFO_ARCH_TARGET_LOONGARCH64,
        CORINFO_ARCH_TARGET_RISCV64,
        CORINFO_ARCH_TARGET_WASM);

    internal static int CodeAlignment => Select(
        CODE_ALIGN_X86,
        CODE_ALIGN_AMD64,
        CODE_ALIGN_ARM64,
        CODE_ALIGN_ARM,
        CODE_ALIGN_LOONGARCH64,
        CODE_ALIGN_RISCV64,
        CODE_ALIGN_WASM);

    internal static regNumber FirstRegister => Select(
        REG_FIRST_X86,
        REG_FIRST_AMD64,
        REG_FIRST_ARM64,
        REG_FIRST_ARM,
        REG_FIRST_LOONGARCH64,
        REG_FIRST_RISCV64,
        REG_FIRST_WASM);

    internal static regNumber FloatingRegisterFirst => Select(
        REG_FP_FIRST_X86,
        REG_FP_FIRST_AMD64,
        REG_FP_FIRST_ARM64,
        REG_FP_FIRST_ARM,
        REG_FP_FIRST_LOONGARCH64,
        REG_FP_FIRST_RISCV64,
        REG_FP_FIRST_WASM);

    internal static regNumber FloatingRegisterLast => Select(
        REG_FP_LAST_X86,
        REG_FP_LAST_AMD64,
        REG_FP_LAST_ARM64,
        REG_FP_LAST_ARM,
        REG_FP_LAST_LOONGARCH64,
        REG_FP_LAST_RISCV64,
        REG_FP_LAST_WASM);

    internal static regNumber IntegerRegisterFirst => Select(
        REG_INT_FIRST_X86,
        REG_INT_FIRST_AMD64,
        REG_INT_FIRST_ARM64,
        REG_INT_FIRST_ARM,
        REG_INT_FIRST_LOONGARCH64,
        REG_INT_FIRST_RISCV64,
        REG_INT_FIRST_WASM);

    internal static regNumber IntegerRegisterLast => Select(
        REG_INT_LAST_X86,
        REG_INT_LAST_AMD64,
        REG_INT_LAST_ARM64,
        REG_INT_LAST_ARM,
        REG_INT_LAST_LOONGARCH64,
        REG_INT_LAST_RISCV64,
        REG_INT_LAST_WASM);

    internal static regNumber ScratchRegister => Select(
        REG_SCRATCH_X86,
        REG_SCRATCH_AMD64,
        REG_SCRATCH_ARM64,
        REG_SCRATCH_ARM,
        REG_SCRATCH_LOONGARCH64,
        REG_SCRATCH_RISCV64,
        REG_SCRATCH_WASM);

    internal static regNumber Register0 => Select(
        REG_X86_EAX,
        REG_X64_EAX,
        REG_ARM64_R0,
        REG_ARM32_R0,
        REG_LOONGARCH64_R0,
        REG_RISCV64_R0,
        REG_WASM_NA);

    internal static regNumber FramePointerRegister => Select(
        REG_X86_EBP,
        REG_X64_EBP,
        REG_ARM64_FP,
        REG_ARM32_R11,
        REG_LOONGARCH64_FP,
        REG_RISCV64_FP,
        REG_WASM_NA);

    internal static regNumber IntegerReturnRegister => Select(
        REG_INTRET_X86,
        REG_INTRET_AMD64,
        REG_INTRET_ARM64,
        REG_INTRET_ARM,
        REG_INTRET_LOONGARCH64,
        REG_INTRET_RISCV64,
        REG_INTRET_WASM);

    internal static regNumber IntegerReturnRegister1 => Select(
        REG_X86_EDX,
        REG_INTRET_1_AMD64,
        REG_INTRET_1_ARM64,
        REG_ARM32_R1,
        REG_INTRET_1_LOONGARCH64,
        REG_INTRET_1_RISCV64,
        REG_WASM_NA);

    internal static regNumber LongReturnLowRegister => Select(
        REG_LNGRET_LO_X86,
        REG_INTRET_AMD64,
        REG_INTRET_ARM64,
        REG_LNGRET_LO_ARM,
        REG_INTRET_LOONGARCH64,
        REG_INTRET_RISCV64,
        REG_WASM_NA);

    internal static regNumber LongReturnHighRegister => Select(
        REG_LNGRET_HI_X86,
        REG_INTRET_1_AMD64,
        REG_INTRET_1_ARM64,
        REG_LNGRET_HI_ARM,
        REG_INTRET_1_LOONGARCH64,
        REG_INTRET_1_RISCV64,
        REG_WASM_NA);

    internal static regNumber FloatingReturnRegister => Select(
        REG_X86_XMM0,
        REG_X64_XMM0,
        REG_ARM64_V0,
        REG_ARM32_F0,
        REG_LOONGARCH64_F0,
        REG_RISCV64_FA0,
        REG_WASM_NA);

    internal static regNumber FloatingReturnRegister1 => Select(
        REG_X86_XMM1,
        REG_X64_XMM1,
        REG_ARM64_V1,
        REG_ARM32_F1,
        REG_LOONGARCH64_F1,
        REG_RISCV64_FA1,
        REG_WASM_NA);

    internal static regNumber FirstIntegerArgumentRegister => Select(
        REG_ARG_0_X86,
        Configuration.UNIX_AMD64_ABI ? REG_X64_EDI : REG_X64_ECX,
        REG_ARG_0_ARM64,
        REG_ARG_0_ARM,
        REG_ARG_0_LOONGARCH64,
        REG_ARG_0_RISCV64,
        REG_ARG_0_WASM);

    internal static regNumber SecondIntegerArgumentRegister => Select(
        REG_ARG_1_X86,
        Configuration.UNIX_AMD64_ABI ? REG_X64_ESI : REG_X64_EDX,
        REG_ARG_1_ARM64,
        REG_ARG_1_ARM,
        REG_ARG_1_LOONGARCH64,
        REG_ARG_1_RISCV64,
        REG_WASM_NA);

    internal static regNumber LastIntegerArgumentRegister => Select(
        REG_X86_EDX,
        REG_X64_R9,
        REG_ARM64_R7,
        REG_ARM32_R3,
        REG_LOONGARCH64_A7,
        REG_RISCV64_A7,
        REG_WASM_NA);

    internal static regNumber FirstFloatingArgumentRegister => Select(
        FIRST_FP_ARGREG_X86,
        FIRST_FP_ARGREG_AMD64,
        FIRST_FP_ARGREG_ARM64,
        FIRST_FP_ARGREG_ARM,
        FIRST_FP_ARGREG_LOONGARCH64,
        FIRST_FP_ARGREG_RISCV64,
        FIRST_FP_ARGREG_WASM);

    internal static regNumber LastFloatingArgumentRegister => Select(
        LAST_FP_ARGREG_X86,
        LAST_FP_ARGREG_AMD64,
        LAST_FP_ARGREG_ARM64,
        LAST_FP_ARGREG_ARM,
        LAST_FP_ARGREG_LOONGARCH64,
        LAST_FP_ARGREG_RISCV64,
        LAST_FP_ARGREG_WASM);

    internal static regNumber ThirdIntegerArgumentRegister => Select(
        REG_X86_NA,
        Configuration.UNIX_AMD64_ABI ? REG_X64_RDX : REG_X64_R8,
        REG_ARM64_R2,
        REG_ARM32_R2,
        REG_LOONGARCH64_A2,
        REG_RISCV64_A2,
        REG_WASM_NA);

    internal static regNumber LinkRegister
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_LR;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_ARM32_LR;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LinkRegisterIsNotAvailableForThisTarget);
            return default!;
        }
    }

    internal static regNumber AsyncContinuationReturnRegister => Select(
        REG_ASYNC_CONTINUATION_RET_X86,
        REG_ASYNC_CONTINUATION_RET_AMD64,
        REG_ASYNC_CONTINUATION_RET_ARM64,
        REG_ASYNC_CONTINUATION_RET_ARM,
        REG_ASYNC_CONTINUATION_RET_LOONGARCH64,
        REG_ASYNC_CONTINUATION_RET_RISCV64,
        REG_WASM_NA);

    internal static regNumber R2RIndirectParameterRegister => Select(
        REG_R2R_INDIRECT_PARAM_X86,
        REG_R2R_INDIRECT_PARAM_AMD64,
        REG_R2R_INDIRECT_PARAM_ARM64,
        REG_R2R_INDIRECT_PARAM_ARM,
        REG_R2R_INDIRECT_PARAM_LOONGARCH64,
        REG_R2R_INDIRECT_PARAM_RISCV64,
        REG_R2R_INDIRECT_PARAM_WASM);

    internal static regNumber SecretStubParameterRegister => Select(
        REG_SECRET_STUB_PARAM_X86,
        REG_SECRET_STUB_PARAM_AMD64,
        REG_SECRET_STUB_PARAM_ARM64,
        REG_SECRET_STUB_PARAM_ARM,
        REG_SECRET_STUB_PARAM_LOONGARCH64,
        REG_SECRET_STUB_PARAM_RISCV64,
        REG_SECRET_STUB_PARAM_WASM);

    internal static regNumber WriteBarrierDestinationRegister => Select(
        REG_WRITE_BARRIER_DST_X86,
        REG_WRITE_BARRIER_DST_AMD64,
        REG_WRITE_BARRIER_DST_ARM64,
        REG_WRITE_BARRIER_DST_ARM,
        REG_WRITE_BARRIER_DST_LOONGARCH64,
        REG_WRITE_BARRIER_DST_RISCV64,
        REG_WASM_NA);

    internal static regNumber WriteBarrierSourceRegister => Select(
        REG_WRITE_BARRIER_SRC_X86,
        REG_WRITE_BARRIER_SRC_AMD64,
        REG_WRITE_BARRIER_SRC_ARM64,
        REG_WRITE_BARRIER_SRC_ARM,
        REG_WRITE_BARRIER_SRC_LOONGARCH64,
        REG_WRITE_BARRIER_SRC_RISCV64,
        REG_WASM_NA);

    internal static regNumber PInvokeFrameRegister => Select(
        REG_PINVOKE_FRAME_X86,
        REG_PINVOKE_FRAME_AMD64,
        REG_PINVOKE_FRAME_ARM64,
        REG_PINVOKE_FRAME_ARM,
        REG_PINVOKE_FRAME_LOONGARCH64,
        REG_PINVOKE_FRAME_RISCV64,
        REG_PINVOKE_FRAME_WASM);

    internal static regNumber LongArgumentLowRegister => Select(
        REG_LNGARG_LO_X86,
        REG_LNGARG_LO_AMD64,
        REG_LNGARG_LO_ARM64,
        REG_LNGARG_LO_ARM,
        REG_LNGARG_LO_LOONGARCH64,
        REG_LNGARG_LO_RISCV64,
        REG_LNGARG_LO_WASM);

    internal static regNumber LongArgumentHighRegister => Select(
        REG_LNGARG_HI_X86,
        REG_LNGARG_HI_AMD64,
        REG_LNGARG_HI_ARM64,
        REG_LNGARG_HI_ARM,
        REG_LNGARG_HI_LOONGARCH64,
        REG_LNGARG_HI_RISCV64,
        REG_LNGARG_HI_WASM);

    internal static regNumber DefaultHelperCallTargetRegister => Select(
        REG_X86_EAX,
        REG_DEFAULT_HELPER_CALL_TARGET_AMD64,
        REG_DEFAULT_HELPER_CALL_TARGET_ARM64,
        REG_DEFAULT_HELPER_CALL_TARGET_ARM,
        REG_DEFAULT_HELPER_CALL_TARGET_LOONGARCH64,
        REG_DEFAULT_HELPER_CALL_TARGET_RISCV64,
        REG_WASM_NA);

    internal static regMaskTP IntegerReturnRegisterMask => Globals.genRegMask(IntegerReturnRegister);

    internal static regMaskTP Register0Mask => Globals.genRegMask(Register0);

    internal static regMaskTP IntegerReturnRegister1Mask => Globals.genRegMask(IntegerReturnRegister1);

    internal static regMask IntegerReturnMask => IntegerReturnRegisterMask.Lower;

    internal static regMask FloatingReturnMask => Globals.genRegMask(FloatingReturnRegister).Lower;

    internal static regMask DoubleReturnMask => Select(
        SRBM_X86_XMM0,
        SRBM_X64_XMM0,
        SRBM_ARM64_V0,
        SRBM_ARM32_F0 | SRBM_ARM32_F1,
        SRBM_LOONGARCH64_F0,
        SRBM_RISCV64_FA0,
        SRBM_NONE);

    internal static regMask FirstIntegerArgumentMask => Globals.genRegMask(FirstIntegerArgumentRegister).Lower;

    internal static regMask SecondIntegerArgumentMask => Globals.genRegMask(SecondIntegerArgumentRegister).Lower;

    internal static regMask ThirdIntegerArgumentMask => Globals.genRegMask(ThirdIntegerArgumentRegister).Lower;

    internal static regMask LinkRegisterMask => Globals.genRegMask(LinkRegister).Lower;

    internal static regMask LongReturnMask
    {
        get
        {
            if (Configuration.TARGET_WASM)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LongReturnRegistersAreNotAvailableForThisTarget);
                return default!;
            }

            var mask = Globals.genRegMask(LongReturnLowRegister).Lower;
            if (!Configuration.TARGET_64BIT)
            {
                mask |= Globals.genRegMask(LongReturnHighRegister).Lower;
            }

            return mask;
        }
    }

    internal static regMaskTP AsyncContinuationReturnRegisterMask => Globals.genRegMask(AsyncContinuationReturnRegister);

    internal static regMask SwiftErrorMask => Globals.genRegMask(SwiftErrorRegister).Lower;

    internal static regMask IntegerCalleeSavedMask => Select(
        SRBM_INT_CALLEE_SAVED_X86,
        SRBM_INT_CALLEE_SAVED_AMD64,
        SRBM_INT_CALLEE_SAVED_ARM64,
        SRBM_INT_CALLEE_SAVED_ARM,
        SRBM_INT_CALLEE_SAVED_LOONGARCH64,
        SRBM_INT_CALLEE_SAVED_RISCV64,
        SRBM_INT_CALLEE_SAVED_WASM);

    internal static regMask FloatingCalleeSavedMask => Select(
        SRBM_FLT_CALLEE_SAVED_X86,
        SRBM_FLT_CALLEE_SAVED_AMD64,
        SRBM_FLT_CALLEE_SAVED_ARM64,
        SRBM_FLT_CALLEE_SAVED_ARM,
        SRBM_FLT_CALLEE_SAVED_LOONGARCH64,
        SRBM_FLT_CALLEE_SAVED_RISCV64,
        SRBM_FLT_CALLEE_SAVED_WASM);

    internal static regMask MaskCalleeSavedMask
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return SRBM_MSK_CALLEE_SAVED_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return SRBM_MSK_CALLEE_SAVED_AMD64;
            }

            if (Configuration.TARGET_ARM64)
            {
                return SRBM_MSK_CALLEE_SAVED_ARM64;
            }

            return SRBM_NONE;
        }
    }

    internal static regMask EncodedCalleeSavedMask
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return Configuration.UNIX_AMD64_ABI ? SRBM_NONE : SRBM_X64_RSI | SRBM_X64_RDI;
            }

            if (Configuration.TARGET_ARM64)
            {
                return SRBM_ENC_CALLEE_SAVED_ARM64;
            }

            if (Configuration.TARGET_X86 || Configuration.TARGET_ARM ||
                Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64 ||
                Configuration.TARGET_WASM)
            {
                return SRBM_NONE;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.EncodedCalleeSavedRegistersAreNotDefinedForThisTarget);
            return default!;
        }
    }

    internal static ReadOnlySpan<regNumber> SwiftFloatReturnRegisters
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return [REG_ARM64_V0, REG_ARM64_V1, REG_ARM64_V2, REG_ARM64_V3];
            }

            if (Configuration.TARGET_AMD64 && Configuration.TARGET_UNIX)
            {
                return [REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3];
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftReturnRegistersAreNotSupportedForThisTarget);
            return default!;
        }
    }

    internal static ReadOnlySpan<regNumber> SwiftIntegerReturnRegisters
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return [REG_ARM64_R0, REG_ARM64_R1, REG_ARM64_R2, REG_ARM64_R3];
            }

            if (Configuration.TARGET_AMD64 && Configuration.TARGET_UNIX)
            {
                return [REG_X64_RAX, REG_X64_RDX, REG_X64_RCX, REG_X64_R8];
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftReturnRegistersAreNotSupportedForThisTarget);
            return default!;
        }
    }

    internal static regNumber StackProbeHelperArgumentRegister
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_STACK_PROBE_HELPER_ARG_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_STACK_PROBE_HELPER_ARG_AMD64;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_STACK_PROBE_HELPER_ARG_ARM;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.StackProbeHelperArgumentsAreNotSupportedForThisTarget);
            return default!;
        }
    }

    internal static regNumber ZeroRegister
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_ZR;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_LOONGARCH64_R0;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_RISCV64_ZERO;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ZeroRegisterIsNotAvailableForThisTarget);
            return default!;
        }
    }

    internal static regNumber MaskRegisterFirst
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_MASK_FIRST_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_MASK_FIRST_AMD64;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_MASK_FIRST_ARM64;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.MaskedRegistersAreNotSupportedForThisTarget);
            return default!;
        }
    }

    internal static regNumber MaskRegisterLast
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_MASK_LAST_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_MASK_LAST_AMD64;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_MASK_LAST_ARM64;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.MaskedRegistersAreNotSupportedForThisTarget);
            return default!;
        }
    }

    internal static regNumber FramePointerBaseRegister => Select(
        REG_FPBASE_X86,
        REG_FPBASE_AMD64,
        REG_FPBASE_ARM64,
        REG_FPBASE_ARM,
        REG_FPBASE_LOONGARCH64,
        REG_FPBASE_RISCV64,
        REG_FPBASE_WASM);

    internal static regNumber StackPointerBaseRegister => Select(
        REG_SPBASE_X86,
        REG_SPBASE_AMD64,
        REG_SPBASE_ARM64,
        REG_SPBASE_ARM,
        REG_SPBASE_LOONGARCH64,
        REG_SPBASE_RISCV64,
        REG_SPBASE_WASM);

    internal static regNumber NoRegister => Select(
        REG_X86_NA,
        REG_X64_NA,
        REG_ARM64_NA,
        REG_ARM32_NA,
        REG_LOONGARCH64_NA,
        REG_RISCV64_NA,
        REG_WASM_NA);

    internal static regNumber ValidateIndirectCallAddressRegister => Select(
        REG_X86_ECX,
        REG_VALIDATE_INDIRECT_CALL_ADDR_AMD64,
        REG_VALIDATE_INDIRECT_CALL_ADDR_ARM64,
        REG_ARM32_R0,
        REG_VALIDATE_INDIRECT_CALL_ADDR_LOONGARCH64,
        REG_VALIDATE_INDIRECT_CALL_ADDR_RISCV64,
        REG_WASM_NA);

    internal static regNumber DispatchIndirectCallAddressRegister => Select(
        REG_X86_NA,
        REG_DISPATCH_INDIRECT_CALL_ADDR_AMD64,
        REG_DISPATCH_INDIRECT_CALL_ADDR_ARM64,
        REG_ARM32_NA,
        REG_DISPATCH_INDIRECT_CALL_ADDR_LOONGARCH64,
        REG_DISPATCH_INDIRECT_CALL_ADDR_RISCV64,
        REG_WASM_NA);

    internal static string FramePointerBaseName => Select(
        STR_FPBASE_X86,
        STR_FPBASE_AMD64,
        STR_FPBASE_ARM64,
        STR_FPBASE_ARM,
        STR_FPBASE_LOONGARCH64,
        STR_FPBASE_RISCV64,
        STR_FPBASE_WASM);

    internal static string StackPointerBaseName => Select(
        STR_SPBASE_X86,
        STR_SPBASE_AMD64,
        STR_SPBASE_ARM64,
        STR_SPBASE_ARM,
        STR_SPBASE_LOONGARCH64,
        STR_SPBASE_RISCV64,
        STR_SPBASE_WASM);

    internal static regNumber SwiftSelfRegister
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return REG_SWIFT_SELF_ARM64;
            }

            if (Configuration.TARGET_AMD64 && Configuration.TARGET_UNIX)
            {
                return REG_SWIFT_SELF_AMD64;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIRegistersAreNotSupportedForThisTarget);
            return default!;
        }
    }

    internal static regNumber SwiftErrorRegister
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return REG_SWIFT_ERROR_ARM64;
            }

            if (Configuration.TARGET_AMD64 && Configuration.TARGET_UNIX)
            {
                return REG_SWIFT_ERROR_AMD64;
            }

            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIRegistersAreNotSupportedForThisTarget);
            return default!;
        }
    }

    internal static T SelectXarch<T>(T x86, T amd64)
    {
        if (Configuration.TARGET_X86)
        {
            return x86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return amd64;
        }

        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.XarchMetadataIsUnavailableForThisArchitecture);
        return default!;
    }

    internal static T Select<T>(T x86, T amd64, T arm64, T arm, T loongArch64, T riscv64, T wasm)
    {
        if (Configuration.TARGET_X86)
        {
            return x86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return amd64;
        }

        if (Configuration.TARGET_ARM64)
        {
            return arm64;
        }

        if (Configuration.TARGET_ARM)
        {
            return arm;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return loongArch64;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return riscv64;
        }

        if (Configuration.TARGET_WASM)
        {
            return wasm;
        }

        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.TargetMetadataIsUnavailableForThisArchitecture);
        return default!;
    }
}
