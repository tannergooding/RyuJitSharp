// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    private static T GetTargetRegisterValue<T>(T x86, T amd64, T arm, T arm64, T loongArch64, T riscv64, T wasm)
    {
        if (Configuration.TARGET_X86)
        {
            return x86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return amd64;
        }

        if (Configuration.TARGET_ARM)
        {
            return arm;
        }

        if (Configuration.TARGET_ARM64)
        {
            return arm64;
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

        return UnsupportedTargetValue<T>("Invalid target architecture.");
    }

    public static regMask SRBM_ALLINT => GetTargetRegisterValue(
        SRBM_ALLINT_X86,
        SRBM_ALLINT_INIT,
        SRBM_ALLINT_ARM,
        SRBM_ALLINT_ARM64,
        SRBM_ALLINT_LOONGARCH64,
        SRBM_ALLINT_RISCV64,
        SRBM_ALLINT_WASM);

    public static regMask SRBM_ALLFLOAT => GetTargetRegisterValue(
        SRBM_ALLFLOAT_X86,
        SRBM_ALLFLOAT_INIT,
        SRBM_ALLFLOAT_ARM,
        SRBM_ALLFLOAT_ARM64,
        SRBM_ALLFLOAT_LOONGARCH64,
        SRBM_ALLFLOAT_RISCV64,
        SRBM_ALLFLOAT_WASM);

    public static regMask SRBM_ALLDOUBLE => GetTargetRegisterValue(
        SRBM_ALLDOUBLE_X86,
        SRBM_ALLFLOAT_INIT,
        SRBM_ALLDOUBLE_ARM,
        SRBM_ALLDOUBLE_ARM64,
        SRBM_ALLDOUBLE_LOONGARCH64,
        SRBM_ALLDOUBLE_RISCV64,
        SRBM_ALLDOUBLE_WASM);

    public static regMask SRBM_CALLEE_SAVED =>
        TargetRuntimeMetadata.IntegerCalleeSavedMask | TargetRuntimeMetadata.FloatingCalleeSavedMask;

    public static regMaskTP RBM_ALLFLOAT => new(SRBM_ALLFLOAT);

    public static regMaskTP RBM_ALLINT => new(SRBM_ALLINT);

    public static regMaskTP RBM_ALLDOUBLE => new(SRBM_ALLDOUBLE);

    public static regMask SRBM_INT_CALLEE_TRASH => GetTargetRegisterValue(
        SRBM_INT_CALLEE_TRASH_X86,
        SRBM_INT_CALLEE_TRASH_INIT,
        SRBM_INT_CALLEE_TRASH_ARM,
        SRBM_INT_CALLEE_TRASH_ARM64,
        SRBM_INT_CALLEE_TRASH_LOONGARCH64,
        SRBM_INT_CALLEE_TRASH_RISCV64,
        SRBM_INT_CALLEE_TRASH_WASM);

    public static regMask SRBM_FLT_CALLEE_TRASH => GetTargetRegisterValue(
        SRBM_FLT_CALLEE_TRASH_X86,
        SRBM_FLT_CALLEE_TRASH_INIT,
        SRBM_FLT_CALLEE_TRASH_ARM,
        SRBM_FLT_CALLEE_TRASH_ARM64,
        SRBM_FLT_CALLEE_TRASH_LOONGARCH64,
        SRBM_FLT_CALLEE_TRASH_RISCV64,
        SRBM_FLT_CALLEE_TRASH_WASM);

    public static regMask SRBM_FPBASE => GetTargetRegisterValue(
        SRBM_FPBASE_X86,
        SRBM_FPBASE_AMD64,
        SRBM_FPBASE_ARM,
        SRBM_FPBASE_ARM64,
        SRBM_FPBASE_LOONGARCH64,
        SRBM_FPBASE_RISCV64,
        SRBM_FPBASE_WASM);

    public static int CNT_CALLEE_TRASH => GetTargetRegisterValue(
        CNT_CALLEE_TRASH_X86,
        CNT_CALLEE_TRASH_INT_INIT,
        CNT_CALLEE_TRASH_ARM,
        CNT_CALLEE_TRASH_ARM64,
        CNT_CALLEE_TRASH_LOONGARCH64,
        CNT_CALLEE_TRASH_RISCV64,
        CNT_CALLEE_TRASH_WASM);

    public static int CNT_CALLEE_TRASH_FLOAT => GetTargetRegisterValue(
        CNT_CALLEE_TRASH_FLOAT_X86,
        CNT_CALLEE_TRASH_FLOAT_INIT,
        CNT_CALLEE_TRASH_FLOAT_ARM,
        CNT_CALLEE_TRASH_FLOAT_ARM64,
        CNT_CALLEE_TRASH_FLOAT_LOONGARCH64,
        CNT_CALLEE_TRASH_FLOAT_RISCV64,
        CNT_CALLEE_TRASH_FLOAT_WASM);

    public static int CNT_CALLEE_TRASH_MASK => GetTargetRegisterValue(
        CNT_CALLEE_TRASH_MASK_INIT_X86,
        CNT_CALLEE_TRASH_MASK_INIT_AMD64,
        CNT_CALLEE_TRASH_MASK_ARM,
        CNT_CALLEE_TRASH_MASK_ARM64,
        CNT_CALLEE_TRASH_MASK_LOONGARCH64,
        CNT_CALLEE_TRASH_MASK_RISCV64,
        CNT_CALLEE_TRASH_MASK_WASM);

    public static int CNT_CALLEE_SAVED => Configuration.TARGET_AMD64
        ? CNT_CALLEE_SAVED_AMD64
        : GetTargetRegisterValue(
            CNT_CALLEE_SAVED_X86,
            0,
            CNT_CALLEE_SAVED_ARM,
            CNT_CALLEE_SAVED_ARM64,
            CNT_CALLEE_SAVED_LOONGARCH64,
            CNT_CALLEE_SAVED_RISCV64,
            CNT_CALLEE_SAVED_WASM);

    public static int CNT_CALLEE_SAVED_FLOAT => Configuration.TARGET_AMD64
        ? Configuration.UNIX_AMD64_ABI ? 0 : 10
        : GetTargetRegisterValue(
            CNT_CALLEE_SAVED_FLOAT_X86,
            0,
            CNT_CALLEE_SAVED_FLOAT_ARM,
            CNT_CALLEE_SAVED_FLOAT_ARM64,
            CNT_CALLEE_SAVED_FLOAT_LOONGARCH64,
            CNT_CALLEE_SAVED_FLOAT_RISCV64,
            CNT_CALLEE_SAVED_FLOAT_WASM);

    public static int CNT_CALLEE_SAVED_MASK => GetTargetRegisterValue(
        CNT_CALLEE_SAVED_MASK_X86,
        CNT_CALLEE_SAVED_MASK_AMD64,
        CNT_CALLEE_SAVED_MASK_ARM,
        CNT_CALLEE_SAVED_MASK_ARM64,
        CNT_CALLEE_SAVED_MASK_LOONGARCH64,
        CNT_CALLEE_SAVED_MASK_RISCV64,
        CNT_CALLEE_SAVED_MASK_WASM);

    public static int CNT_CALLEE_ENREG => Configuration.TARGET_AMD64
        ? CNT_CALLEE_SAVED
        : GetTargetRegisterValue(
            CNT_CALLEE_ENREG_X86,
            0,
            CNT_CALLEE_ENREG_ARM,
            CNT_CALLEE_ENREG_ARM64,
            CNT_CALLEE_ENREG_LOONGARCH64,
            CNT_CALLEE_ENREG_RISCV64,
            CNT_CALLEE_ENREG_WASM);

    public static int CNT_CALLEE_ENREG_FLOAT => CNT_CALLEE_SAVED_FLOAT;

    public static int CNT_CALLEE_ENREG_MASK => CNT_CALLEE_SAVED_MASK;

    public static int CNT_HIGHFLOAT => GetTargetRegisterValue(
        CNT_HIGHFLOAT_X86,
        CNT_HIGHFLOAT_AMD64,
        0,
        0,
        0,
        0,
        0);

    public static int CNT_MASK_REGS => GetTargetRegisterValue(
        CNT_MASK_REGS_X86,
        CNT_MASK_REGS_AMD64,
        0,
        0,
        0,
        0,
        0);

    public static uint CALLEE_SAVED_REG_MAXSZ =>
        unchecked((uint)(CNT_CALLEE_SAVED * Configuration.REGSIZE_BYTES));

    public static uint CALLEE_SAVED_FLOAT_MAXSZ => unchecked((uint)GetTargetRegisterValue(
        0,
        CALLEE_SAVED_FLOAT_MAXSZ_AMD64,
        CALLEE_SAVED_FLOAT_MAXSZ_ARM,
        CALLEE_SAVED_FLOAT_MAXSZ_ARM64,
        CALLEE_SAVED_FLOAT_MAXSZ_LOONGARCH64,
        CALLEE_SAVED_FLOAT_MAXSZ_RISCV64,
        0));

    public static int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES => GetTargetRegisterValue(
        STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_X86,
        STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_AMD64,
        STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_ARM,
        STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_ARM64,
        STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_LOONGARCH64,
        STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_RISCV64,
        0);
}
