// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree? optVNBasedFoldExpr_Call_Memcmp(GenTreeCall call)
    {
        JITDUMP("See if we can optimize NI_System_SpanHelpers_SequenceEqual with help of VN...\n");
        assert(call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_SequenceEqual));
        assert(vnStore is not null);

        var arg1 = call.Args.GetUserArgByIndex(0) ?? throw new InvalidOperationException();
        var arg2 = call.Args.GetUserArgByIndex(1) ?? throw new InvalidOperationException();
        var lengthArg = call.Args.GetUserArgByIndex(2) ?? throw new InvalidOperationException();
        var lengthVN = optConservativeNormalVN(lengthArg.Node);
        if (!vnStore.IsVNIntegralConstant(lengthVN, out nuint length))
        {
            var firstVN = optConservativeNormalVN(arg1.Node);
            var secondVN = optConservativeNormalVN(arg2.Node);
            if ((firstVN != ValueNumStore.NoVN) && (firstVN == secondVN))
            {
                JITDUMP("...both arguments have the same VN -> optimize to constant true.\n");
                return gtWrapWithSideEffects(gtNewIconNode(TYP_INT, 1), call, GTF_ALL_EFFECT, true);
            }

            JITDUMP("...length is not a constant - bail out.\n");
            return null;
        }

        if (length == 0)
        {
            JITDUMP("...length is 0 -> optimize to constant true.\n");
            return gtWrapWithSideEffects(gtNewIconNode(TYP_INT, 1), call, GTF_ALL_EFFECT, true);
        }

        const nuint maxLength = 65536;
        if (length > maxLength)
        {
            JITDUMP($"...length is too big ({length} bytes) - bail out.\n");
            return null;
        }

        if (GetImmutableDataFromAddress(arg1.Node, (int)length, out var first) &&
            GetImmutableDataFromAddress(arg2.Node, (int)length, out var second))
        {
            var firstData = first ?? throw new InvalidOperationException("Immutable source returned no data.");
            var secondData = second ?? throw new InvalidOperationException("Immutable source returned no data.");
            var areEqual = firstData.AsSpan().SequenceEqual(secondData);
            JITDUMP($"...both memory regions are known at compile time -> optimize to constant {(areEqual ? "true" : "false")}.\n");
            return gtWrapWithSideEffects(gtNewIconNode(TYP_INT, areEqual ? 1 : 0), call, GTF_ALL_EFFECT, true);
        }

        JITDUMP("...data is not known at compile time - bail out.\n");
        return null;
    }

    public GenTree? optVNBasedFoldExpr_Call_Memset(GenTreeCall call)
    {
        JITDUMP("See if we can optimize NI_System_SpanHelpers_Fill with help of VN...\n");
        assert(call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_Fill));
        assert(vnStore is not null);

        var dstArg = call.Args.GetUserArgByIndex(0) ?? throw new InvalidOperationException();
        var lengthArg = call.Args.GetUserArgByIndex(1) ?? throw new InvalidOperationException();
        var valueArg = call.Args.GetUserArgByIndex(2) ?? throw new InvalidOperationException();
        var valueType = valueArg.SignatureType;
        var lengthScale = valueType.Size;
        if (lengthScale == 1)
        {
            JITDUMP("...value's type is byte - leave it for lower to expand.\n");
            return null;
        }

        if (varTypeIsStruct(valueType) || varTypeIsGC(valueType))
        {
            JITDUMP("...value's type is not supported - bail out.\n");
            return null;
        }

        var lengthVN = vnStore.VNNormalValue(lengthArg.Node._vnPair.Conservative);
        if (!vnStore.IsVNConstant(lengthVN))
        {
            JITDUMP("...length is not a constant - bail out.\n");
            return null;
        }

        var length = vnStore.CoercedConstantValue<nuint>(lengthVN);
        var threshold = (nuint)GetUnrollThreshold(Memset);
        if ((length > threshold) || ((length * (nuint)lengthScale) > threshold))
        {
            JITDUMP("...length is too big to unroll - bail out.\n");
            return null;
        }

        if (!valueArg.Node.Oper.IsConst && (length >= 8))
        {
            JITDUMP("...length is too big to unroll for non-constant value - bail out.\n");
            return null;
        }

        var dst = fgMakeMultiUse(ref dstArg.NodeRef);
        var value = fgMakeMultiUse(ref valueArg.NodeRef);
        GenTree? result = null;
        gtExtractSideEffList(call, ref result, GTF_ALL_EFFECT, true);

        for (nuint offset = 0; offset < length; offset++)
        {
            var offsetNode = gtNewIconNode(TYP_I_IMPL, unchecked((nint)(offset * (nuint)lengthScale)));
            var currentDst = gtNewBinaryNode(GT_ADD, dst.Type, gtCloneExpr(dst), offsetNode);
            var store = gtNewStoreIndNode(valueType, currentDst, gtCloneExpr(value),
                GTF_IND_UNALIGNED | GTF_IND_ALLOW_NON_ATOMIC);
            result = result is null ? store : gtNewBinaryNode(GT_COMMA, TYP_VOID, result, store);
        }

        JITDUMP("...optimized into STOREIND(s):\n");
        if (result is not null)
        {
            DISPTREE(result);
        }
        return result;
    }

    public GenTree? optVNBasedFoldExpr_Call_Memmove(GenTreeCall call)
    {
        JITDUMP("See if we can optimize NI_System_SpanHelpers_Memmove with help of VN...\n");
        assert(call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_Memmove) || call.IsHelperCall(CORINFO_HELP_MEMCPY));
        assert(vnStore is not null);

        var dstArg = call.Args.GetUserArgByIndex(0) ?? throw new InvalidOperationException();
        var srcArg = call.Args.GetUserArgByIndex(1) ?? throw new InvalidOperationException();
        var lengthArg = call.Args.GetUserArgByIndex(2) ?? throw new InvalidOperationException();
        var lengthVN = vnStore.VNNormalValue(lengthArg.Node._vnPair.Conservative);
        if (!vnStore.IsVNConstant(lengthVN))
        {
            JITDUMP("...length is not a constant - bail out.\n");
            return null;
        }

        var length = vnStore.CoercedConstantValue<nuint>(lengthVN);
        if (length == 0)
        {
            JITDUMP("...length is 0 -> optimize to no-op.\n");
            return gtWrapWithSideEffects(gtNewNothingNode(), call, GTF_ALL_EFFECT, true);
        }

        if (length > (nuint)GetUnrollThreshold(Memcpy))
        {
            JITDUMP("...length is too big to unroll - bail out.\n");
            return null;
        }

        if (!GetImmutableDataFromAddress(srcArg.Node, (int)length, out var buffer))
        {
            JITDUMP("...src is not a constant - fallback to LowerCallMemmove.\n");
            return null;
        }
        var data = buffer ?? throw new InvalidOperationException("Immutable source returned no data.");

        var dst = fgMakeMultiUse(ref dstArg.NodeRef);
        GenTree? result = null;
        gtExtractSideEffList(call, ref result, GTF_ALL_EFFECT, true);

        var remaining = (int)length;
        while (remaining > 0)
        {
            var offset = (int)length - remaining;
            var currentDst = gtCloneExpr(dst);
            if (offset != 0)
            {
                currentDst = gtNewBinaryNode(GT_ADD, dst.Type, currentDst, gtNewIconNode(TYP_I_IMPL, offset));
            }

            var type = roundDownMaxType(remaining);
            var constant = gtNewGenericCon(type, data.AsSpan(offset, type.Size));
            var store = gtNewStoreIndNode(type, currentDst, constant, GTF_IND_UNALIGNED);
            fgUpdateConstTreeValueNumber(constant);
            result = result is null ? store : gtNewBinaryNode(GT_COMMA, TYP_VOID, result, store);
            remaining -= type.Size;
        }

        JITDUMP("...optimized into STOREIND(s)!:\n");
        if (result is not null)
        {
            DISPTREE(result);
        }
        return result;
    }

    public unsafe GenTree? optVNBasedFoldExpr_Call(BasicBlock block, GenTree? parent, GenTreeCall call)
    {
        switch (call.HelperNum)
        {
            case CORINFO_HELP_CHKCASTARRAY:
            case CORINFO_HELP_CHKCASTANY:
            case CORINFO_HELP_CHKCASTINTERFACE:
            case CORINFO_HELP_CHKCASTCLASS:
            case CORINFO_HELP_ISINSTANCEOFARRAY:
            case CORINFO_HELP_ISINSTANCEOFCLASS:
            case CORINFO_HELP_ISINSTANCEOFANY:
            case CORINFO_HELP_ISINSTANCEOFINTERFACE:
            {
                var castClassArg = call.Args.GetUserArgByIndex(0) ?? throw new InvalidOperationException();
                var castObjectArg = call.Args.GetUserArgByIndex(1) ?? throw new InvalidOperationException();
                var castClass = castClassArg.Node;
                var castObject = castObjectArg.Node;
                if (castObject._vnPair == call._vnPair)
                {
                    castObject = fgMakeMultiUse(ref castObjectArg.NodeRef);
                    return gtWrapWithSideEffects(castObject, call, GTF_ALL_EFFECT, true);
                }

                if (castClass.Oper.IsCnsIntOrI && castClass.AsIntCon().IsIconHandle(GTF_ICON_CLASS_HDL))
                {
                    var from = gtGetClassHandle(castObject, out _, out _);
                    if (from != NO_CLASS_HANDLE)
                    {
                        var to = gtGetHelperArgClassHandle(castClass);
                        if ((to != NO_CLASS_HANDLE) &&
                            (info.compCompHnd->compareTypesForCast(from, to) is TypeCompareState.Must))
                        {
                            castObject = fgMakeMultiUse(ref castObjectArg.NodeRef);
                            return gtWrapWithSideEffects(castObject, call, GTF_ALL_EFFECT, true);
                        }
                    }
                }
                break;
            }

            default:
            {
                break;
            }
        }

        if (call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_Memmove) || call.IsHelperCall(CORINFO_HELP_MEMCPY))
        {
            return optVNBasedFoldExpr_Call_Memmove(call);
        }
        if (call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_Fill))
        {
            return optVNBasedFoldExpr_Call_Memset(call);
        }
        if (call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_SequenceEqual))
        {
            return optVNBasedFoldExpr_Call_Memcmp(call);
        }
        return null;
    }

    public GenTree? optVNBasedFoldExpr(BasicBlock block, GenTree? parent, GenTree tree)
    {
        var foldedToConstant = optVNBasedFoldConstExpr(block, parent, tree);
        if (foldedToConstant is not null)
        {
            return foldedToConstant;
        }

        switch (tree.Oper)
        {
            case GT_CALL:
            {
                return optVNBasedFoldExpr_Call(block, parent, tree.AsCall());
            }

            default:
            {
                break;
            }
        }
        return null;
    }

    public GenTree? optVNBasedFoldConstExpr(BasicBlock block, GenTree? parent, GenTree tree)
    {
        assert(vnStore is not null);
        if (tree.Oper is GT_JTRUE)
        {
            return optVNConstantPropOnJTrue(block, tree);
        }
        if (tree.Oper.IsCompare && ((tree.Flags & GTF_RELOP_JMP_USED) != 0))
        {
            return null;
        }

        var vnPair = tree._vnPair;
        var constantVN = vnStore.VNNormalValue(vnPair.Conservative);
        if (!vnStore.IsVNConstant(constantVN))
        {
            var app = new VNFuncApp();
            if (((tree.Flags & GTF_SIDE_EFFECT) == 0) && vnStore.GetVNFunc(constantVN, ref app) &&
                app.FuncIs(VNF_PtrToLoc))
            {
                var local = unchecked((uint)vnStore.CoercedConstantValue<nuint>(app.GetArg(0)));
                var offset = unchecked((uint)vnStore.CoercedConstantValue<nuint>(app.GetArg(1)));
                assert(offset <= ushort.MaxValue);
                return gtNewLclAddrNode(tree.Type, checked((int)local), unchecked((ushort)offset));
            }
            return null;
        }

        GenTree? constantTree = null;
        switch (vnStore.TypeOfVN(constantVN))
        {
            case TYP_FLOAT:
            {
                var value = vnStore.ConstantValue<float>(constantVN);
                if (tree.Type is TYP_INT)
                {
                    constantTree = gtNewIconNode(TYP_INT, BitConverter.SingleToInt32Bits(value));
                }
                else
                {
                    assert(varTypeIsFloating(tree.Type));
                    constantTree = gtNewDconNode(tree.Type, value);
                }
                break;
            }

            case TYP_DOUBLE:
            {
                var value = vnStore.ConstantValue<double>(constantVN);
                if (tree.Type is TYP_LONG)
                {
                    constantTree = gtNewLconNode(BitConverter.DoubleToInt64Bits(value));
                }
                else
                {
                    assert(varTypeIsFloating(tree.Type));
                    constantTree = gtNewDconNode(tree.Type, value);
                }
                break;
            }

            case TYP_LONG:
            {
                var value = vnStore.ConstantValue<long>(constantVN);
#if TARGET_64BIT
                if (vnStore.IsVNHandle(constantVN))
                {
                    if (!opts.compReloc)
                    {
                        constantTree = gtNewIconHandleNode(unchecked((nint)value), vnStore.GetHandleFlags(constantVN));
                    }
                }
                else
#endif
                {
                    switch (tree.Type)
                    {
                        case TYP_INT:
                        {
                            constantTree = gtNewIconNode(TYP_INT, unchecked((int)value));
                            break;
                        }

                        case TYP_LONG:
                        {
                            constantTree = gtNewLconNode(value);
                            break;
                        }

                        case TYP_FLOAT:
                        {
                            throw new UnreachableException();
                        }

                        case TYP_DOUBLE:
                        {
                            constantTree = gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(value));
                            break;
                        }
                    }
                }
                break;
            }

            case TYP_REF:
            {
                if (tree.Type is TYP_REF)
                {
                    var value = vnStore.ConstantValue<nuint>(constantVN);
                    if (value == 0)
                    {
                        constantTree = gtNewNull();
                    }
                    else
                    {
                        assert(vnStore.IsVNObjHandle(constantVN));
                        constantTree = gtNewIconHandleNode(unchecked((nint)value), GTF_ICON_OBJ_HDL);
                    }
                }
                break;
            }

            case TYP_INT:
            {
                var value = vnStore.ConstantValue<int>(constantVN);
#if !TARGET_64BIT
                if (vnStore.IsVNHandle(constantVN))
                {
                    if (!opts.compReloc)
                    {
                        constantTree = gtNewIconHandleNode(value, vnStore.GetHandleFlags(constantVN));
                    }
                }
                else
#endif
                {
                    switch (tree.Type)
                    {
                        case TYP_REF:
                        case TYP_INT:
                        {
                            constantTree = gtNewIconNode(TYP_INT, value);
                            break;
                        }

                        case TYP_BYTE:
                        case TYP_UBYTE:
                        case TYP_SHORT:
                        case TYP_USHORT:
                        {
                            assert(FitsIn(tree.Type, value));
                            constantTree = gtNewIconNode(TYP_INT, value);
                            break;
                        }

                        case TYP_LONG:
                        {
                            constantTree = gtNewLconNode(value);
                            break;
                        }

                        case TYP_FLOAT:
                        {
                            constantTree = gtNewDconNode(TYP_FLOAT, BitConverter.Int32BitsToSingle(value));
                            break;
                        }

                        case TYP_DOUBLE:
                        {
                            throw new UnreachableException();
                        }
                    }
                }
                break;
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                var vector = gtNewVconNode(tree.Type);
                vector.SimdVal = vnStore.GetConstantSimd(constantVN);
                constantTree = vector;
                break;
            }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
            case TYP_MASK:
            {
                constantTree = gtNewMskConNode(vnStore.GetConstantSimdMask(constantVN));
                break;
            }
#endif

            case TYP_BYREF:
            {
                break;
            }

            default:
            {
                throw new UnreachableException();
            }
        }

        if (constantTree is null)
        {
            return null;
        }
        if (!optIsProfitableToSubstitute(tree, block, parent, constantTree))
        {
            return null;
        }

        constantTree._vnPair = vnPair;
        var ignoreRoot = true;
        if (((tree.Flags & GTF_EXCEPT) != 0) && (tree.Exceptions(this) is not ExceptionSetFlags.None))
        {
            var operandExceptions = ValueNumStore.VNPForEmptyExcSet();
            _ = tree.VisitOperands(operand =>
            {
                var pair = operand._vnPair.BothDefined() ? operand._vnPair : ValueNumStore.VNPForVoid();
                operandExceptions = vnStore.VNPUnionExcSet(pair, operandExceptions);
                return GenTree.VisitResult.Continue;
            });
            ignoreRoot = vnStore.VNPExcIsSubset(operandExceptions, vnStore.VNPExceptionSet(vnPair));
        }

        return gtWrapWithSideEffects(constantTree, tree, GTF_SIDE_EFFECT, ignoreRoot);
    }

    public GenTree? optVNConstantPropOnJTrue(BasicBlock block, GenTree test)
    {
        assert(vnStore is not null);
        var relop = test.AsUnOp().Op1;
        if (!relop.Oper.IsCompare)
        {
            return null;
        }

        assert((relop.Flags & GTF_RELOP_JMP_USED) != 0);
        var constantVN = vnStore.VNNormalValue(relop._vnPair.Conservative);
        if (!vnStore.IsVNConstant(constantVN))
        {
            return null;
        }

        var effects = gtWrapWithSideEffects(gtNewNothingNode(), relop);
        if (!effects.IsNothingNode)
        {
            var statement = fgNewStmtNearEnd(block, effects);
            _ = fgMorphBlockStmt(block, statement, message: nameof(optVNConstantPropOnJTrue));
        }

        var evaluatesToTrue = vnStore.CoercedConstantValue<long>(constantVN) != 0;
        test.AsUnOp().Op1 = gtNewBinaryNode(evaluatesToTrue ? GT_EQ : GT_NE, relop.Type,
            gtNewZeroConNode(TYP_INT), gtNewZeroConNode(TYP_INT));
        return test;
    }

    public bool optIsProfitableToSubstitute(GenTree dest, BasicBlock destBlock, GenTree? destParent, GenTree value)
    {
        if (value.Oper.IsCnsIntOrI &&
            (value.AsIntCon().IsIconHandle(GTF_ICON_STATIC_HDL) || value.AsIntCon().IsIconHandle(GTF_ICON_CLASS_HDL)))
        {
            return false;
        }

        if (dest.Oper is not GT_LCL_VAR)
        {
            return true;
        }

        var local = dest.AsLclVar();
        if (value.Oper.IsCnsVec)
        {
#if FEATURE_HW_INTRINSICS
            var inspectIntrinsic = false;
            if (destParent is GenTreeHWIntrinsic intrinsic)
            {
                ref var descriptor = ref lvaGetDesc(local.LclNum);
                inspectIntrinsic = local.HasSsaName
                    ? descriptor.GetPerSsaData(local.SsaNum).NumUses > 1
                    : descriptor.lvRefCnt() > 2;

                if (inspectIntrinsic)
                {
                    if (!HWIntrinsicInfo.CanBenefitFromConstantProp(intrinsic.HWIntrinsicId))
                    {
                        return false;
                    }
                    return intrinsic.ShouldConstantProp(dest, value.AsVecCon());
                }
            }
#endif
        }
        else if (!value.Oper.IsCnsFltOrDbl && !value.Oper.IsCnsMsk)
        {
            return true;
        }

        gtPrepareCost(value);
        if ((value.CostEx > 1) && (value.CostSz > 1) && local.HasSsaName)
        {
            var definitionBlock = lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum).Block;
            if (definitionBlock is not null)
            {
                var definitionWeight = definitionBlock.getBBWeight(this);
                var useWeight = destBlock.getBBWeight(this);
                if ((definitionWeight > 0) && ((useWeight / definitionWeight) >= BB_LOOP_WEIGHT_SCALE))
                {
                    JITDUMP($"Constant propagation inside loop {FMT_BB(destBlock.bbNum)} is not profitable\n");
                    return false;
                }
            }
        }
        return true;
    }
}
