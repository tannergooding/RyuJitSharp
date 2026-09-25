// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private void ContainCheckHWIntrinsicQuaternary(GenTreeHWIntrinsic node, NamedIntrinsic intrinsicId,
        HWIntrinsicCategory category, bool isContainedImm)
    {
        if (category is not HW_Category_IMM)
        {
            throw new System.InvalidOperationException($"Unexpected quaternary hardware intrinsic category: {category}.");
        }

        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);
        var op4 = node.GetOp(4);

        switch (intrinsicId)
        {
            case NI_AVX512_Fixup:
            case NI_AVX512_FixupScalar:
            {
                if (!isContainedImm)
                {
                    break;
                }
                TryMakeSrcContainedOrRegOptional(node, op3);
                if (!node.IsRmwHWIntrinsic(CompilerInstance))
                {
                    assert(op1 is GenTreeVecCon);
                    MakeSrcContained(node, op1);
                }
                break;
            }

            case NI_AVX512_TernaryLogic:
            {
#if DEBUG
                assert(CompilerInstance.canUseEvexEncodingDebugOnly());
#endif
                if (!isContainedImm)
                {
                    break;
                }

                const byte A = 0xF0;
                const byte B = 0xCC;
                const byte C = 0xAA;
                var immediate = op4.AsIntCon();
                var control = unchecked((byte)immediate.IconValue);
                var info = TernaryLogicInfo.Lookup(control);
                var useFlags = info.GetAllUseFlags();
                var supportsOp1RegOptional = false;
                bool supportsOp2RegOptional;
                bool supportsOp3RegOptional;
                GenTree? containedOperand = null;
                GenTree? regOptionalOperand = null;
                var swapOperands = TernaryLogicUseFlags.None;

                switch (useFlags)
                {
                    case TernaryLogicUseFlags.None:
                    {
                        break;
                    }

                    case TernaryLogicUseFlags.C:
                    {
                        assert(op1 is GenTreeVecCon);
                        MakeSrcContained(node, op1);
                        assert(op2 is GenTreeVecCon);
                        MakeSrcContained(node, op2);
                        if (IsContainableHWIntrinsicOp(node, op3, out supportsOp3RegOptional))
                        {
                            containedOperand = op3;
                        }
                        else if (supportsOp3RegOptional)
                        {
                            regOptionalOperand = op3;
                        }
                        break;
                    }

                    case TernaryLogicUseFlags.BC:
                    {
                        assert(op1 is GenTreeVecCon);
                        MakeSrcContained(node, op1);
                        if (IsContainableHWIntrinsicOp(node, op3, out supportsOp3RegOptional))
                        {
                            containedOperand = op3;
                        }
                        else if (IsContainableHWIntrinsicOp(node, op2, out supportsOp2RegOptional))
                        {
                            containedOperand = op2;
                            swapOperands = TernaryLogicUseFlags.BC;
                        }
                        else
                        {
                            if (supportsOp2RegOptional)
                            {
                                regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op2);
                            }
                            if (supportsOp3RegOptional)
                            {
                                regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op3);
                            }
                            if (ReferenceEquals(regOptionalOperand, op2))
                            {
                                swapOperands = TernaryLogicUseFlags.BC;
                            }
                        }
                        break;
                    }

                    case TernaryLogicUseFlags.ABC:
                    {
                        GenTree? user = null;
                        if (BlockRange().TryGetUse(node, out var use))
                        {
                            user = use.User();
                        }
                        var resultOpNum = node.GetResultOpNumForRmwIntrinsic(user, op1, op2, op3);
                        if (resultOpNum is 2)
                        {
                            node.SetOp(1, op2);
                            node.SetOp(2, op1);
                            (op1, op2) = (op2, op1);
                            control = TernaryLogicInfo.GetTernaryControlByte(info, B, A, C);
                            immediate.IconValue = control;
                            resultOpNum = 1;
                            info = TernaryLogicInfo.Lookup(control);
                        }
                        else if (resultOpNum is 3)
                        {
                            node.SetOp(1, op3);
                            node.SetOp(3, op1);
                            (op1, op3) = (op3, op1);
                            control = TernaryLogicInfo.GetTernaryControlByte(info, C, B, A);
                            immediate.IconValue = control;
                            resultOpNum = 1;
                            info = TernaryLogicInfo.Lookup(control);
                        }

                        if (IsContainableHWIntrinsicOp(node, op3, out supportsOp3RegOptional))
                        {
                            containedOperand = op3;
                        }
                        else if (IsContainableHWIntrinsicOp(node, op2, out supportsOp2RegOptional))
                        {
                            containedOperand = op2;
                            swapOperands = TernaryLogicUseFlags.BC;
                        }
                        else if ((resultOpNum != 1) &&
                            IsContainableHWIntrinsicOp(node, op1, out supportsOp1RegOptional))
                        {
                            containedOperand = op1;
                            swapOperands = TernaryLogicUseFlags.AC;
                        }
                        else
                        {
                            if (supportsOp1RegOptional)
                            {
                                regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op1);
                            }
                            if (supportsOp2RegOptional)
                            {
                                regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op2);
                            }
                            if (supportsOp3RegOptional)
                            {
                                regOptionalOperand = PreferredRegOptionalOperand(regOptionalOperand, op3);
                            }
                            if (ReferenceEquals(regOptionalOperand, op1))
                            {
                                swapOperands = TernaryLogicUseFlags.AC;
                            }
                            else if (ReferenceEquals(regOptionalOperand, op2))
                            {
                                swapOperands = TernaryLogicUseFlags.BC;
                            }
                        }
                        break;
                    }

                    default:
                    {
                        throw new System.InvalidOperationException(
                            $"Ternary logic control {control:X2} was not normalized before containment.");
                    }
                }

                if (containedOperand is not null)
                {
                    if (containedOperand is GenTreeVecCon vecCon &&
                        node.IsEmbeddedBroadcastCompatibleHWIntrinsic(CompilerInstance))
                    {
                        TryFoldCnsVecForEmbeddedBroadcast(node, vecCon);
                    }
                    else
                    {
                        ContainHWIntrinsicOperand(node, containedOperand);
                    }
                }
                else if (regOptionalOperand is not null)
                {
                    MakeSrcRegOptional(node, regOptionalOperand);
                }

                if (swapOperands is TernaryLogicUseFlags.AC)
                {
                    var currentOp1 = node.GetOp(1);
                    node.SetOp(1, node.GetOp(3));
                    node.SetOp(3, currentOp1);
                    control = TernaryLogicInfo.GetTernaryControlByte(info, C, B, A);
                    immediate.IconValue = control;
                }
                else if (swapOperands is TernaryLogicUseFlags.BC)
                {
                    var currentOp2 = node.GetOp(2);
                    node.SetOp(2, node.GetOp(3));
                    node.SetOp(3, currentOp2);
                    control = TernaryLogicInfo.GetTernaryControlByte(info, A, C, B);
                    immediate.IconValue = control;
                }
                break;
            }

            default:
            {
                throw new System.InvalidOperationException(
                    $"Unhandled quaternary immediate hardware intrinsic: {intrinsicId}.");
            }
        }
    }
#endif
}
