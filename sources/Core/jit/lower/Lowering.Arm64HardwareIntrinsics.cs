// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class Lowering
{
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
        if (operation is GT_AND or GT_OR)
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
                return LowerHWIntrinsicCndSel(node);
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
#endif
