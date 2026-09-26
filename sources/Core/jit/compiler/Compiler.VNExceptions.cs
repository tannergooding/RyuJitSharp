// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, valuenum.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgValueNumberAddExceptionSetForIndirection(GenTree tree, GenTree baseAddr)
    {
        assert(tree.Oper.IsIndir || tree.IsImplicitIndir);
        assert(vnStore is not null);

        if (tree._vnPair.BothEqual() && vnStore.IsVNConstant(tree._vnPair.Liberal))
        {
            return;
        }

        tree._vnPair = vnStore.VNPWithExc(tree._vnPair, fgValueNumberIndirNullCheckExceptions(baseAddr));
    }

    public ValueNumPair fgValueNumberIndirNullCheckExceptions(GenTree baseAddr)
    {
        assert(vnStore is not null);
        var baseVNP = vnStore.VNPNormalPair(baseAddr._vnPair);
        var baseLVN = baseVNP.Liberal;
        var baseCVN = baseVNP.Conservative;
        assert(baseVNP.BothDefined());

        if (!varTypeIsSimd(baseAddr.Type))
        {
            vnStore.PeelOffsets(ref baseLVN, out var offsetL);
            if (fgIsBigOffset(unchecked((nint)offsetL)))
            {
                baseLVN = baseVNP.Liberal;
            }

            vnStore.PeelOffsets(ref baseCVN, out var offsetC);
            if (fgIsBigOffset(unchecked((nint)offsetC)))
            {
                baseCVN = baseVNP.Conservative;
            }
        }

        var exceptions = ValueNumStore.VNPForEmptyExcSet();
        if (!vnStore.IsKnownNonNull(baseLVN))
        {
            exceptions.Liberal = vnStore.VNExcSetSingleton(
                vnStore.VNForFunc(TYP_REF, VNF_NullPtrExc, baseLVN));
        }

        if (!vnStore.IsKnownNonNull(baseCVN))
        {
            exceptions.Conservative = vnStore.VNExcSetSingleton(
                vnStore.VNForFunc(TYP_REF, VNF_NullPtrExc, baseCVN));
        }

        return exceptions;
    }

    public void fgValueNumberAddExceptionSetForDivision(GenTree tree)
    {
        assert(vnStore is not null);
        var exceptions = fgValueNumberDivisionExceptions(tree.Oper, tree.AsOp().Op1, tree.AsOp().Op2);
        vnStore.VNPUnpackExc(tree._vnPair, out var normal, out var existing);
        tree._vnPair = vnStore.VNPWithExc(normal, vnStore.VNPExcSetUnion(existing, exceptions));
    }

    public ValueNumPair fgValueNumberDivisionExceptions(genTreeOps oper, GenTree dividend, GenTree divisor)
    {
        assert(vnStore is not null);
        assert(oper is GT_DIV or GT_UDIV or GT_MOD or GT_UMOD);
        var isUnsignedOper = oper is GT_UDIV or GT_UMOD;
        var needDivideByZeroExcLib = true;
        var needDivideByZeroExcCon = true;
        var needArithmeticExcLib = !isUnsignedOper;
        var needArithmeticExcCon = !isUnsignedOper;

        var type = dividend.Type.ActualType;
        assert(type is TYP_INT or TYP_LONG);
        var divisorNorm = vnStore.VNPNormalPair(divisor._vnPair);
        var vnDivisorNormLib = divisorNorm.Liberal;
        var vnDivisorNormCon = divisorNorm.Conservative;

        if (type is TYP_INT)
        {
            if (vnStore.IsVNConstant(vnDivisorNormLib))
            {
                var value = vnStore.ConstantValue<int>(vnDivisorNormLib);
                if (value != 0)
                {
                    needDivideByZeroExcLib = false;
                }
                if (!isUnsignedOper && (value != -1))
                {
                    needArithmeticExcLib = false;
                }
            }

            if (vnStore.IsVNConstant(vnDivisorNormCon))
            {
                var value = vnStore.ConstantValue<int>(vnDivisorNormCon);
                if (value != 0)
                {
                    needDivideByZeroExcCon = false;
                }
                if (!isUnsignedOper && (value != -1))
                {
                    needArithmeticExcCon = false;
                }
            }
        }
        else
        {
            if (vnStore.IsVNConstant(vnDivisorNormLib))
            {
                var value = vnStore.ConstantValue<long>(vnDivisorNormLib);
                if (value != 0)
                {
                    needDivideByZeroExcLib = false;
                }
                if (!isUnsignedOper && (value != -1))
                {
                    needArithmeticExcLib = false;
                }
            }

            if (vnStore.IsVNConstant(vnDivisorNormCon))
            {
                var value = vnStore.ConstantValue<long>(vnDivisorNormCon);
                if (value != 0)
                {
                    needDivideByZeroExcCon = false;
                }
                if (!isUnsignedOper && (value != -1))
                {
                    needArithmeticExcCon = false;
                }
            }
        }

        var dividendNorm = vnStore.VNPNormalPair(dividend._vnPair);
        var vnDividendNormLib = dividendNorm.Liberal;
        var vnDividendNormCon = dividendNorm.Conservative;
        if (needArithmeticExcLib || needArithmeticExcCon)
        {
            if (type is TYP_INT)
            {
                if (vnStore.IsVNConstant(vnDividendNormLib) &&
                    !isUnsignedOper && (vnStore.ConstantValue<int>(vnDividendNormLib) != int.MinValue))
                {
                    needArithmeticExcLib = false;
                }
                if (vnStore.IsVNConstant(vnDividendNormCon) &&
                    !isUnsignedOper && (vnStore.ConstantValue<int>(vnDividendNormCon) != int.MinValue))
                {
                    needArithmeticExcCon = false;
                }
            }
            else
            {
                if (vnStore.IsVNConstant(vnDividendNormLib) &&
                    !isUnsignedOper && (vnStore.ConstantValue<long>(vnDividendNormLib) != long.MinValue))
                {
                    needArithmeticExcLib = false;
                }
                if (vnStore.IsVNConstant(vnDividendNormCon) &&
                    !isUnsignedOper && (vnStore.ConstantValue<long>(vnDividendNormCon) != long.MinValue))
                {
                    needArithmeticExcCon = false;
                }
            }
        }

        var divideByZero = ValueNumStore.VNPForEmptyExcSet();
        var arithmetic = ValueNumStore.VNPForEmptyExcSet();
        if (needDivideByZeroExcLib)
        {
            divideByZero.Liberal = vnStore.VNExcSetSingleton(
                vnStore.VNForFunc(TYP_REF, VNF_DivideByZeroExc, vnDivisorNormLib));
        }
        if (needDivideByZeroExcCon)
        {
            divideByZero.Conservative = vnStore.VNExcSetSingleton(
                vnStore.VNForFunc(TYP_REF, VNF_DivideByZeroExc, vnDivisorNormCon));
        }
        if (needArithmeticExcLib)
        {
            arithmetic.Liberal = vnStore.VNExcSetSingleton(vnStore.VNForFuncNoFolding(
                TYP_REF, VNF_ArithmeticExc, vnDividendNormLib, vnDivisorNormLib));
        }
        if (needArithmeticExcCon)
        {
            arithmetic.Conservative = vnStore.VNExcSetSingleton(vnStore.VNForFuncNoFolding(
                TYP_REF, VNF_ArithmeticExc, vnDividendNormLib, vnDivisorNormCon));
        }

        return vnStore.VNPExcSetUnion(divideByZero, arithmetic);
    }

    public void fgValueNumberAddExceptionSetForOverflow(GenTree tree)
    {
        assert(tree.HasOverflowCheckEx);
        assert(vnStore is not null);
        var oper = tree.Oper;
        assert(oper is GT_ADD or GT_SUB or GT_MUL);
        var vnf = (oper, tree.AsOp().IsUnsigned) switch
        {
            (GT_ADD, false) => VNF_ADD_OVF,
            (GT_SUB, false) => VNF_SUB_OVF,
            (GT_MUL, false) => VNF_MUL_OVF,
            (GT_ADD, true) => VNF_ADD_UN_OVF,
            (GT_SUB, true) => VNF_SUB_UN_OVF,
            (GT_MUL, true) => VNF_MUL_UN_OVF,
            _ => throw new System.Diagnostics.UnreachableException(),
        };

        for (var kind = VNK_Liberal; kind <= VNK_Conservative; kind++)
        {
            var vn = tree._vnPair[kind];
            vnStore.VNUnpackExc(vn, out var normal, out var existing);
            if (vnStore.IsVNConstant(normal) ||
                (normal == vnStore.VNNormalValue(tree.AsOp().Op1._vnPair[kind])) ||
                (normal == vnStore.VNNormalValue(tree.AsOp().Op2._vnPair[kind])))
            {
                continue;
            }

#if DEBUG
            var app = new VNFuncApp();
            assert(vnStore.GetVNFunc(normal, ref app) && app.FuncIs(vnf));
#endif
            var overflow = vnStore.VNExcSetSingleton(vnStore.VNForFunc(TYP_REF, VNF_OverflowExc, normal));
            tree._vnPair[kind] = vnStore.VNWithExc(normal, vnStore.VNExcSetUnion(existing, overflow));
        }
    }

    public void fgValueNumberAddExceptionSetForBoundsCheck(GenTree tree)
    {
        assert(vnStore is not null);
        var node = tree.AsBoundsChk();
        vnStore.VNPUnpackExc(tree._vnPair, out var normal, out var existing);
        var bounds = vnStore.VNPExcSetSingleton(vnStore.VNPairForFuncNoFolding(
            TYP_REF, VNF_IndexOutOfRangeExc,
            vnStore.VNPNormalPair(node.Index._vnPair),
            vnStore.VNPNormalPair(node.ArrayLength._vnPair)));
        tree._vnPair = vnStore.VNPWithExc(normal, vnStore.VNPExcSetUnion(existing, bounds));
    }

    public void fgValueNumberAddExceptionSetForCkFinite(GenTree tree)
    {
        assert(tree.Oper is GT_CKFINITE);
        assert(vnStore is not null);
        vnStore.VNPUnpackExc(tree._vnPair, out var normal, out var existing);
        var arithmetic = vnStore.VNPExcSetSingleton(
            vnStore.VNPairForFunc(TYP_REF, VNF_ArithmeticExc, normal));
        tree._vnPair = vnStore.VNPWithExc(normal, vnStore.VNPExcSetUnion(existing, arithmetic));
    }

    public void fgValueNumberAddExceptionSet(GenTree tree)
    {
        if (!tree.MayThrow(this))
        {
            return;
        }

        switch (tree.Oper)
        {
            case GT_CAST:
            case GT_LCLHEAP:
            {
                break;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            {
                assert(tree.HasOverflowCheckEx);
                fgValueNumberAddExceptionSetForOverflow(tree);
                break;
            }

            case GT_DIV:
            case GT_UDIV:
            case GT_MOD:
            case GT_UMOD:
            {
                fgValueNumberAddExceptionSetForDivision(tree);
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                fgValueNumberAddExceptionSetForBoundsCheck(tree);
                break;
            }

            case GT_INTRINSIC:
            {
                assert(tree.AsIntrinsic().IntrinsicName is NI_System_Object_GetType);
                fgValueNumberAddExceptionSetForIndirection(tree, tree.AsIntrinsic().Op1);
                break;
            }

            case GT_XAND:
            case GT_XORR:
            case GT_XADD:
            case GT_XCHG:
            case GT_CMPXCHG:
            case GT_IND:
            case GT_BLK:
            case GT_STOREIND:
            case GT_STORE_BLK:
            case GT_NULLCHECK:
            {
                fgValueNumberAddExceptionSetForIndirection(tree, tree.AsIndir().Addr);
                break;
            }

            case GT_ARR_LENGTH:
            case GT_MDARR_LENGTH:
            case GT_MDARR_LOWER_BOUND:
            {
                fgValueNumberAddExceptionSetForIndirection(tree, tree.AsArrCommon().ArrRef);
                break;
            }

            case GT_CKFINITE:
            {
                fgValueNumberAddExceptionSetForCkFinite(tree);
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                break;
            }
#endif
            default:
            {
                throw new System.Diagnostics.UnreachableException(
                    $"Handle {tree.Oper} in fgValueNumberAddExceptionSet");
            }
        }
    }

#if DEBUG
    private static void fgDebugCheckExceptionSetsTree(GenTree tree, ValueNumStore store)
    {
        assert(tree._vnPair.BothDefined() || (tree.Oper is GT_PHI_ARG));
        var operandExceptions = ValueNumStore.VNPForEmptyExcSet();
        _ = tree.VisitOperands(operand =>
        {
            fgDebugCheckExceptionSetsTree(operand, store);
            var pair = operand._vnPair.BothDefined() ? operand._vnPair : ValueNumStore.VNPForVoid();
            operandExceptions = store.VNPUnionExcSet(pair, operandExceptions);
            return GenTree.VisitResult.Continue;
        });

        if ((tree.Flags & GTF_CALL) != 0)
        {
            return;
        }
        var nodeExceptions = store.VNPExceptionSet(tree._vnPair);
        assert(store.VNExcIsSubset(nodeExceptions.Liberal, operandExceptions.Liberal));
        assert(store.VNExcIsSubset(nodeExceptions.Conservative, operandExceptions.Conservative));
    }

    public void fgDebugCheckExceptionSets()
    {
        assert(vnStore is not null);
        foreach (var block in Blocks)
        {
            foreach (var stmt in block.Statements)
            {
                if (stmt.RootNode._vnPair.Liberal == ValueNumStore.NoVN)
                {
                    continue;
                }
                fgDebugCheckExceptionSetsTree(stmt.RootNode, vnStore);
            }
        }
    }
#endif
}
