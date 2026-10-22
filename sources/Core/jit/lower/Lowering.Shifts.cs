// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerRotate(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            ContainCheckShiftRotate(tree.AsOp());
            return;
        }

        if (Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64)
        {
            if (tree.Oper is GT_ROL)
            {
                var rotate = tree.AsOp();
                var rotatedValueBitSize = rotate.Op1.Type.Size * BITS_PER_BYTE;
                var rotateLeftIndexNode = rotate.Op2;
                if (rotateLeftIndexNode.Oper.IsCnsIntOrI)
                {
                    var constant = rotateLeftIndexNode.AsIntCon();
                    if (Configuration.TARGET_ARM)
                    {
                        constant.IconValue = unchecked((nint)(int)((uint)rotatedValueBitSize - (uint)constant.IconValue));
                    }
                    else
                    {
                        constant.IconValue = unchecked(rotatedValueBitSize - constant.IconValue);
                    }
                }
                else
                {
                    var negate = CompilerInstance.gtNewUnaryNode(GT_NEG, rotateLeftIndexNode.Type.ActualType,
                        rotateLeftIndexNode);
                    BlockRange().InsertAfter(rotateLeftIndexNode, negate);
                    rotate.Op2 = negate;
                }

                tree.SetOper(GT_ROR);
                tree.Flags &= GTF_COMMON_MASK;
            }

            ContainCheckShiftRotate(tree.AsOp());
            return;
        }

        ContainCheckShiftRotate(tree.AsOp());
    }

    private void ContainCheckShiftRotate(GenTreeOp node)
    {
        if (Configuration.TARGET_XARCH)
        {
            assert(node.Oper.IsShiftOrRotate);

            var source = node.Op1;
            var shiftBy = node.Op2;
            if (Configuration.TARGET_X86 && node.Oper.IsShiftLong)
            {
                assert(source.Oper is GT_LONG);
                MakeSrcContained(node, source);
            }

            if (IsContainableImmed(node, shiftBy) && (shiftBy.AsIntConCommon().IconValue <= 255) &&
                (shiftBy.AsIntConCommon().IconValue >= 0))
            {
                MakeSrcContained(node, shiftBy);
            }

            var canContainSource = !source.IsContained && (source.Type.Size >= node.Type.Size);
            if (canContainSource && ((node.Flags & GTF_SET_FLAGS) == 0) &&
                (shiftBy.IsContained != node.Oper.IsShift) &&
                CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
            {
                if (IsContainableMemoryOp(source) && IsSafeToContainMem(node, source))
                {
                    MakeSrcContained(node, source);
                }
                else if (IsSafeToMarkRegOptional(node, source))
                {
                    MakeSrcRegOptional(node, source);
                }
            }

            return;
        }

        if (Configuration.TARGET_ARM)
        {
            assert(node.Oper.IsShiftOrRotate);
            var source = node.Op1;
            var shiftBy = node.Op2;
            if (node.Oper is GT_LSH_HI or GT_RSH_LO)
            {
                assert(source.Oper is GT_LONG);
                MakeSrcContained(node, source);
            }

            if (shiftBy.Oper.IsCnsIntOrI)
            {
                MakeSrcContained(node, shiftBy);
            }

            return;
        }

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_RISCV64)
        {
            assert(node.Oper.IsShiftOrRotate);
            var shiftBy = node.Op2;
            if (shiftBy.Oper.IsCnsIntOrI)
            {
                MakeSrcContained(node, shiftBy);
            }

            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            assert(node.Oper.IsShiftOrRotate);
            if (node.Op2.Oper.IsCnsIntOrI)
            {
                MakeSrcContained(node, node.Op2);
            }

            return;
        }

        if (Configuration.TARGET_WASM)
        {
            // Wasm shifts and rotates do not contain operands.
            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.ShiftContainmentIsNotPortedForThisTarget);
        return;
    }
}
