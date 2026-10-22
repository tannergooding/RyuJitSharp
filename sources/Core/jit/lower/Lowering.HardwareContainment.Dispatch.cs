// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckHWIntrinsicXarch(GenTreeHWIntrinsic node)
    {
        var intrinsicId = node.HWIntrinsicId;
        var category = HWIntrinsicInfo.lookupCategory(intrinsicId);
        var numArgs = node.Operands.Length;
        var simdBaseType = node.SimdBaseType;
        var simdSize = (uint)node.SimdSize;

        if (!HWIntrinsicInfo.SupportsContainment(intrinsicId))
        {
            if (HWIntrinsicInfo.isAVX2GatherIntrinsic(intrinsicId))
            {
                MakeSrcContained(node, node.GetOp(numArgs));
            }
            return;
        }

        var isContainedImm = false;
        if (category is HW_Category_IMM)
        {
            var lastOp = node.GetOp(numArgs);
            if (HWIntrinsicInfo.isImmOp(intrinsicId, lastOp) && lastOp.Oper.IsCnsIntOrI)
            {
                MakeSrcContained(node, lastOp);
                isContainedImm = true;
            }
        }

        var isCommutative = node.IsCommutativeHWIntrinsic;
        switch (numArgs)
        {
            case 1:
            {
                assert(!isCommutative);
                ContainCheckHWIntrinsicUnary(node, intrinsicId, category, simdBaseType, simdSize);
                break;
            }

            case 2:
            {
                ContainCheckHWIntrinsicBinary(node, intrinsicId, category, simdBaseType, simdSize,
                    isCommutative, isContainedImm);
                break;
            }

            case 3:
            {
                assert(!isCommutative);
                ContainCheckHWIntrinsicTernary(node, intrinsicId, category, simdBaseType, simdSize,
                    isContainedImm);
                break;
            }

            case 4:
            {
                assert(!isCommutative);
                ContainCheckHWIntrinsicQuaternary(node, intrinsicId, category, isContainedImm);
                break;
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnexpectedHardwareIntrinsicOperandCountArgument, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? numArgs : default));
                return;
            }
        }
    }

    private void ContainCheckHWIntrinsicLoongArch64(GenTreeHWIntrinsic node)
    {
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64HardwareIntrinsicContainmentIsNotPorted
    );
        return;
    }

    private void ContainCheckHWIntrinsic(GenTreeHWIntrinsic node)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            return;
        }

        if (Configuration.TARGET_XARCH)
        {
            ContainCheckHWIntrinsicXarch(node);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            ContainCheckHWIntrinsicArm64(node);
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            ContainCheckHWIntrinsicWasm(node);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            ContainCheckHWIntrinsicLoongArch64(node);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            ContainCheckHWIntrinsicRiscV64(node);
            return;
        }

        // Targets without architecture-specific containment leave the node unchanged.
        return;
    }
}
