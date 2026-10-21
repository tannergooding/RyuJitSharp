// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerSIMD(GenTree simdNode)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD lowering was dispatched for another target.");
            return;
        }

        NYI_RISCV64("LowerSIMD");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD lowering is not ported.");
        return;
    }

    private void ContainCheckSIMD(GenTree simdNode)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD containment was dispatched for another target.");
            return;
        }

        NYI_RISCV64("ContainCheckSIMD");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD containment is not ported.");
        return;
    }

    private bool IsValidConstForMovImmRiscV64(GenTreeHWIntrinsic node)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "RISC-V64 hardware-intrinsic immediate materialization was dispatched incorrectly.");
            return default!;
        }

        NYI_RISCV64("IsValidConstForMovImm");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "RISC-V64 hardware-intrinsic immediate materialization is not ported.");
        return default!;
    }

    private GenTree? LowerHWIntrinsicCmpOpRiscV64(GenTreeHWIntrinsic node, genTreeOps cmpOp)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "RISC-V64 hardware-intrinsic comparison was dispatched incorrectly.");
            return default!;
        }

        NYI_RISCV64("LowerHWIntrinsicCmpOp");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V64 hardware-intrinsic comparisons are not ported.");
        return default!;
    }

    private void ContainCheckHWIntrinsicRiscV64(GenTreeHWIntrinsic node)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "RISC-V64 hardware-intrinsic containment was dispatched incorrectly.");
            return;
        }

        NYI_RISCV64("ContainCheckHWIntrinsic");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V64 hardware-intrinsic containment is not ported.");
        return;
    }
}
