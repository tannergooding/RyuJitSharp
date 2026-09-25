// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerHWIntrinsicCC(GenTreeHWIntrinsic node, NamedIntrinsic newIntrinsicId, GenCondition condition)
    {
#if TARGET_XARCH
        var cc = LowerNodeCC(node, condition);
        assert((HWIntrinsicInfo.lookupNumArgs(newIntrinsicId) == 2) || (newIntrinsicId is NI_AVX512_KORTEST));
        node.ChangeHWIntrinsicId(newIntrinsicId);
        node.Type = TYP_VOID;
        node.IsUnusedValue = false;

        var swapOperands = false;
        var canSwapOperands = false;
        switch (newIntrinsicId)
        {
            case NI_X86Base_COMIS:
            case NI_X86Base_UCOMIS:
            case NI_AVX10v1_VCOMISH:
            case NI_AVX10v1_VUCOMISH:
            {
                // Avoiding an extra parity branch takes precedence over containing a memory operand.
                if ((cc is not null) && cc.Condition.PreferSwap)
                {
                    swapOperands = true;
                }
                else
                {
                    canSwapOperands = (cc is null) || !GenCondition.Swap(cc.Condition).PreferSwap;
                }
                break;
            }

            case NI_X86Base_PTEST:
            case NI_AVX_PTEST:
            {
                // Carry-based tests are not symmetric.
                canSwapOperands = (cc is null) || (cc.Condition.Code is GenCondition.EQ or GenCondition.NE);
                break;
            }

            case NI_AVX512_KORTEST:
            case NI_AVX512_KTEST:
            {
                break;
            }

            default:
            {
                throw new FatalJitException("Condition-code lowering requires a flag-producing hardware intrinsic.");
            }
        }

        if (canSwapOperands &&
            !IsContainableHWIntrinsicOp(node, node.GetOp(2), out _) &&
            IsContainableHWIntrinsicOp(node, node.GetOp(1), out _))
        {
            swapOperands = true;
        }

        if (swapOperands)
        {
            var operand = node.GetOp(1);
            node.SetOp(1, node.GetOp(2));
            node.SetOp(2, operand);
            cc?.Condition = GenCondition.Swap(cc.Condition);
        }
#else
        throw new NotImplementedException("Non-xarch hardware condition-code lowering is not ported.");
#endif
    }
}
