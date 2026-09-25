// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerHWIntrinsic(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        if (node.Type is TYP_SIMD12)
        {
            node.Type = TYP_SIMD16;
        }

        var intrinsicId = node.HWIntrinsicId;
        if (node.IsEmbeddedRoundingEnabled)
        {
            var last = node.GetOp(node.Operands.Length);
            if (last.Oper.IsCnsIntOrI)
            {
                MakeSrcContained(node, last);
            }

            // Codegen consumes the rounding operand; ordinary containment expects the unrounded arity.
            return node.Next;
        }

        var operation = node.GetOperForHWIntrinsicId(out _);
        if (GenTreeHWIntrinsic.OperIsBitwiseHWIntrinsic(operation) && (node.Type is not TYP_MASK))
        {
            const byte A = 0xF0;
            const byte B = 0xCC;
            const byte C = 0xAA;
            var simdType = node.Type;
            var baseType = node.SimdBaseType;
            var size = node.SimdSize;
            var op1 = node.GetOp(1);
            var op2 = node.GetOp(2);
            GenTree op3;
            var isNot = (operation is GT_XOR) && op2.IsVectorAllBitsSet;

            if (BlockRange().TryGetUse(node, out var use) && use.User().Oper.IsHWIntrinsic)
            {
                var user = use.User().AsHWIntrinsic();
                var userOperation = user.GetOperForHWIntrinsicId(out _);
                baseType = user.SimdBaseType;
                if (GenTreeHWIntrinsic.OperIsBitwiseHWIntrinsic(userOperation))
                {
                    if (isNot && (userOperation is GT_AND))
                    {
                        var next = node.Next;
                        BlockRange().Remove(op2);
                        BlockRange().Remove(node);
                        op2 = user.GetOp(2);
                        if (op2 == node)
                        {
                            op2 = user.GetOp(1);
                        }

                        // Xarch AND_NOT complements its first operand, unlike the expression's name.
                        var id = CompilerInstance.GetHWIntrinsicIdForBinOp(GT_AND_NOT, op1, op2, baseType, size, false);
                        user.ResetHWIntrinsicId(id, op1, op2);
                        return next;
                    }

                    if (CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        var next = node.Next;
                        BlockRange().Remove(node);
                        op3 = user.GetOp(2);
                        var firstUse = TernaryLogicUseFlags.AB;
                        if (op3 == node)
                        {
                            if (userOperation is GT_AND_NOT)
                            {
                                op3 = op2;
                                op2 = op1;
                                op1 = user.GetOp(1);
                                firstUse = TernaryLogicUseFlags.BC;
                            }
                            else
                            {
                                op3 = user.GetOp(1);
                            }
                        }

                        byte control;
                        if ((userOperation is GT_XOR) && op3.IsVectorAllBitsSet)
                        {
                            assert(firstUse is TernaryLogicUseFlags.AB);
                            (op2, op3) = (op3, op2);
                            (op1, op2) = (op2, op1);
                            if (isNot)
                            {
                                assert(op1.IsVectorAllBitsSet && op3.IsVectorAllBitsSet);
                                if (BlockRange().TryGetUse(user, out var outerUse))
                                {
                                    outerUse.ReplaceWith(op2);
                                }
                                else
                                {
                                    op2.IsUnusedValue = true;
                                }

                                // Both nodes may be the saved continuation of this double negation.
                                if (next == op1)
                                {
                                    next = next.Next;
                                }
                                if (next == user)
                                {
                                    next = next.Next;
                                }

                                BlockRange().Remove(op3);
                                BlockRange().Remove(op1);
                                BlockRange().Remove(user);
                                return next;
                            }

                            assert(op1.IsVectorAllBitsSet);
                            control = unchecked((byte)~TernaryLogicInfo.GetTernaryControlByte(operation, B, C));
                        }
                        else if (isNot)
                        {
                            if (firstUse is TernaryLogicUseFlags.AB)
                            {
                                assert(op2.IsVectorAllBitsSet);
                                (op1, op2) = (op2, op1);
                                control = TernaryLogicInfo.GetTernaryControlByte(userOperation, unchecked((byte)~B), C);
                            }
                            else
                            {
                                assert(firstUse is TernaryLogicUseFlags.BC);
                                assert(op3.IsVectorAllBitsSet);
                                (op2, op3) = (op3, op2);
                                (op1, op2) = (op2, op1);
                                control = TernaryLogicInfo.GetTernaryControlByte(userOperation, B, unchecked((byte)~C));
                            }
                        }
                        else if (firstUse is TernaryLogicUseFlags.AB)
                        {
                            control = TernaryLogicInfo.GetTernaryControlByte(operation, A, B);
                            control = TernaryLogicInfo.GetTernaryControlByte(userOperation, control, C);
                        }
                        else
                        {
                            assert(firstUse is TernaryLogicUseFlags.BC);
                            control = TernaryLogicInfo.GetTernaryControlByte(operation, B, C);
                            control = TernaryLogicInfo.GetTernaryControlByte(userOperation, A, control);
                        }

                        var immediate = CompilerInstance.gtNewIconNode(TYP_INT, control);
                        BlockRange().InsertBefore(user, immediate);
                        user.ResetHWIntrinsicId(NI_AVX512_TernaryLogic, op1, op2, op3, immediate);
                        return next;
                    }
                }
            }

            if (isNot && CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX512))
            {
                BlockRange().Remove(op2);
                if (op1 is GenTreeHWIntrinsic { HWIntrinsicId: NI_AVX512_TernaryLogic } inner)
                {
                    var control = inner.GetOp(4);
                    if (control.Oper.IsCnsIntOrI)
                    {
                        var next = node.Next;
                        control.AsIntConCommon().IconValue = unchecked((byte)~control.AsIntConCommon().IconValue);
                        if (BlockRange().TryGetUse(node, out use))
                        {
                            use.ReplaceWith(op1);
                        }
                        else
                        {
                            op1.IsUnusedValue = true;
                        }

                        BlockRange().Remove(node);
                        return next;
                    }
                }

                op3 = op1;
                op2 = CompilerInstance.gtNewZeroConNode(simdType);
                BlockRange().InsertBefore(node, op2);
                op1 = CompilerInstance.gtNewZeroConNode(simdType);
                BlockRange().InsertBefore(node, op1);
                var immediate = CompilerInstance.gtNewIconNode(TYP_INT, unchecked((byte)~C));
                BlockRange().InsertBefore(node, immediate);
                node.ResetHWIntrinsicId(NI_AVX512_TernaryLogic, op1, op2, op3, immediate);
                return LowerNode(node);
            }
        }

        switch (intrinsicId)
        {
            case NI_Vector_ConditionalSelect:
            {
                return LowerHWIntrinsicCndSel(node);
            }
            case NI_Vector_Create:
            case NI_Vector_CreateScalar:
            {
                return LowerHWIntrinsicCreate(node);
            }
            case NI_Vector_Dot:
            {
                assert(node.SimdSize is 16 or 32);
                return LowerHWIntrinsicDot(node);
            }
            case NI_Vector_GetElement:
            {
                return LowerHWIntrinsicGetElement(node);
            }
            case NI_Vector_GetUpper:
            {
                var baseType = node.SimdBaseType;
                if (node.SimdSize == 32)
                {
                    intrinsicId = varTypeIsFloating(baseType) || !CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2)
                        ? NI_AVX_ExtractVector128 : NI_AVX2_ExtractVector128;
                }
                else
                {
                    assert(node.SimdSize == 64);
                    intrinsicId = NI_AVX512_ExtractVector256;
                }

                var value = node.GetOp(1);
                var index = CompilerInstance.gtNewIconNode(TYP_INT, 1);
                BlockRange().InsertBefore(node, index);
                _ = LowerNode(index);
                node.ResetHWIntrinsicId(intrinsicId, value, index);
                break;
            }
            case NI_Vector_WithElement:
            {
                return LowerHWIntrinsicWithElement(node);
            }
            case NI_Vector_WithLower:
            case NI_Vector_WithUpper:
            {
                var baseType = node.SimdBaseType;
                var index = intrinsicId is NI_Vector_WithUpper ? 1 : 0;
                if (node.SimdSize == 32)
                {
                    intrinsicId = varTypeIsFloating(baseType) || !CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2)
                        ? NI_AVX_InsertVector128 : NI_AVX2_InsertVector128;
                }
                else
                {
                    assert(node.SimdSize == 64);
                    intrinsicId = NI_AVX512_InsertVector256;
                }

                var first = node.GetOp(1);
                var second = node.GetOp(2);
                var immediate = CompilerInstance.gtNewIconNode(TYP_INT, index);
                BlockRange().InsertBefore(node, immediate);
                _ = LowerNode(immediate);
                node.ResetHWIntrinsicId(intrinsicId, first, second, immediate);
                break;
            }
            case NI_Vector_op_Equality:
            {
                return LowerHWIntrinsicCmpOp(node, GT_EQ);
            }
            case NI_Vector_op_Inequality:
            {
                return LowerHWIntrinsicCmpOp(node, GT_NE);
            }
            case NI_AVX512_Fixup:
            case NI_AVX512_FixupScalar:
            {
                if (!node.IsRmwHWIntrinsic(CompilerInstance))
                {
                    var operand = node.GetOp(1);
                    if (!operand.Oper.IsCnsVec)
                    {
                        operand.IsUnusedValue = true;
                        operand = CompilerInstance.gtNewZeroConNode(node.Type);
                        BlockRange().InsertBefore(node, operand);
                        node.SetOp(1, operand);
                    }
                }
                break;
            }
            case NI_AVX512_CompareEqualMask:
            case NI_AVX512_CompareNotEqualMask:
            {
                var first = node.GetOp(1);
                var second = node.GetOp(2);
                if (!varTypeIsFloating(node.SimdBaseType) && second.IsVectorZero)
                {
                    var testId = intrinsicId is NI_AVX512_CompareEqualMask ? NI_AVX512_PTESTNM : NI_AVX512_PTESTM;
                    BlockRange().Remove(second);
                    var firstUse = new LIR.Use(BlockRange(), ref node.GetOpRef(1), node);
                    _ = ReplaceWithLclVar(firstUse);
                    first = node.GetOp(1);
                    second = CompilerInstance.gtClone(first);
                    assert(second is not null);
                    BlockRange().InsertAfter(first, second);
                    node.SetOp(2, second);
                    node.ChangeHWIntrinsicId(testId);
                    return LowerNode(node);
                }
                break;
            }
            case NI_AVX512_AndMask:
            {
                var transform = false;
                var first = node.GetOp(1);
                var second = node.GetOp(2);
                if (first is GenTreeHWIntrinsic { HWIntrinsicId: NI_AVX512_NotMask } firstNot &&
                    (firstNot.SimdBaseType.Size == node.SimdBaseType.Size))
                {
                    transform = true;
                    first = firstNot.GetOp(1);
                    BlockRange().Remove(firstNot);
                }
                if (!transform && second is GenTreeHWIntrinsic { HWIntrinsicId: NI_AVX512_NotMask } secondNot &&
                    (secondNot.SimdBaseType.Size == node.SimdBaseType.Size))
                {
                    transform = true;
                    second = secondNot.GetOp(1);
                    BlockRange().Remove(secondNot);
                    (first, second) = (second, first);
                }
                if (transform)
                {
                    node.ChangeHWIntrinsicId(NI_AVX512_AndNotMask, first, second);
                }
                break;
            }
            case NI_AVX512_NotMask:
            {
                if (node.GetOp(1) is GenTreeHWIntrinsic { HWIntrinsicId: NI_AVX512_XorMask } inner &&
                    (inner.SimdBaseType.Size == node.SimdBaseType.Size))
                {
                    node.ResetHWIntrinsicId(NI_AVX512_XnorMask, inner.GetOp(1), inner.GetOp(2));
                    BlockRange().Remove(inner);
                }
                break;
            }
            case NI_Vector_ToScalar:
            {
                return LowerHWIntrinsicToScalar(node);
            }
            case NI_Vector_CreateScalarUnsafe:
            {
                // A floating scalar already occupies the low SIMD lane. Preserve its scalar type
                // and retain the wrapper for consumers that materialize its ABI-sized vector.
                if (varTypeIsFloating(node.SimdBaseType))
                {
                    var operand = node.GetOp(1);
                    var next = node.Next;
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        if (!use.User().Oper.IsHWIntrinsic)
                        {
                            break;
                        }
                        use.ReplaceWith(operand);
                    }
                    else
                    {
                        assert(node.IsUnusedValue);
                        node.IsUnusedValue = false;
                        operand.IsUnusedValue = true;
                    }

                    BlockRange().Remove(node);
                    return next;
                }
                break;
            }
            case NI_X86Base_Extract:
            {
                if (varTypeIsFloating(node.SimdBaseType))
                {
                    assert((node.SimdBaseType is TYP_FLOAT) && (node.SimdSize == 16));
                    var index = node.GetOp(2);
                    if (!index.Oper.IsConst)
                    {
                        var mask = CompilerInstance.gtNewIconNode(TYP_INT, 3);
                        BlockRange().InsertAfter(index, mask);
                        var maskedIndex = CompilerInstance.gtNewBinaryNode(GT_AND, TYP_INT, index, mask);
                        BlockRange().InsertAfter(mask, maskedIndex);
                        _ = LowerNode(maskedIndex);
                        node.SetOp(2, maskedIndex);
                    }
                    node.ChangeHWIntrinsicId(NI_Vector_GetElement);
                    return LowerNode(node);
                }
                break;
            }
            case NI_X86Base_Insert:
            {
                assert(node.Operands.Length == 3);
                if (node.SimdBaseType is not TYP_FLOAT)
                {
                    break;
                }

                var first = node.GetOp(1);
                var second = node.GetOp(2);
                var immediate = node.GetOp(3);
                var firstIsZero = first.IsVectorZero;
                var secondIsZero = second.IsVectorZero;
                if (firstIsZero && secondIsZero)
                {
                    first.Type = node.Type;
                    first.AsVecCon().SimdVal = default;
                    var next = node.Next;
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        use.ReplaceWith(first);
                    }
                    else
                    {
                        first.IsUnusedValue = true;
                    }
                    BlockRange().Remove(second);
                    immediate.IsUnusedValue = true;
                    BlockRange().Remove(node);
                    return next;
                }
                if (!immediate.Oper.IsCnsIntOrI)
                {
                    break;
                }

                // INSERTPS encodes the zero mask in bits 0-3, destination in 4-5,
                // and register-source lane in 6-7.
                var value = immediate.AsIntConCommon().IconValue;
                var zeroMask = value & 0x0F;
                var destination = (value & 0x30) >> 4;
                var source = (value & 0xC0) >> 6;
                if (firstIsZero)
                {
                    zeroMask = (zeroMask | ~((nint)1 << (int)destination)) & 0x0F;
                    value = (source << 6) | (destination << 4) | zeroMask;
                    immediate.AsIntConCommon().IconValue = value;
                }
                else if (secondIsZero)
                {
                    zeroMask = (zeroMask | ((nint)1 << (int)destination)) & 0x0F;
                    value = (source << 6) | (destination << 4) | zeroMask;
                    immediate.AsIntConCommon().IconValue = value;
                }

                if (zeroMask == 0x0F)
                {
                    var next = node.Next;
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        var zero = CompilerInstance.gtNewZeroConNode(TYP_SIMD16);
                        BlockRange().InsertBefore(node, zero);
                        use.ReplaceWith(zero);
                    }
                    first.IsUnusedValue = true;
                    second.IsUnusedValue = true;
                    immediate.IsUnusedValue = true;
                    BlockRange().Remove(node);
                    return next;
                }

                if ((source == 0) && second is GenTreeHWIntrinsic { HWIntrinsicId: NI_Vector_CreateScalarUnsafe } create &&
                    create.GetOp(1) is GenTreeHWIntrinsic extract && (extract.SimdBaseType.Size == 4))
                {
                    GenTree? sourceVector = null;
                    GenTree? sourceIndex = null;
                    nint newSource = 0;
                    if (extract.HWIntrinsicId is NI_Vector_ToScalar)
                    {
                        sourceVector = extract.GetOp(1);
                    }
                    else if ((extract.HWIntrinsicId is NI_Vector_GetElement) && extract.GetOp(2).Oper.IsCnsIntOrI)
                    {
                        sourceVector = extract.GetOp(1);
                        sourceIndex = extract.GetOp(2);
                        newSource = sourceIndex.AsIntConCommon().IconValue;
                    }

                    // Only low-128-bit register lanes fit count_s. Contained scalar
                    // loads already give the optimal m32 form and must not become vector loads.
                    if (sourceVector is not null && !sourceVector.IsContained &&
                        (newSource >= 0) && (newSource <= 3) && IsInvariantInRange(sourceVector, node))
                    {
                        source = newSource;
                        value = (source << 6) | (destination << 4) | zeroMask;
                        immediate.AsIntConCommon().IconValue = value;
                        node.SetOp(2, sourceVector);
                        second = sourceVector;
                        if (sourceIndex is not null)
                        {
                            BlockRange().Remove(sourceIndex);
                        }
                        BlockRange().Remove(extract);
                        BlockRange().Remove(create);
                    }
                }

                if (first is not GenTreeHWIntrinsic { HWIntrinsicId: NI_X86Base_Insert, SimdBaseType: TYP_FLOAT } inner)
                {
                    break;
                }
                var innerIndex = inner.GetOp(3);
                if (!innerIndex.Oper.IsCnsIntOrI || !IsInvariantInRange(first, node))
                {
                    break;
                }
                if (inner.GetOp(2).IsVectorZero)
                {
                    assert(inner.GetOp(2).IsContained);
                    var innerValue = innerIndex.AsIntConCommon().IconValue;
                    value |= (innerValue & 0x0F) & ~((nint)1 << (int)destination);
                    immediate.AsIntConCommon().IconValue = value;
                    node.SetOp(1, inner.GetOp(1));
                    BlockRange().Remove(inner.GetOp(2));
                    BlockRange().Remove(inner.GetOp(3));
                    BlockRange().Remove(inner);
                }
                else if (secondIsZero)
                {
                    value = innerIndex.AsIntConCommon().IconValue | zeroMask;
                    immediate.AsIntConCommon().IconValue = value;
                    node.SetOp(1, inner.GetOp(1));
                    node.SetOp(2, inner.GetOp(2));
                    BlockRange().Remove(second);
                    BlockRange().Remove(inner.GetOp(3));
                    BlockRange().Remove(inner);
                }
                break;
            }
            case NI_X86Base_CompareGreaterThan:
            case NI_X86Base_CompareGreaterThanOrEqual:
            case NI_X86Base_CompareNotGreaterThan:
            case NI_X86Base_CompareNotGreaterThanOrEqual:
            {
                if (!varTypeIsFloating(node.SimdBaseType))
                {
                    assert(varTypeIsIntegral(node.SimdBaseType));
                    break;
                }
                if (CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX))
                {
                    break;
                }

                var newId = intrinsicId switch {
                    NI_X86Base_CompareGreaterThan => NI_X86Base_CompareLessThan,
                    NI_X86Base_CompareGreaterThanOrEqual => NI_X86Base_CompareLessThanOrEqual,
                    NI_X86Base_CompareNotGreaterThan => NI_X86Base_CompareNotLessThan,
                    _ => NI_X86Base_CompareNotLessThanOrEqual,
                };
                node.ChangeHWIntrinsicId(newId);
                (node.GetOpRef(1), node.GetOpRef(2)) = (node.GetOp(2), node.GetOp(1));
                break;
            }
            case NI_X86Base_CompareLessThan:
            case NI_AVX2_CompareLessThan:
            {
                if (varTypeIsFloating(node.SimdBaseType))
                {
                    break;
                }
                assert(varTypeIsIntegral(node.SimdBaseType));
                node.ChangeHWIntrinsicId(intrinsicId is NI_X86Base_CompareLessThan
                    ? NI_X86Base_CompareGreaterThan : NI_AVX2_CompareGreaterThan);
                (node.GetOpRef(1), node.GetOpRef(2)) = (node.GetOp(2), node.GetOp(1));
                break;
            }
            case NI_X86Base_CompareScalarOrderedEqual:
            case NI_X86Base_CompareScalarOrderedNotEqual:
            case NI_X86Base_CompareScalarOrderedLessThan:
            case NI_X86Base_CompareScalarOrderedLessThanOrEqual:
            case NI_X86Base_CompareScalarOrderedGreaterThan:
            case NI_X86Base_CompareScalarOrderedGreaterThanOrEqual:
            case NI_X86Base_CompareScalarUnorderedEqual:
            case NI_X86Base_CompareScalarUnorderedNotEqual:
            case NI_X86Base_CompareScalarUnorderedLessThan:
            case NI_X86Base_CompareScalarUnorderedLessThanOrEqual:
            case NI_X86Base_CompareScalarUnorderedGreaterThan:
            case NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual:
            case NI_AVX10v1_CompareScalarOrderedEqual:
            case NI_AVX10v1_CompareScalarOrderedNotEqual:
            case NI_AVX10v1_CompareScalarOrderedLessThan:
            case NI_AVX10v1_CompareScalarOrderedLessThanOrEqual:
            case NI_AVX10v1_CompareScalarOrderedGreaterThan:
            case NI_AVX10v1_CompareScalarOrderedGreaterThanOrEqual:
            case NI_AVX10v1_CompareScalarUnorderedEqual:
            case NI_AVX10v1_CompareScalarUnorderedNotEqual:
            case NI_AVX10v1_CompareScalarUnorderedLessThan:
            case NI_AVX10v1_CompareScalarUnorderedLessThanOrEqual:
            case NI_AVX10v1_CompareScalarUnorderedGreaterThan:
            case NI_AVX10v1_CompareScalarUnorderedGreaterThanOrEqual:
            {
                var (compare, condition) = intrinsicId switch {
                    NI_X86Base_CompareScalarOrderedEqual => (NI_X86Base_COMIS, GenCondition.FEQ),
                    NI_X86Base_CompareScalarOrderedNotEqual => (NI_X86Base_COMIS, GenCondition.FNEU),
                    NI_X86Base_CompareScalarOrderedLessThan => (NI_X86Base_COMIS, GenCondition.FLT),
                    NI_X86Base_CompareScalarOrderedLessThanOrEqual => (NI_X86Base_COMIS, GenCondition.FLE),
                    NI_X86Base_CompareScalarOrderedGreaterThan => (NI_X86Base_COMIS, GenCondition.FGT),
                    NI_X86Base_CompareScalarOrderedGreaterThanOrEqual => (NI_X86Base_COMIS, GenCondition.FGE),
                    NI_X86Base_CompareScalarUnorderedEqual => (NI_X86Base_UCOMIS, GenCondition.FEQ),
                    NI_X86Base_CompareScalarUnorderedNotEqual => (NI_X86Base_UCOMIS, GenCondition.FNEU),
                    NI_X86Base_CompareScalarUnorderedLessThan => (NI_X86Base_UCOMIS, GenCondition.FLT),
                    NI_X86Base_CompareScalarUnorderedLessThanOrEqual => (NI_X86Base_UCOMIS, GenCondition.FLE),
                    NI_X86Base_CompareScalarUnorderedGreaterThan => (NI_X86Base_UCOMIS, GenCondition.FGT),
                    NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual => (NI_X86Base_UCOMIS, GenCondition.FGE),
                    NI_AVX10v1_CompareScalarOrderedEqual => (NI_AVX10v1_VCOMISH, GenCondition.FEQ),
                    NI_AVX10v1_CompareScalarOrderedNotEqual => (NI_AVX10v1_VCOMISH, GenCondition.FNEU),
                    NI_AVX10v1_CompareScalarOrderedLessThan => (NI_AVX10v1_VCOMISH, GenCondition.FLT),
                    NI_AVX10v1_CompareScalarOrderedLessThanOrEqual => (NI_AVX10v1_VCOMISH, GenCondition.FLE),
                    NI_AVX10v1_CompareScalarOrderedGreaterThan => (NI_AVX10v1_VCOMISH, GenCondition.FGT),
                    NI_AVX10v1_CompareScalarOrderedGreaterThanOrEqual => (NI_AVX10v1_VCOMISH, GenCondition.FGE),
                    NI_AVX10v1_CompareScalarUnorderedEqual => (NI_AVX10v1_VUCOMISH, GenCondition.FEQ),
                    NI_AVX10v1_CompareScalarUnorderedNotEqual => (NI_AVX10v1_VUCOMISH, GenCondition.FNEU),
                    NI_AVX10v1_CompareScalarUnorderedLessThan => (NI_AVX10v1_VUCOMISH, GenCondition.FLT),
                    NI_AVX10v1_CompareScalarUnorderedLessThanOrEqual => (NI_AVX10v1_VUCOMISH, GenCondition.FLE),
                    NI_AVX10v1_CompareScalarUnorderedGreaterThan => (NI_AVX10v1_VUCOMISH, GenCondition.FGT),
                    NI_AVX10v1_CompareScalarUnorderedGreaterThanOrEqual => (NI_AVX10v1_VUCOMISH, GenCondition.FGE),
                    _ => throw new FatalJitException("Unexpected scalar comparison intrinsic."),
                };
                LowerHWIntrinsicCC(node, compare, new GenCondition(condition));
                break;
            }
            case NI_X86Base_TestC:
            case NI_X86Base_TestZ:
            case NI_X86Base_TestNotZAndNotC:
            case NI_AVX_TestC:
            case NI_AVX_TestZ:
            case NI_AVX_TestNotZAndNotC:
            {
                var (test, condition) = intrinsicId switch {
                    NI_X86Base_TestC => (NI_X86Base_PTEST, GenCondition.C),
                    NI_X86Base_TestZ => (NI_X86Base_PTEST, GenCondition.EQ),
                    NI_X86Base_TestNotZAndNotC => (NI_X86Base_PTEST, GenCondition.UGT),
                    NI_AVX_TestC => (NI_AVX_PTEST, GenCondition.C),
                    NI_AVX_TestZ => (NI_AVX_PTEST, GenCondition.EQ),
                    _ => (NI_AVX_PTEST, GenCondition.UGT),
                };
                LowerHWIntrinsicCC(node, test, new GenCondition(condition));
                break;
            }
            case NI_AVX2_MultiplyAdd:
            case NI_AVX2_MultiplyAddNegated:
            case NI_AVX2_MultiplyAddNegatedScalar:
            case NI_AVX2_MultiplyAddScalar:
            case NI_AVX2_MultiplySubtract:
            case NI_AVX2_MultiplySubtractNegated:
            case NI_AVX2_MultiplySubtractNegatedScalar:
            case NI_AVX2_MultiplySubtractScalar:
            case NI_AVX512_FusedMultiplyAdd:
            case NI_AVX512_FusedMultiplyAddNegated:
            case NI_AVX512_FusedMultiplyAddNegatedScalar:
            case NI_AVX512_FusedMultiplyAddScalar:
            case NI_AVX512_FusedMultiplySubtract:
            case NI_AVX512_FusedMultiplySubtractNegated:
            case NI_AVX512_FusedMultiplySubtractNegatedScalar:
            case NI_AVX512_FusedMultiplySubtractScalar:
            {
                LowerFusedMultiplyOp(node);
                break;
            }
            case NI_AVX512_TernaryLogic:
            {
                return LowerHWIntrinsicTernaryLogic(node);
            }
            case NI_X86Base_BlendVariable:
            case NI_AVX_BlendVariable:
            case NI_AVX2_BlendVariable:
            {
                var baseType = node.SimdBaseType;
                var size = node.SimdSize;
                var first = node.GetOp(1);
                var second = node.GetOp(2);
                var mask = node.GetOp(3);
                if (varTypeIsIntegral(baseType))
                {
                    // Integral blend encodings operate bytewise despite normalized API types.
                    baseType = TYP_BYTE;
                }
                if (mask.IsVectorPerElementMask(CompilerInstance, baseType, size) &&
                    (first.IsVectorZero || second.IsVectorZero))
                {
                    GenTree binary;
                    if (first.IsVectorZero)
                    {
                        binary = CompilerInstance.gtNewSimdBinOpNode(GT_AND, node.Type, mask, second, baseType, size);
                        BlockRange().Remove(first);
                    }
                    else
                    {
                        binary = CompilerInstance.gtNewSimdBinOpNode(GT_AND_NOT, node.Type, first, mask, baseType, size);
                        BlockRange().Remove(second);
                    }
                    BlockRange().InsertBefore(node, binary);
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        use.ReplaceWith(binary);
                    }
                    else
                    {
                        binary.IsUnusedValue = true;
                    }
                    BlockRange().Remove(node);
                    return LowerNode(binary);
                }
                break;
            }
            case NI_AVX512_BlendVariableMask:
            {
                if (node.GetOp(2).IsVectorZero && TryInvertMask(node.GetOp(3), node.SimdSize, node.SimdBaseType))
                {
                    (node.GetOpRef(1), node.GetOpRef(2)) = (node.GetOp(2), node.GetOp(1));
                }
                break;
            }
            case NI_AVX512_ConvertVectorToMask:
            {
                var operand = node.GetOp(1);
                if (operand.Oper.IsCnsVec)
                {
                    var mask = CompilerInstance.gtFoldExprConvertVecCnsToMask(node, operand.AsVecCon());
                    BlockRange().InsertAfter(node, mask);
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        use.ReplaceWith(mask);
                    }
                    else
                    {
                        mask.IsUnusedValue = true;
                    }
                    BlockRange().Remove(operand);
                    BlockRange().Remove(node);
                    return LowerNode(mask);
                }
                break;
            }
            case NI_Vector_ToVector256Unsafe:
            case NI_Vector_ToVector512Unsafe:
            case NI_Vector_GetLower:
            case NI_Vector_GetLower128:
            {
                // Hardware consumers read register lanes at their own width, and containment
                // checks memory widths. ABI consumers still need this node's materialized size.
                if (BlockRange().TryGetUse(node, out var use) && use.User().Oper.IsHWIntrinsic)
                {
                    var user = use.User().AsHWIntrinsic();
                    var gatherIndex = user.HWIntrinsicId switch {
                        NI_AVX2_GatherVector128 or NI_AVX2_GatherVector256 => user.GetOp(2),
                        NI_AVX2_GatherMaskVector128 or NI_AVX2_GatherMaskVector256 => user.GetOp(3),
                        _ => null,
                    };
                    // VSIB selects xmm/ymm from the index operand's own width.
                    if (gatherIndex != node)
                    {
                        var operand = node.GetOp(1);
                        var next = node.Next;
                        use.ReplaceWith(operand);
                        BlockRange().Remove(node);
                        return next;
                    }
                }
                break;
            }
        }

        ContainCheckHWIntrinsic(node);
        return node.Next;
#else
        throw new NotImplementedException("Non-xarch hardware-intrinsic lowering is not ported.");
#endif
    }
}
