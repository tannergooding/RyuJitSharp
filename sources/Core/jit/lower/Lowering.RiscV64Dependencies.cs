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
            throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD lowering was dispatched for another target.");
        }

        NYI_RISCV64("LowerSIMD");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD lowering is not ported.");
    }

    private void ContainCheckSIMD(GenTree simdNode)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD containment was dispatched for another target.");
        }

        NYI_RISCV64("ContainCheckSIMD");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD containment is not ported.");
    }

    private bool IsValidConstForMovImmRiscV64(GenTreeHWIntrinsic node)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "RISC-V64 hardware-intrinsic immediate materialization was dispatched incorrectly.");
        }

        NYI_RISCV64("IsValidConstForMovImm");
        throw new FatalJitException(CORJIT_SKIPPED,
            "RISC-V64 hardware-intrinsic immediate materialization is not ported.");
    }

    private GenTree? LowerHWIntrinsicCmpOpRiscV64(GenTreeHWIntrinsic node, genTreeOps cmpOp)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "RISC-V64 hardware-intrinsic comparison was dispatched incorrectly.");
        }

        NYI_RISCV64("LowerHWIntrinsicCmpOp");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 hardware-intrinsic comparisons are not ported.");
    }

    private void ContainCheckHWIntrinsicRiscV64(GenTreeHWIntrinsic node)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "RISC-V64 hardware-intrinsic containment was dispatched incorrectly.");
        }

        NYI_RISCV64("ContainCheckHWIntrinsic");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 hardware-intrinsic containment is not ported.");
    }
}
