// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LOWER_DECOMPOSE_LONGS
using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private sealed partial class DecomposeLongs
    {
        private GenTree? DecomposeLclVar(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_LCL_VAR);
            var tree = use.Def();
            var varNum = tree.AsLclVarCommon().LclNum;
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            var loResult = tree;
            loResult.Type = TYP_INT;
            GenTree hiResult = _compiler.gtNewLclvNode(TYP_INT, varNum);
            Range().InsertAfter(loResult, hiResult);

            if (varDsc.lvPromoted)
            {
                assert(varDsc.lvFieldCnt == 2);
                var loVarNum = varDsc.lvFieldLclStart;
                loResult.AsLclVarCommon().LclNum = loVarNum;
                hiResult.AsLclVarCommon().LclNum = loVarNum + 1;
            }
            else
            {
                _compiler.lvaSetVarDoNotEnregister(varNum, DoNotEnregisterReason.LocalField);
                var loField = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, varNum, 0, null, null,
                    loResult, NodeThreading.LIR) {
                    Flags = loResult.Flags,
                };
                loField.CopySsaIdentityFrom(loResult.AsLclVarCommon());
                loField._vnPair.SetBoth(ValueNumStore.NoVN);
                Range().ReplaceNode(loResult, loField);
                use.ReplaceWith(loField);
                loResult = loField;

                var hiField = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, varNum, 4, null, null,
                    hiResult, NodeThreading.LIR);
                hiField._vnPair.SetBoth(ValueNumStore.NoVN);
                Range().ReplaceNode(hiResult, hiField);
                hiResult = hiField;
            }

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }

        private GenTree? DecomposeLclFld(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_LCL_FLD);
            var loResult = use.Def().AsLclFld();
            loResult.Type = TYP_INT;
            var hiResult = _compiler.gtNewLclFldNode(TYP_INT, loResult.LclNum,
                checked((ushort)(loResult.LclOffs + 4)));
            Range().InsertAfter(loResult, hiResult);

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }

        private GenTree? DecomposeStoreLclVar(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_STORE_LCL_VAR);
            var tree = use.Def();
            var rhs = tree.AsUnOp().Op1;
            if (rhs.Oper is GT_CALL or GT_MUL_LONG)
            {
                return tree.Next;
            }

            noway_assert(rhs.Oper is GT_LONG);
            ref var varDsc = ref _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum);
            if (!varDsc.lvPromoted)
            {
                // Splitting a whole-local definition into two partial definitions changes
                // liveness. Codegen splits this store without changing its IR semantics.
                return tree.Next;
            }

            assert(varDsc.lvFieldCnt == 2);
            var value = rhs.AsOp();
            Range().Remove(value);
            var loVarNum = varDsc.lvFieldLclStart;
            tree.AsLclVarCommon().LclNum = loVarNum;
            tree.AsUnOp().Op1 = value.Op1;
            tree.Type = TYP_INT;

            var hiStore = new GenTreeLclVar(TYP_INT, loVarNum + 1, value.Op2) {
                Flags = GTF_VAR_DEF,
            };
            Range().InsertAfter(tree, hiStore);

            return hiStore.Next;
        }

        private GenTree? DecomposeStoreLclFld(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_STORE_LCL_FLD);
            var loStore = use.Def().AsLclFld();
            var value = loStore.Op1.AsOp();
            assert(value.Oper is GT_LONG);
            Range().Remove(value);

            loStore.Op1 = value.Op1;
            loStore.Type = TYP_INT;
            loStore.Flags |= GTF_VAR_USEASG;
            var hiStore = _compiler.gtNewStoreLclFldNode(TYP_INT, loStore.LclNum,
                checked((ushort)(loStore.LclOffs + 4)), value.Op2);
            Range().InsertAfter(loStore, hiStore);

            return hiStore.Next;
        }

        private GenTree? DecomposeCnsLng(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_CNS_LNG);
            var tree = use.Def().AsLngCon();
            var loVal = tree.LoVal;
            var hiVal = tree.HiVal;
            var loResult = new GenTreeIntCon(TYP_INT, loVal, null, tree, NodeThreading.LIR);
            loResult._vnPair.SetBoth(ValueNumStore.NoVN);
            Range().ReplaceNode(tree, loResult);
            use.ReplaceWith(loResult);

            var hiResult = _compiler.gtNewIconNode(TYP_INT, hiVal);
            Range().InsertAfter(loResult, hiResult);

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }

        private GenTree? DecomposeFieldList(GenTreeFieldList fieldList, GenTreeOp longNode)
        {
            assert(longNode.Oper is GT_LONG);
            GenTreeFieldList.Use? loUse = null;
            foreach (var fieldUse in fieldList.Uses)
            {
                if (fieldUse.Node == longNode)
                {
                    loUse = fieldUse;
                    break;
                }
            }

            assert(loUse is not null);
            Range().Remove(longNode);
            loUse.Node = longNode.Op1;
            loUse.Type = TYP_INT;
            fieldList.InsertFieldLIR(_compiler, loUse, longNode.Op2,
                checked((ushort)(loUse.Offset + 4)), TYP_INT);

            return fieldList.Next;
        }

        private GenTree? DecomposeCall(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_CALL);

            return StoreNodeToVar(ref use);
        }

        private GenTreeStoreInd DecomposeStoreInd(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_STOREIND);
            var tree = use.Def().AsStoreInd();
            assert(tree.Data.Oper is GT_LONG);
            var gtLong = tree.Data.AsOp();
            var address = new LIR.Use(Range(), ref tree.AddrRef, tree);
            _ = address.ReplaceWithLclVar(_compiler);
            JITDUMP("[DecomposeStoreInd]: Saving address tree to a temp var:\n");
            DISPTREERANGE(Range(), address.Def());

            if (!gtLong.Op1.Oper.IsLeaf)
            {
                var op1 = new LIR.Use(Range(), ref gtLong.Op1Ref, gtLong);
                _ = op1.ReplaceWithLclVar(_compiler);
                JITDUMP("[DecomposeStoreInd]: Saving low data tree to a temp var:\n");
                DISPTREERANGE(Range(), op1.Def());
            }

            if (!gtLong.Op2.Oper.IsLeaf)
            {
                var op2 = new LIR.Use(Range(), ref gtLong.Op2Ref, gtLong);
                _ = op2.ReplaceWithLclVar(_compiler);
                JITDUMP("[DecomposeStoreInd]: Saving high data tree to a temp var:\n");
                DISPTREERANGE(Range(), op2.Def());
            }

            var addrBase = tree.Addr;
            var dataHigh = gtLong.Op2;
            var dataLow = gtLong.Op1;
            Range().Remove(gtLong);
            Range().Remove(dataHigh);
            tree.Data = dataLow;
            tree.Type = TYP_INT;

            var addrBaseHigh = new GenTreeLclVar(addrBase.Type, addrBase.AsLclVarCommon().LclNum);
            var addrHigh = new GenTreeAddrMode(TYP_REF, addrBaseHigh, null, 0, (int)TYP_INT.Size);
            var storeIndHigh = new GenTreeStoreInd(TYP_INT, addrHigh, dataHigh) {
                Flags = tree.Flags & (GTF_ALL_EFFECT | GTF_LIVENESS_MASK),
            };
            Range().InsertAfter(tree, dataHigh, addrBaseHigh, addrHigh, storeIndHigh);

            return storeIndHigh;
        }

        private GenTree? DecomposeInd(ref LIR.Use use)
        {
            var indLow = use.Def().AsIndir();
            var address = new LIR.Use(Range(), ref indLow.AddrRef, indLow);
            _ = address.ReplaceWithLclVar(_compiler);
            JITDUMP("[DecomposeInd]: Saving addr tree to a temp var:\n");
            DISPTREERANGE(Range(), address.Def());
            indLow.Type = TYP_INT;

            var addrBase = indLow.Addr;
            var addrBaseHigh = new GenTreeLclVar(addrBase.Type, addrBase.AsLclVarCommon().LclNum);
            var addrHigh = new GenTreeAddrMode(TYP_REF, addrBaseHigh, null, 0, (int)TYP_INT.Size);
            var indHigh = new GenTreeIndir(GT_IND, TYP_INT, addrHigh);
            indHigh.Flags |= indLow.Flags & (GTF_GLOB_REF | GTF_EXCEPT | GTF_IND_FLAGS);
            Range().InsertAfter(indLow, addrBaseHigh, addrHigh, indHigh);

            return FinalizeDecomposition(ref use, indLow, indHigh, indHigh);
        }

        private GenTree? DecomposeNot(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_NOT);
            var loResult = use.Def().AsUnOp();
            var gtLong = loResult.Op1.AsOp();
            noway_assert(gtLong.Oper is GT_LONG);
            var loOp1 = gtLong.Op1;
            var hiOp1 = gtLong.Op2;
            Range().Remove(gtLong);

            loResult.Type = TYP_INT;
            loResult.Op1 = loOp1;
            var hiResult = _compiler.gtNewUnaryNode(GT_NOT, TYP_INT, hiOp1);
            Range().InsertAfter(loResult, hiResult);

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }

        private GenTree? DecomposeNeg(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_NEG);
            var loResult = use.Def().AsUnOp();
            var gtLong = loResult.Op1.AsOp();
            noway_assert(gtLong.Oper is GT_LONG);
            var loOp1 = gtLong.Op1;
            var hiOp1 = gtLong.Op2;
            Range().Remove(gtLong);

            loResult.Type = TYP_INT;
            loResult.Op1 = loOp1;
            var zero = _compiler.gtNewZeroConNode(TYP_INT);
#if TARGET_X86
            var hiAdjust = _compiler.gtNewBinaryNode(GT_ADD_HI, TYP_INT, hiOp1, zero);
            var hiResult = _compiler.gtNewUnaryNode(GT_NEG, TYP_INT, hiAdjust);
            Range().InsertAfter(loResult, zero, hiAdjust, hiResult);
            loResult.Flags |= GTF_SET_FLAGS;
#elif TARGET_ARM
            // MOVS may materialize zero, so it must precede the low NEG's carry flags.
            var hiResult = _compiler.gtNewBinaryNode(GT_SUB_HI, TYP_INT, zero, hiOp1);
            Range().InsertBefore(loResult, zero);
            Range().InsertAfter(loResult, hiResult);
            loResult.Flags |= GTF_SET_FLAGS;
#else
            throw new NotImplementedException("Long negation decomposition requires x86 or ARM32.");
#endif

#if TARGET_X86 || TARGET_ARM
            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
#endif
        }

        private GenTree? DecomposeArith(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            var loResult = use.Def().AsOp();
            var oper = loResult.Oper;
            assert(oper is GT_ADD or GT_SUB or GT_OR or GT_XOR or GT_AND);
            var op1 = loResult.Op1.AsOp();
            var op2 = loResult.Op2.AsOp();
            noway_assert((op1.Oper is GT_LONG) && (op2.Oper is GT_LONG));
            var loOp1 = op1.Op1;
            var hiOp1 = op1.Op2;
            var loOp2 = op2.Op1;
            var hiOp2 = op2.Op2;
            Range().Remove(op1);
            Range().Remove(op2);

            loResult.SetOper(GetLoOper(oper));
            loResult.Type = TYP_INT;
            loResult.Op1 = loOp1;
            loResult.Op2 = loOp2;
            var hiResult = new GenTreeOp(GetHiOper(oper), TYP_INT, hiOp1, hiOp2);
            Range().InsertAfter(loResult, hiResult);
            if (oper is GT_ADD or GT_SUB)
            {
                loResult.Flags |= GTF_SET_FLAGS;
                if ((loResult.Flags & GTF_OVERFLOW) != 0)
                {
                    hiResult.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
                    loResult.Flags &= ~(GTF_OVERFLOW | GTF_EXCEPT);
                }

                if (loResult.IsUnsigned)
                {
                    hiResult.IsUnsigned = true;
                }
            }

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }

        private GenTree? DecomposeSelect(ref LIR.Use use)
        {
            var select = use.Def().AsConditional();
            var op1 = select.Op1.AsOp();
            var op2 = select.Op2.AsOp();
            assert(op1.Oper is GT_LONG);
            assert(op2.Oper is GT_LONG);
            var loOp1 = op1.Op1;
            var hiOp1 = op1.Op2;
            var loOp2 = op2.Op1;
            var hiOp2 = op2.Op2;
            select.Type = TYP_INT;
            select.Op1 = loOp1;
            select.Op2 = loOp2;
            Range().Remove(op1);
            Range().Remove(op2);

            select.Flags |= GTF_SET_FLAGS;
            var hiSelect = new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(GenCondition.NE), hiOp1, hiOp2);
            Range().InsertAfter(select, hiSelect);

            return FinalizeDecomposition(ref use, select, hiSelect, hiSelect);
        }

        private GenTree? DecomposeMul(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            var tree = use.Def().AsOp();
            assert(tree.Oper is GT_MUL);
            assert(tree.Is64RsltMul);
            var op1 = tree.Op1;
            var op2 = tree.Op2;
            assert((op1.Type is TYP_LONG) && (op2.Type is TYP_LONG));
            assert(op1.Oper is GT_CAST);
            if (op2.Oper is not GT_CAST)
            {
                assert(op2.Oper is GT_LONG);
                assert(op2.AsOp().Op1.Oper.IsIntegralConst);
                assert(op2.AsOp().Op2.Oper.IsIntegralConst);
                Range().Remove(op2.AsOp().Op2);
            }

            Range().Remove(op1);
            Range().Remove(op2);
            tree.Op1 = op1.AsUnOp().Op1;
            tree.Op2 = op2.AsUnOp().Op1;
            var multiply = CreateMultiRegMultiply(tree);
            Range().ReplaceNode(tree, multiply);
            use.ReplaceWith(multiply);

            return StoreNodeToVar(ref use);
        }

        private static GenTreeMultiRegOp CreateMultiRegMultiply(GenTreeOp tree)
        {
            var multiply = new GenTreeMultiRegOp(GT_MUL_LONG, TYP_LONG, tree.Op1, tree.Op2, tree, NodeThreading.LIR) {
                Flags = tree.Flags,
            };
            return multiply;
        }

        private GenTree? DecomposeUMod(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            var loResult = use.Def().AsOp();
            assert(loResult.Oper is GT_UMOD);
            assert(loResult.Op1.Oper is GT_LONG);
            var op2 = loResult.Op2.AsOp();
            assert(op2.Oper is GT_LONG);
            var loOp2 = op2.Op1;
            var hiOp2 = op2.Op2;
            assert(loOp2.Oper is GT_CNS_INT);
            assert(hiOp2.Oper is GT_CNS_INT);
            assert((loOp2.AsIntCon().IconValue >= 2) && (loOp2.AsIntCon().IconValue <= 0x3fffffff));
            assert(hiOp2.AsIntCon().IconValue == 0);
            Range().Remove(hiOp2);
            Range().Remove(op2);

            loResult.Op2 = loOp2;
            loResult.Type = TYP_INT;
            var hiResult = _compiler.gtNewZeroConNode(TYP_INT);
            Range().InsertAfter(loResult, hiResult);

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }
    }
}
#endif
