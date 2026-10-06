// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if TARGET_RISCV64 && FEATURE_SIMD
    private void LowerSIMD(GenTreeSIMD simdNode)
    {
        NYI_RISCV64("LowerSIMD");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD lowering is not ported.");
    }

    private void ContainCheckSIMD(GenTreeSIMD simdNode)
    {
        NYI_RISCV64("ContainCheckSIMD");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD containment is not ported.");
    }
#endif

#if TARGET_RISCV64 && FEATURE_HW_INTRINSICS
    private bool IsValidConstForMovImm(GenTreeHWIntrinsic node)
    {
        NYI_RISCV64("IsValidConstForMovImm");
        throw new FatalJitException(CORJIT_SKIPPED,
            "RISC-V64 hardware-intrinsic immediate materialization is not ported.");
    }

    private GenTree? LowerHWIntrinsicCmpOp(GenTreeHWIntrinsic node, genTreeOps cmpOp)
    {
        NYI_RISCV64("LowerHWIntrinsicCmpOp");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 hardware-intrinsic comparisons are not ported.");
    }

    private void ContainCheckHWIntrinsic(GenTreeHWIntrinsic node)
    {
        NYI_RISCV64("ContainCheckHWIntrinsic");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 hardware-intrinsic containment is not ported.");
    }
#endif
}
