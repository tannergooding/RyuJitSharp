// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerBswapOp(GenTreeUnOp node)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.TARGET_XARCH)
        {
            return;
        }

        assert(node.Oper is GT_BSWAP or GT_BSWAP16);
        if (!CompilerInstance.opts.OptimizationEnabled ||
            !CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            return;
        }

        var operand = node.Op1;
        var swapSize = node.Oper is GT_BSWAP16 ? 2 : node.Type.Size;
        if ((swapSize == operand.Type.Size) && IsContainableMemoryOp(operand) && IsSafeToContainMem(node, operand))
        {
            MakeSrcContained(node, operand);
        }
    }

    private void ContainCheckIntrinsic(GenTreeIntrinsic node)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Scalar intrinsic containment is not ported for this target.");
            return;
        }

        assert(node.Oper is GT_INTRINSIC);
        if (node.IntrinsicName is NI_System_Math_Ceiling or NI_System_Math_Floor or
            NI_System_Math_Truncate or NI_System_Math_Round or NI_System_Math_Sqrt)
        {
            var operand = node.Op1;
            if (operand.IsCnsNonZeroFltOrDbl)
            {
                MakeSrcContained(node, operand);
            }
            else
            {
                TryMakeSrcContainedOrRegOptional(node, operand);
            }
        }
    }
}
