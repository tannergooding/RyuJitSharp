// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree ConvertSveMaskOperandToMask(GenTreeHWIntrinsic insertionPoint, GenTree operand)
    {
        if (varTypeIsMask(operand.Type))
        {
            return operand;
        }

        if (operand.IsConvertMaskToVector)
        {
            var mask = operand.AsHWIntrinsic().GetOp(1);
            BlockRange().Remove(operand);
            return mask;
        }

        if (operand.IsVectorZero)
        {
            var mask = CompilerInstance.gtNewSimdFalseMaskByteNode();
            BlockRange().InsertBefore(insertionPoint, mask);
            BlockRange().Remove(operand);
            return mask;
        }

        unreached();
        return default!;
    }

    private GenTree? TryLowerSveConvertVectorToMask(GenTreeHWIntrinsic node)
    {
        assert(node.HWIntrinsicId is NI_Sve_ConvertVectorToMask);
        assert(node.Operands.Length == 2);

        var mask = node.GetOp(2);
        if (!varTypeIsMask(mask.Type))
        {
            return null;
        }

        var next = node.Next;
        if (BlockRange().TryGetUse(node, out var use))
        {
            use.ReplaceWith(mask);
        }
        else
        {
            mask.IsUnusedValue = true;
        }

        BlockRange().Remove(node.GetOp(1));
        BlockRange().Remove(node);
        return next;
    }

    private bool TryLowerSvePredicateBitwiseClear(
        GenTreeHWIntrinsic node, ref NamedIntrinsic intrinsicId, ref genTreeOps operation)
    {
        assert(operation is GT_AND);

        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        GenTreeHWIntrinsic? notNode = null;
        var nodeType = node.Type;
        var simdSize = node.SimdSize;

        void ConvertResultToVectorIfNeeded()
        {
            if (!BlockRange().TryGetUse(node, out var use))
            {
                return;
            }

            var user = use.User();
            if ((user is GenTreeHWIntrinsic conversion) &&
                (conversion.HWIntrinsicId is NI_Sve_ConvertVectorToMask) &&
                (conversion.SimdBaseType.Size == node.SimdBaseType.Size))
            {
                return;
            }

            var vector = CompilerInstance.gtNewSimdCvtMaskToVectorNode(
                nodeType, node, node.SimdBaseType, simdSize);
            BlockRange().InsertAfter(node, vector);
            use.ReplaceWith(vector);
        }

        if (op2.Oper.IsHWIntrinsic)
        {
            var op2Intrinsic = op2.AsHWIntrinsic();
            var op2Operation = op2Intrinsic.GetOperForHWIntrinsicId(out var op2IsScalar);
            if ((op2Operation is GT_NOT) && !op2IsScalar)
            {
                notNode = op2Intrinsic;
                op2 = op2Intrinsic.GetOp(1);
            }
        }

        if ((notNode is null) && op1.Oper.IsHWIntrinsic)
        {
            var op1Intrinsic = op1.AsHWIntrinsic();
            var op1Operation = op1Intrinsic.GetOperForHWIntrinsicId(out var op1IsScalar);
            if ((op1Operation is GT_NOT) && !op1IsScalar)
            {
                notNode = op1Intrinsic;
                op1 = op1Intrinsic.GetOp(1);
                (op1, op2) = (op2, op1);
            }
        }

        if ((notNode is not null) && op1.IsSveMaskOperand && op2.IsSveMaskOperand)
        {
            var simdBaseTypeSize = node.SimdBaseType.Size;
            if ((op1.IsConvertMaskToVector &&
                 (op1.AsHWIntrinsic().SimdBaseType.Size != simdBaseTypeSize)) ||
                (op2.IsConvertMaskToVector &&
                 (op2.AsHWIntrinsic().SimdBaseType.Size != simdBaseTypeSize)))
            {
                return false;
            }

            op1 = ConvertSveMaskOperandToMask(node, op1);
            op2 = ConvertSveMaskOperandToMask(node, op2);
            BlockRange().Remove(notNode);

            node.ResetHWIntrinsicId(NI_Sve_BitwiseClear_Predicates, op1, op2);
            node.Type = TYP_MASK;
            intrinsicId = NI_Sve_BitwiseClear_Predicates;
            operation = GT_AND_NOT;
            ConvertResultToVectorIfNeeded();
            return true;
        }

        return false;
    }

    private GenTree? LowerHWIntrinsicArm64(GenTreeHWIntrinsic node)
    {
        if (node.Type is TYP_SIMD12)
        {
            node.Type = TYP_SIMD16;
        }

        if ((node.Type is TYP_MASK) || HWIntrinsicInfo.ReturnsPerElementMask(node.HWIntrinsicId))
        {
            assert(HWIntrinsicInfo.ReturnsPerElementMask(node.HWIntrinsicId));
            assert(node.Type is TYP_MASK);
        }

        var id = node.HWIntrinsicId;
        var operation = node.GetOperForHWIntrinsicId(out _);
        var loweredSvePredicate = (operation is GT_AND) && Configuration.FEATURE_MASKED_HW_INTRINSICS &&
            TryLowerSvePredicateBitwiseClear(node, ref id, ref operation);
        if (!loweredSvePredicate && (operation is GT_AND or GT_OR))
        {
            assert(node.Type is TYP_SIMD8 or TYP_SIMD16);
            var transform = false;
            var first = node.GetOp(1);
            var second = node.GetOp(2);
            if (second.Oper.IsHWIntrinsic)
            {
                var intrinsic = second.AsHWIntrinsic();
                if (intrinsic.GetOperForHWIntrinsicId(out var scalar) is GT_NOT)
                {
                    assert(!scalar);
                    transform = true;
                    second = intrinsic.GetOp(1);
                    BlockRange().Remove(intrinsic);
                }
            }

            if (!transform && first.Oper.IsHWIntrinsic)
            {
                var intrinsic = first.AsHWIntrinsic();
                if (intrinsic.GetOperForHWIntrinsicId(out var scalar) is GT_NOT)
                {
                    assert(!scalar);
                    transform = true;
                    first = intrinsic.GetOp(1);
                    BlockRange().Remove(intrinsic);
                    (first, second) = (second, first);
                }
            }

            if (transform)
            {
                id = operation is GT_AND ? NI_AdvSimd_BitwiseClear : NI_AdvSimd_OrNot;
                node.ChangeHWIntrinsicId(id, first, second);
            }
        }

        switch (id)
        {
            case NI_Sve_ConvertVectorToMask when Configuration.FEATURE_MASKED_HW_INTRINSICS:
            {
                var next = TryLowerSveConvertVectorToMask(node);
                if (next is not null)
                {
                    return next;
                }
                break;
            }

            case NI_Vector_Create:
            case NI_Vector_CreateScalar:
            {
                return LowerHWIntrinsicCreateArm64(node);
            }

            case NI_Vector_Dot:
            {
                return LowerHWIntrinsicDotArm64(node);
            }

            case NI_Vector_GetElement:
            {
                return LowerHWIntrinsicGetElementArm64(node);
            }

            case NI_Vector_op_Equality:
            case NI_Vector_op_Inequality:
            {
                return LowerHWIntrinsicCmpOpArm64(node, id is NI_Vector_op_Equality ? GT_EQ : GT_NE);
            }

            case NI_Sve_TestAnyTrue:
            case NI_Sve_TestFirstTrue:
            case NI_Sve_TestLastTrue:
            {
                var condition = id switch {
                    NI_Sve_TestAnyTrue => GenCondition.NE,
                    NI_Sve_TestFirstTrue => GenCondition.SLT,
                    _ => GenCondition.ULT,
                };
                _ = LowerNodeCC(node, new GenCondition(condition));
                node.Type = TYP_VOID;
                return node.Next;
            }

            case NI_Vector_WithLower:
            case NI_Vector_WithUpper:
            {
                var index = CompilerInstance.gtNewIconNode(TYP_INT, id is NI_Vector_WithUpper ? 1 : 0);
                BlockRange().InsertBefore(node, index);
                _ = LowerNode(index);
                node.SimdBaseType = TYP_ULONG;
                node.ResetHWIntrinsicId(NI_AdvSimd_InsertScalar, node.GetOp(1), index, node.GetOp(2));
                break;
            }

            case NI_Vector_GetLower:
            case NI_Vector_ToVector128Unsafe:
            {
                // Only intrinsic consumers read the aliased register directly; ABI consumers need the original size.
                if (BlockRange().TryGetUse(node, out var use) && use.User().Oper.IsHWIntrinsic)
                {
                    var next = node.Next;
                    use.ReplaceWith(node.GetOp(1));
                    BlockRange().Remove(node);
                    return next;
                }
                break;
            }

            case NI_AdvSimd_FusedMultiplyAddScalar:
            {
                LowerHWIntrinsicFusedMultiplyAddScalar(node);
                break;
            }

            case NI_Vector_CreateScalarUnsafe:
            {
                // Keep scalar types intact for spills, and preserve the wrapper needed by scalar FMA negation folding.
                if (varTypeIsFloating(node.SimdBaseType))
                {
                    var operand = node.GetOp(1);
                    var next = node.Next;
                    if (BlockRange().TryGetUse(node, out var use))
                    {
                        if (!use.User().Oper.IsHWIntrinsic ||
                            (use.User().AsHWIntrinsic().HWIntrinsicId is NI_AdvSimd_FusedMultiplyAddScalar))
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

            case NI_Sve_ConditionalSelect:
            case NI_Sve_ConditionalSelect_Predicates:
            {
                return LowerSveConditionalSelect(node);
            }

            case NI_Sve_SetFfr:
            {
                StoreFFRValue(node);
                break;
            }

            case NI_Sve_GetFfrByte:
            case NI_Sve_GetFfrDouble:
            case NI_Sve_GetFfrInt16:
            case NI_Sve_GetFfrInt32:
            case NI_Sve_GetFfrInt64:
            case NI_Sve_GetFfrSByte:
            case NI_Sve_GetFfrSingle:
            case NI_Sve_GetFfrUInt16:
            case NI_Sve_GetFfrUInt32:
            case NI_Sve_GetFfrUInt64:
            {
                if (BlockRange().TryGetUse(node, out var use))
                {
                    var local = CompilerInstance.getFFRegisterVarNum();
                    var value = CompilerInstance.gtNewLclvNode(TYP_MASK, local);
                    BlockRange().InsertBefore(node, value);
                    use.ReplaceWith(value);
                    var next = node.Next;
                    BlockRange().Remove(node);
                    return next;
                }

                node.IsUnusedValue = true;
                break;
            }

            default:
            {
                if (IsFfrIntrinsic(id))
                {
                    var foundUse = BlockRange().TryGetUse(node, out var use);
                    if (_ffrTrashed)
                    {
                        var local = CompilerInstance.getFFRegisterVarNum();
                        var value = CompilerInstance.gtNewLclvNode(TYP_MASK, local);
                        BlockRange().InsertBefore(node, value);
                        _ = LowerNode(value);
                        if (node.Operands.Length == 3)
                        {
                            node.ResetHWIntrinsicId(id, node.GetOp(1), node.GetOp(2), node.GetOp(3), value);
                        }
                        else
                        {
                            assert(node.Operands.Length == 2);
                            node.ResetHWIntrinsicId(id, node.GetOp(1), node.GetOp(2), value);
                        }
                    }

                    if (foundUse)
                    {
                        var temp = CompilerInstance.lvaGrabTempWithImplicitUse(true, "Return value result/FFR");
                        CompilerInstance.lvaGetDesc(temp).Type = node.Type;
                        _ = use.ReplaceWithLclVar(CompilerInstance, temp, out _);
                    }
                    else
                    {
                        node.IsUnusedValue = true;
                    }

                    StoreFFRValue(node);
                }
                break;
            }
        }

        if (Configuration.FEATURE_MASKED_HW_INTRINSICS &&
            (id is NI_Sve_And_Predicates or NI_Sve_BitwiseClear_Predicates))
        {
            ContainCheckHWIntrinsic(node);
            return node.Next;
        }

        if (HWIntrinsicInfo.IsEmbeddedMaskedOperation(id))
        {
            LABELEDDISPTREERANGE("lowering EmbeddedMasked HWIntrinisic (before)", BlockRange(), node);
            var last = node.GetOp(node.Operands.Length);
            if (last.Oper.IsHWIntrinsic && TryContainingCselOp(node, last.AsHWIntrinsic()))
            {
                LABELEDDISPTREERANGE("Contained conditional select", BlockRange(), node);
                return node.Next;
            }

            if (BlockRange().TryGetUse(node, out var use))
            {
                var maskOperation = HWIntrinsicInfo.ReturnsPerElementMask(node.HWIntrinsicId);
                var selectType = maskOperation ? TYP_MASK : Compiler.GetSimdTypeForSize(node.SimdSize);
                var selectId = maskOperation ? NI_Sve_ConditionalSelect_Predicates : NI_Sve_ConditionalSelect;
                var mask = CompilerInstance.gtNewSimdTrueMaskNode(node.SimdBaseType);
                var zero = CompilerInstance.gtNewZeroConNode(selectType);
                BlockRange().InsertBefore(node, mask);
                BlockRange().InsertBefore(node, zero);
                var select = CompilerInstance.gtNewSimdHWIntrinsicNode(selectType, selectId,
                    node.SimdBaseType, node.SimdSize, mask, node, zero);
                BlockRange().InsertAfter(node, select);
                use.ReplaceWith(select);
                LABELEDDISPTREERANGE("Wrapped embedded-mask intrinsic with ConditionalSelect", BlockRange(), select);
            }
            else
            {
                assert(node.IsUnusedValue);
            }
        }

        ContainCheckHWIntrinsic(node);
        return node.Next;
    }

    private GenTree? LowerHWIntrinsicGetElementArm64(GenTreeHWIntrinsic node)
    {
        var vector = node.GetOp(1);
        var index = NormalizeIndexToNativeSized(node.GetOp(2));
        node.SetOp(2, index);
        var containMemory = IsContainableMemoryOp(vector) && IsSafeToContainMem(node, vector);

        if (containMemory || !index.Oper.IsConst)
        {
            var baseType = node.SimdBaseType;
            var simdType = Compiler.GetSimdTypeForSize(node.SimdSize);
            var local = BAD_VAR_NUM;
            ushort localOffset = 0;
            if (!containMemory)
            {
                local = CompilerInstance.getSIMDInitTempVarNum(simdType);
                var store = CompilerInstance.gtNewStoreLclVarNode(local, vector);
                BlockRange().InsertBefore(node, store);
                _ = LowerNode(store);
            }
            else if (vector.Oper.IsLocal)
            {
                local = vector.AsLclVarCommon().LclNum;
                localOffset = vector.AsLclVarCommon().LclOffs;
                BlockRange().Remove(vector);
            }

            GenTree address;
            if (local != BAD_VAR_NUM)
            {
                address = CompilerInstance.gtNewLclAddrNode(TYP_BYREF, local, localOffset);
                BlockRange().InsertBefore(node, address);
                _ = LowerNode(address);
            }
            else
            {
                assert(vector.Oper.IsIndir);
                address = vector.AsIndir().Addr;
                BlockRange().Remove(vector);
            }

            var offset = index;
            var elementSize = baseType.Size;
            if (offset.Oper.IsConst)
            {
                offset.AsIntCon().IconValue = unchecked(offset.AsIntCon().IconValue * elementSize);
            }
            else if (elementSize != 1)
            {
                var scale = CompilerInstance.gtNewIconNode(TYP_INT, elementSize);
                BlockRange().InsertBefore(node, scale);
                offset = CompilerInstance.gtNewBinaryNode(GT_MUL, offset.Type, offset, scale);
                BlockRange().InsertBefore(node, offset);
            }

            // Indirection lowering forms the address mode; do not lower the multiply or add separately.
            if (!offset.IsIntegralConst(0))
            {
                address = CompilerInstance.gtNewBinaryNode(GT_ADD, address.Type, address, offset);
                BlockRange().InsertBefore(node, address);
            }
            else
            {
                BlockRange().Remove(offset);
            }

            var load = CompilerInstance.gtNewIndir(baseType, address);
            BlockRange().InsertBefore(node, load);
            if (BlockRange().TryGetUse(node, out var use))
            {
                use.ReplaceWith(load);
            }
            else
            {
                load.IsUnusedValue = true;
            }

            BlockRange().Remove(node);
            return LowerNode(load);
        }

        assert(index.Oper.IsConst);
        ContainCheckHWIntrinsic(node);
        return node.Next;
    }
}
