// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private GenTree? LowerHWIntrinsicTernaryLogic(GenTreeHWIntrinsic node)
    {
#if DEBUG
        assert(CompilerInstance.canUseEvexEncodingDebugOnly());
#endif
        const byte A = 0xF0;
        const byte B = 0xCC;
        const byte C = 0xAA;

        var simdType = node.Type;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);

        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);
        var op4 = node.GetOp(4);

        if (op4.Oper.IsCnsIntOrI)
        {
            var immediate = op4.AsIntConCommon();
            var control = unchecked((byte)immediate.IconValue);
            var info = TernaryLogicInfo.Lookup(control);
            var useFlags = info.GetAllUseFlags();

            switch (control)
            {
                case 0xAC:
                case 0xE4:
                case 0xE2:
                case 0xCA:
                case 0xD8:
                case 0xB8:
                {
                    assert(info.Oper1 is TernaryLogicOperKind.Select);
                    assert(info.Oper2 is TernaryLogicOperKind.Select);
                    assert(info.Oper3 is TernaryLogicOperKind.Cond);
                    GenTree condition;
                    GenTree selectTrue;
                    GenTree selectFalse;

                    if (info.Oper1Use is TernaryLogicUseFlags.A)
                    {
                        selectTrue = op1;
                        if (info.Oper2Use is TernaryLogicUseFlags.B)
                        {
                            selectFalse = op2;
                            condition = op3;
                        }
                        else
                        {
                            assert(info.Oper2Use is TernaryLogicUseFlags.C);
                            selectFalse = op3;
                            condition = op2;
                        }
                    }
                    else if (info.Oper1Use is TernaryLogicUseFlags.B)
                    {
                        selectTrue = op2;
                        if (info.Oper2Use is TernaryLogicUseFlags.A)
                        {
                            selectFalse = op1;
                            condition = op3;
                        }
                        else
                        {
                            assert(info.Oper2Use is TernaryLogicUseFlags.C);
                            selectFalse = op3;
                            condition = op1;
                        }
                    }
                    else
                    {
                        assert(info.Oper1Use is TernaryLogicUseFlags.C);
                        selectTrue = op3;
                        if (info.Oper2Use is TernaryLogicUseFlags.A)
                        {
                            selectFalse = op1;
                            condition = op2;
                        }
                        else
                        {
                            assert(info.Oper2Use is TernaryLogicUseFlags.B);
                            selectFalse = op2;
                            condition = op1;
                        }
                    }

                    if (condition is GenTreeHWIntrinsic conversion &&
                        conversion.HWIntrinsicId is NI_AVX512_ConvertMaskToVector)
                    {
                        var mask = conversion.GetOp(1);
                        if (mask is not GenTreeHWIntrinsic)
                        {
                            break;
                        }
                        BlockRange().Remove(conversion);
                        condition = mask;
                    }
                    else if (!varTypeIsMask(condition.Type))
                    {
                        if (condition is not GenTreeHWIntrinsic comparison)
                        {
                            break;
                        }

                        var maskComparisonId = comparison.HWIntrinsicId switch
                        {
                            NI_AVX_Compare => NI_AVX512_CompareMask,
                            NI_X86Base_CompareEqual or NI_AVX_CompareEqual or NI_AVX2_CompareEqual =>
                                NI_AVX512_CompareEqualMask,
                            NI_X86Base_CompareGreaterThan or NI_AVX_CompareGreaterThan or NI_AVX2_CompareGreaterThan =>
                                NI_AVX512_CompareGreaterThanMask,
                            NI_X86Base_CompareGreaterThanOrEqual or NI_AVX_CompareGreaterThanOrEqual =>
                                NI_AVX512_CompareGreaterThanOrEqualMask,
                            NI_X86Base_CompareLessThan or NI_AVX_CompareLessThan or NI_AVX2_CompareLessThan =>
                                NI_AVX512_CompareLessThanMask,
                            NI_X86Base_CompareLessThanOrEqual or NI_AVX_CompareLessThanOrEqual =>
                                NI_AVX512_CompareLessThanOrEqualMask,
                            NI_X86Base_CompareNotEqual or NI_AVX_CompareNotEqual =>
                                NI_AVX512_CompareNotEqualMask,
                            NI_X86Base_CompareNotGreaterThan or NI_AVX_CompareNotGreaterThan =>
                                NI_AVX512_CompareNotGreaterThanMask,
                            NI_X86Base_CompareNotGreaterThanOrEqual or NI_AVX_CompareNotGreaterThanOrEqual =>
                                NI_AVX512_CompareNotGreaterThanOrEqualMask,
                            NI_X86Base_CompareNotLessThan or NI_AVX_CompareNotLessThan =>
                                NI_AVX512_CompareNotLessThanMask,
                            NI_X86Base_CompareNotLessThanOrEqual or NI_AVX_CompareNotLessThanOrEqual =>
                                NI_AVX512_CompareNotLessThanOrEqualMask,
                            NI_X86Base_CompareOrdered or NI_AVX_CompareOrdered =>
                                NI_AVX512_CompareOrderedMask,
                            NI_X86Base_CompareUnordered or NI_AVX_CompareUnordered =>
                                NI_AVX512_CompareUnorderedMask,
                            _ => NI_Illegal,
                        };
                        if (maskComparisonId is NI_Illegal)
                        {
                            assert(!HWIntrinsicInfo.ReturnsPerElementMask(comparison.HWIntrinsicId));
                            break;
                        }
                        comparison.Type = TYP_MASK;
                        comparison.ChangeHWIntrinsicId(maskComparisonId);
                    }

                    assert(varTypeIsMask(condition.Type));
                    if (condition is not GenTreeHWIntrinsic maskIntrinsic)
                    {
                        break;
                    }
                    node.SimdBaseType = maskIntrinsic.SimdBaseType;
                    node.ResetHWIntrinsicId(NI_AVX512_BlendVariableMask, selectFalse, selectTrue, condition);
                    BlockRange().Remove(op4);
                    break;
                }

                default:
                {
                    switch (useFlags)
                    {
                        case TernaryLogicUseFlags.A:
                        {
                            SwapTernaryOperands(node, 1, 3);
                            control = TernaryLogicInfo.GetTernaryControlByte(info, C, B, A);
                            immediate.IconValue = control;
                            useFlags = TernaryLogicUseFlags.C;
                            break;
                        }
                        case TernaryLogicUseFlags.B:
                        {
                            SwapTernaryOperands(node, 2, 3);
                            control = TernaryLogicInfo.GetTernaryControlByte(info, A, C, B);
                            immediate.IconValue = control;
                            useFlags = TernaryLogicUseFlags.C;
                            break;
                        }
                        case TernaryLogicUseFlags.AB:
                        {
                            SwapTernaryOperands(node, 2, 3);
                            SwapTernaryOperands(node, 1, 2);
                            control = TernaryLogicInfo.GetTernaryControlByte(info, B, C, A);
                            immediate.IconValue = control;
                            useFlags = TernaryLogicUseFlags.BC;
                            break;
                        }
                        case TernaryLogicUseFlags.AC:
                        {
                            SwapTernaryOperands(node, 1, 2);
                            control = TernaryLogicInfo.GetTernaryControlByte(info, B, A, C);
                            immediate.IconValue = control;
                            useFlags = TernaryLogicUseFlags.BC;
                            break;
                        }
                    }

                    op1 = node.GetOp(1);
                    op2 = node.GetOp(2);
                    op3 = node.GetOp(3);
                    GenTree? replacement = null;

                    switch (useFlags)
                    {
                        case TernaryLogicUseFlags.None:
                        {
                            op1.IsUnusedValue = true;
                            op2.IsUnusedValue = true;
                            op3.IsUnusedValue = true;
                            replacement = control is 0
                                ? CompilerInstance.gtNewZeroConNode(simdType)
                                : CompilerInstance.gtNewAllBitsSetConNode(simdType);
                            assert(control is 0 or 0xFF);
                            BlockRange().InsertBefore(node, replacement);
                            break;
                        }

                        case TernaryLogicUseFlags.C:
                        {
                            if (control is C)
                            {
                                op1.IsUnusedValue = true;
                                op2.IsUnusedValue = true;
                                replacement = op3;
                                break;
                            }
                            if (op1 is not GenTreeVecCon)
                            {
                                op1.IsUnusedValue = true;
                                op1 = CompilerInstance.gtNewZeroConNode(simdType);
                                BlockRange().InsertBefore(node, op1);
                                node.SetOp(1, op1);
                            }
                            if (op2 is not GenTreeVecCon)
                            {
                                op2.IsUnusedValue = true;
                                op2 = CompilerInstance.gtNewZeroConNode(simdType);
                                BlockRange().InsertBefore(node, op2);
                                node.SetOp(2, op2);
                            }
                            break;
                        }

                        case TernaryLogicUseFlags.BC:
                        {
                            if (op1 is not GenTreeVecCon)
                            {
                                op1.IsUnusedValue = true;
                                op1 = CompilerInstance.gtNewZeroConNode(simdType);
                                BlockRange().InsertBefore(node, op1);
                                node.SetOp(1, op1);
                            }
                            break;
                        }

                        default:
                        {
                            assert(useFlags is TernaryLogicUseFlags.ABC);
                            break;
                        }
                    }

                    if (replacement is not null)
                    {
                        if (BlockRange().TryGetUse(node, out var use))
                        {
                            use.ReplaceWith(replacement);
                        }
                        else
                        {
                            replacement.IsUnusedValue = true;
                        }
                        var next = node.Next;
                        BlockRange().Remove(op4);
                        BlockRange().Remove(node);
                        return next;
                    }
                    break;
                }
            }
        }

        ContainCheckHWIntrinsic(node);
        return node.Next;
    }

    private static void SwapTernaryOperands(GenTreeHWIntrinsic node, int first, int second)
    {
        var operand = node.GetOp(first);
        node.SetOp(first, node.GetOp(second));
        node.SetOp(second, operand);
    }
#endif
}
