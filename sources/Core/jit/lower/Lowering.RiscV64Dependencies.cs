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
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64SIMDLoweringWasDispatchedForAnotherTarget
    );
            return;
        }

        NYI_RISCV64("LowerSIMD");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.RISCV64SIMDLoweringIsNotPorted);
        return;
    }

    private void ContainCheckSIMD(GenTree simdNode)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64SIMDContainmentWasDispatchedForAnotherTarget
    );
            return;
        }

        NYI_RISCV64("ContainCheckSIMD");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.RISCV64SIMDContainmentIsNotPorted);
        return;
    }

    private bool IsValidConstForMovImmRiscV64(GenTreeHWIntrinsic node)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64HardwareIntrinsicImmediateMaterializationWasDispatchedIncorrectly
    );
            return default!;
        }

        NYI_RISCV64("IsValidConstForMovImm");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64HardwareIntrinsicImmediateMaterializationIsNotPorted
    );
        return default!;
    }

    private GenTree? LowerHWIntrinsicCmpOpRiscV64(GenTreeHWIntrinsic node, genTreeOps cmpOp)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64HardwareIntrinsicComparisonWasDispatchedIncorrectly
    );
            return default!;
        }

        NYI_RISCV64("LowerHWIntrinsicCmpOp");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64HardwareIntrinsicComparisonsAreNotPorted
    );
        return default!;
    }

    private void ContainCheckHWIntrinsicRiscV64(GenTreeHWIntrinsic node)
    {
        if (!Configuration.TARGET_RISCV64 || !Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64HardwareIntrinsicContainmentWasDispatchedIncorrectly
    );
            return;
        }

        NYI_RISCV64("ContainCheckHWIntrinsic");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCV64HardwareIntrinsicContainmentIsNotPorted
    );
        return;
    }
}
