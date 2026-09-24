// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTree;

namespace RyuJitSharp;

public partial class Compiler
{
    private unsafe GenTree fgMorphSmpOp(GenTree tree, out bool assertionPropDone)
    {
        assert(tree.Oper.IsSimple);
        assertionPropDone = false;
        var isQmarkColon = false;
        var originalAssertions = BitVecOps.UninitVal();
        var thenAssertions = BitVecOps.UninitVal();
        var operation = tree.Oper;
        var type = tree.Type;
        var op1 = tree.AsUnOp().Op1;
        var op2 = operation.IsBinary ? tree.AsOp().Op2 : null;

        GenTree MorphArithmeticHelper(CorInfoHelpFunc helper)
        {
            assert(tree.Oper.IsBinary && (op1 is not null) && (op2 is not null));
            var old = tree;
            tree = gtFoldExpr(tree);
            if (tree.Oper.IsLeaf || (old != tree))
            {
                return old != tree ? fgMorphTree(tree) : fgMorphLeaf(tree);
            }
            if (tree.Oper is GT_COMMA)
            {
                noway_assert(fgIsCommaThrow(tree));
                return fgMorphTree(tree);
            }
            return fgMorphIntoHelperCall(tree, helper, morphArgs: true, op1, op2);
        }

        // Preorder cannot assume canonical operands: either child may still
        // become a constant when recursively morphed.
        switch (operation)
        {
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                ref var descriptor = ref lvaGetDesc(tree.AsLclVarCommon().LclNum);
                if (descriptor.IsAddressExposed
#if FEATURE_IMPLICIT_BYREFS
                    || descriptor.lvIsLastUseCopyOmissionCandidate
#endif
                )
                {
                    tree.AddAllEffectsFlags(GTF_GLOB_REF);
                }
                var expanded = fgMorphExpandLocal(tree.AsLclVarCommon());
                if (expanded is not null)
                {
                    expanded.SetMorphed(this);
                    tree = expanded;
                    operation = tree.Oper;
                    op1 = tree.AsUnOp().Op1;
                    op2 = operation.IsBinary ? tree.AsOp().Op2 : null;
                }
                break;
            }

            case GT_QMARK:
            case GT_JTRUE:
            {
                assert(op1 is not null);
                if (op1.Oper.IsCompare)
                {
                    op1.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
                }
                else
                {
                    var effective = op1.EffectiveVal;
                    noway_assert((effective.Oper is GT_CNS_INT) && (effective.IsIntegralConst(0) || effective.IsIntegralConst(1)));
                }
                break;
            }

            case GT_COLON:
            {
                if (optLocalAssertionProp)
                {
                    assert(apTraits is not null);
                    isQmarkColon = true;
                    BitVecOps.ClearD(apTraits, apLocalPostorder);
                }
                break;
            }

            case GT_FIELD_ADDR:
            {
                return fgMorphFieldAddr(tree);
            }

            case GT_INDEX_ADDR:
            {
                return fgMorphIndexAddr(tree.AsIndexAddr());
            }

            case GT_CAST:
            {
                var cast = fgMorphExpandCast(tree.AsCast());
                if (cast is not null)
                {
                    return cast;
                }
                op1 = tree.AsCast().CastOp;
                break;
            }

            case GT_MUL:
            {
                assert(op2 is not null);
#if !TARGET_64BIT && !TARGET_WASM
                if (type is TYP_LONG)
                {
                    if (tree.Is64RsltMul)
                    {
                        tree = fgMorphLongMul(tree.AsOp());
                        goto DoneMorphingChildren;
                    }
                    tree = fgRecognizeAndMorphLongMul(tree.AsOp());
                    op1 = tree.AsOp().Op1;
                    op2 = tree.AsOp().Op2;
                    if (tree.Is64RsltMul)
                    {
                        goto DoneMorphingChildren;
                    }
                    var helper = tree.HasOverflowCheck
                        ? ((tree.Flags & GTF_UNSIGNED) != 0 ? CORINFO_HELP_ULMUL_OVF : CORINFO_HELP_LMUL_OVF)
                        : CORINFO_HELP_LMUL;
                    return MorphArithmeticHelper(helper);
                }
#endif
#if TARGET_WASM
                if (tree.HasOverflowCheck && (tree.Type is TYP_LONG))
                {
                    return MorphArithmeticHelper((tree.Flags & GTF_UNSIGNED) != 0 ? CORINFO_HELP_ULMUL_OVF : CORINFO_HELP_LMUL_OVF);
                }
#endif
                break;
            }

            case GT_ARR_LENGTH:
            {
                assert(op1 is not null);
                if (op1.Oper is GT_CNS_STR)
                {
                    var length = gtNewStringLiteralLength(op1.AsStrCon());
                    if (length is not null)
                    {
                        length.SetMorphed(this);
                        return length;
                    }
                }
                break;
            }

            case GT_IND:
            {
                if (opts.OptimizationEnabled)
                {
                    var constant = gtFoldIndirConst(tree.AsIndir());
                    if (constant is not null)
                    {
                        assert(constant.Oper.IsConst);
                        constant.SetMorphed(this);
                        return constant;
                    }
                }
                break;
            }

            case GT_STOREIND:
            {
                if (varTypeIsGC(tree.Type))
                {
                    var address = op1;
                    while ((address is not null) && (address.Oper is GT_FIELD_ADDR))
                    {
                        if (eeIsByrefLike(info.compCompHnd->getFieldClass(address.AsFieldAddr().FldHnd)))
                        {
#if DEBUG
                            JITDUMP($"Marking [{tree.TreeId:D6}] STOREIND as GTF_IND_TGT_NOT_HEAP: field's owner is a byref-like struct\n");
#endif
                            tree.Flags |= GTF_IND_TGT_NOT_HEAP;
                            break;
                        }
                        address = address.AsFieldAddr().FldObj;
                    }
                }
                break;
            }

            case GT_DIV:
            {
                assert((op1 is not null) && (op2 is not null));
                if (varTypeIsIntegral(tree.Type) && op1.IsNeverNegative(this) && op2.IsNeverNegative(this))
                {
                    tree.SetOper(GT_UDIV, PRESERVE_VN);
                    tree.Flags &= GTF_COMMON_MASK;
                    return fgMorphSmpOp(tree, out _);
                }
#if !TARGET_64BIT && !TARGET_WASM
                if (type is TYP_LONG)
                {
                    return MorphArithmeticHelper(CORINFO_HELP_LDIV);
                }
#if USE_HELPERS_FOR_INT_DIV
                if (type is TYP_INT)
                {
                    return MorphArithmeticHelper(CORINFO_HELP_DIV);
                }
#endif
#endif
                break;
            }

            case GT_UDIV:
            {
#if !TARGET_64BIT && !TARGET_WASM
                if (type is TYP_LONG)
                {
                    return MorphArithmeticHelper(CORINFO_HELP_ULDIV);
                }
#if USE_HELPERS_FOR_INT_DIV
                if (type is TYP_INT)
                {
                    return MorphArithmeticHelper(CORINFO_HELP_UDIV);
                }
#endif
#endif
                break;
            }

            case GT_MOD:
            case GT_UMOD:
            {
                assert((op1 is not null) && (op2 is not null));
                if (operation is GT_MOD)
                {
                    if (varTypeIsFloating(type))
                    {
                        var helper = CORINFO_HELP_DBLREM;
                        if (op1.Type is TYP_FLOAT)
                        {
                            if (op2.Type is TYP_FLOAT)
                            {
                                helper = CORINFO_HELP_FLTREM;
                            }
                            else
                            {
                                tree.AsOp().Op1 = op1 = gtNewCastNode(TYP_DOUBLE, op1, false, TYP_DOUBLE);
                            }
                        }
                        else if (op2.Type is TYP_FLOAT)
                        {
                            tree.AsOp().Op2 = op2 = gtNewCastNode(TYP_DOUBLE, op2, false, TYP_DOUBLE);
                        }
                        return MorphArithmeticHelper(helper);
                    }
                    if (varTypeIsIntegral(tree.Type) && op1.IsNeverNegative(this) && op2.IsNeverNegative(this))
                    {
                        tree.SetOper(GT_UMOD, PRESERVE_VN);
                        tree.Flags &= GTF_COMMON_MASK;
                        return fgMorphSmpOp(tree, out _);
                    }
                }
#if !TARGET_ARMARCH
                else if ((type is TYP_LONG) && opts.OptimizationEnabled
                    && (op2.Oper is GT_CNS_NATIVELONG) && (op2.AsIntConCommon().IntegralValue is >= 2 and <= 0x3FFFFFFF))
                {
                    op2.SetMorphed(this);
                    tree.AsOp().Op1 = op1 = fgMorphTree(op1);
                    noway_assert(op1.Type is TYP_LONG);
                    tree.Flags = (tree.Flags & ~GTF_ALL_EFFECT) | (op1.Flags & GTF_ALL_EFFECT);
                    if (op1.Oper is GT_CNS_NATIVELONG)
                    {
                        tree = gtFoldExpr(tree);
                    }
                    if (!tree.Oper.IsConst)
                    {
                        tree.AsOp().CheckDivideByConstOptimized(this);
                    }
                    return tree;
                }
#endif
                if (op2.IsIntegralConst(1))
                {
                    var optimized = fgMorphModToZero(tree.AsOp());
                    if (optimized is not null)
                    {
                        tree = optimized;
                        if (tree.Oper is GT_COMMA)
                        {
                            op1 = tree.AsOp().Op1;
                            op2 = tree.AsOp().Op2;
                        }
                        else
                        {
                            assert(tree.Oper.IsIntegralConst);
                            op1 = null;
                            op2 = null;
                        }
                        break;
                    }
                }
#if !TARGET_64BIT && !TARGET_WASM
                if (type is TYP_LONG)
                {
                    return MorphArithmeticHelper(operation is GT_UMOD ? CORINFO_HELP_ULMOD : CORINFO_HELP_LMOD);
                }
#if TARGET_ARM
                if (type is TYP_INT)
                {
                    return MorphArithmeticHelper(operation is GT_UMOD ? CORINFO_HELP_UMOD : CORINFO_HELP_MOD);
                }
#endif
#endif
                if ((tree.Oper is GT_UMOD) && op2.IsIntegralConstUnsignedPow2)
                {
                    tree = fgMorphUModToAndSub(tree.AsOp());
                    op1 = tree.AsOp().Op1;
                    op2 = tree.AsOp().Op2;
                }
#if TARGET_ARM64
                else
#else
                else if ((tree.Oper is GT_MOD) && op2.Oper.IsIntegralConst && !op2.IsIntegralConstAbsPow2)
#endif
                {
                    tree = fgMorphModToSubMulDiv(tree.AsOp());
                    op1 = tree.AsOp().Op1;
                    op2 = tree.AsOp().Op2;
                }
                break;
            }

            case GT_RETURN:
            case GT_SWIFT_ERROR_RET:
            {
                ref var value = ref (operation is GT_RETURN ? ref tree.AsUnOp().Op1Ref : ref tree.AsOp().Op2Ref);
                if ((tree.Type is not TYP_VOID) && ((genReturnBB is null) || (compCurBB == genReturnBB)))
                {
                    if (value.Oper is GT_LCL_FLD)
                    {
                        value = fgMorphRetInd(tree.AsUnOp());
                    }
                    fgTryReplaceStructLocalWithFields(ref value);
                }
                if (fgGlobalMorph && varTypeIsSmall(info.compRetType) && (value is not null)
                    && (value.Type is not TYP_VOID) && fgCastNeeded(value, info.compRetType))
                {
#if SWIFT_SUPPORT
                    if (operation is GT_SWIFT_ERROR_RET)
                    {
                        assert(op1 is not null);
                        var error = fgMorphTree(op1);
                        tree.AsUnOp().Op1 = error;
                        tree.SetAllEffectsFlags(error);
                    }
#endif
                    value = gtNewCastNode(TYP_INT, value, false, info.compRetType);
                    value.Flags |= tree.Flags & GTF_COLON_COND;
                    value = fgMorphTree(value);
                    tree.SetAllEffectsFlags(value);
                    return tree;
                }
                if (operation is GT_RETURN)
                {
                    op1 = value;
                }
                else
                {
                    op2 = value;
                }
                break;
            }

            case GT_EQ:
            case GT_NE:
            {
                assert((op1 is not null) && (op2 is not null));
                if (opts.OptimizationEnabled)
                {
                    var optimized = gtFoldTypeCompare(tree.AsOp());
                    if (optimized != tree)
                    {
                        return fgMorphTree(optimized);
                    }

                    // Do this before MOD can become a helper. The zero test of
                    // a power-of-two remainder works for negative dividends too.
                    var other = op2.IsIntegralConst(0) ? op1 : op1.IsIntegralConst(0) ? op2 : null;
                    if ((other is not null) && (other.Oper is GT_MOD) && varTypeIsIntegral(other.Type))
                    {
                        var divisor = other.AsOp().Op2;
                        if (divisor.Oper.IsCnsIntOrI)
                        {
                            var modulus = divisor.AsIntCon().IconValue;
                            if (nint.IsPow2(modulus))
                            {
                                JITDUMP("\nTransforming:\n");
                                DISPTREE(tree);
                                other.SetOper(GT_AND, PRESERVE_VN);
                                other.Flags &= ~(GTF_DIV_MOD_NO_OVERFLOW | GTF_DIV_MOD_NO_BY_ZERO);
                                divisor.AsIntConCommon().IconValue = modulus - 1;
                                fgUpdateConstTreeValueNumber(divisor);
                                JITDUMP("\ninto:\n");
                                DISPTREE(tree);
                            }
                        }
                    }
                }
                goto case GT_GT;
            }

            case GT_GT:
            {
                var optimized = gtFoldBoxNullable(tree.AsOp());
                if (optimized.Oper != tree.Oper)
                {
                    return optimized;
                }
                tree = optimized;
                op1 = tree.AsUnOp().Op1;
                op2 = tree.Oper.IsBinary ? tree.AsOp().Op2 : null;
                break;
            }

            case GT_RUNTIMELOOKUP:
            {
                assert(op1 is not null);
                return fgMorphTree(op1);
            }

            case GT_COMMA:
            {
                assert(op2 is not null);
                if (op2.Oper.IsStore || ((op2.Oper is GT_COMMA) && (op2.Type is TYP_VOID)) || fgIsThrow(op2))
                {
                    type = tree.Type = TYP_VOID;
                }
                break;
            }
        }

        if (opts.OptimizationEnabled && fgGlobalMorph)
        {
            var reduced = fgMorphReduceAddOps(tree);
            if (reduced != tree)
            {
                return fgMorphTree(reduced);
            }
        }

        if (op1 is not null)
        {
            if (isQmarkColon)
            {
                assert(optLocalAssertionProp && (apTraits is not null));
                BitVecOps.Assign(apTraits, ref originalAssertions, apLocal);
            }
            if (tree.Oper.IsIndir && !tree.Oper.IsAtomic)
            {
                fgMarkAddrModeForFieldAddr(tree.AsIndir());
            }
            tree.AsUnOp().Op1 = op1 = fgMorphTree(op1);
            if (isQmarkColon)
            {
                assert(optLocalAssertionProp && (apTraits is not null));
                BitVecOps.Assign(apTraits, ref thenAssertions, apLocal);
            }
        }
        if (op2 is not null)
        {
            if (isQmarkColon)
            {
                assert(optLocalAssertionProp && (apTraits is not null) && (apLocal is not null));
                BitVecOps.Assign(apTraits, ref apLocal, originalAssertions);
            }
            tree.AsOp().Op2 = op2 = fgMorphTree(op2);
            if (isQmarkColon)
            {
                assert(optLocalAssertionProp && (apTraits is not null));
                BitVecOps.IntersectionD(apTraits, apLocal, thenAssertions);
            }
        }
#if !TARGET_64BIT && !TARGET_WASM
    DoneMorphingChildren:
#endif
        gtUpdateNodeOperSideEffects(tree);
        if (op1 is not null)
        {
            tree.AddAllEffectsFlags(op1);
        }
        if (op2 is not null)
        {
            tree.AddAllEffectsFlags(op2);
        }
        if (varTypeIsGC(tree.Type) && (op1 is not null) && !varTypeIsGC(op1.Type)
            && (op2 is not null) && !varTypeIsGC(op2.Type))
        {
            tree.Type = (tree.Oper is GT_COMMA ? op2.Type : op1.Type).ActualType;
        }

        var oldTree = tree;
        GenTree? qmarkOp1 = null;
        GenTree? qmarkOp2 = null;
        if ((tree.Oper is GT_QMARK) && (tree.AsOp().Op2.Oper is GT_COLON))
        {
            qmarkOp1 = tree.AsOp().Op2.AsOp().Op1;
            qmarkOp2 = tree.AsOp().Op2.AsOp().Op2;
        }
        if (fgGlobalMorph && optLocalAssertionProp && (optAssertionCount > 0))
        {
            // apLocal may include assertions from reordered children; only
            // assertions from prior statements are safe in postorder.
            var optimized = tree;
            var again = JitConfig.JitEnablePostorderLocalAssertionProp > 0;
            var didOptimize = false;
            if (!again)
            {
                JITDUMP("*** Postorder assertion prop disabled by config\n");
            }
            while (again)
            {
                assert(optimized is not null);
                tree = optimized;
                optimized = optLocalAssertionPropTree(apLocalPostorder, tree);
                again = optimized is not null;
                didOptimize |= again;
            }
            if (didOptimize)
            {
                gtUpdateNodeSideEffects(tree);
            }
        }

        tree = gtFoldExpr(tree);
        if (oldTree != tree)
        {
            if ((tree == op1) || (tree == op2) || (tree == qmarkOp1) || (tree == qmarkOp2))
            {
                return tree;
            }
            if (fgIsCommaThrow(tree))
            {
                tree.AsOp().Op1 = fgMorphTree(tree.AsOp().Op1);
                fgMorphTreeDone(tree);
            }
            return tree;
        }
        if (tree.Oper.IsConst || tree.IsNothingNode)
        {
            return tree;
        }

        operation = tree.Oper;
        type = tree.Type;
        op1 = tree.AsUnOp().Op1;
        op2 = operation.IsBinary ? tree.AsOp().Op2 : null;
        switch (operation)
        {
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            case GT_STOREIND:
            {
                tree = fgOptimizeCastOnStore(tree);
                op1 = tree.AsUnOp().Op1;
                op2 = tree.Oper.IsBinary ? tree.AsOp().Op2 : null;
                if (tree.Oper is GT_STOREIND)
                {
                    var finalized = fgMorphFinalizeIndir(tree.AsIndir());
                    if (finalized is not null)
                    {
                        return finalized;
                    }
                }
                break;
            }

            case GT_CAST:
            {
                tree = fgOptimizeCast(tree.AsCast());
                if (!tree.Oper.IsSimple)
                {
                    return tree;
                }
                type = tree.Type;
                operation = tree.Oper;
                op1 = tree.AsUnOp().Op1;
                op2 = operation.IsBinary ? tree.AsOp().Op2 : null;
                break;
            }

            case GT_BITCAST:
            {
                var optimized = fgOptimizeBitCast(tree.AsUnOp());
                if (optimized is not null)
                {
                    return optimized;
                }
                break;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            {
                tree = fgOptimizeRelationalComparison(tree.AsOp());
                if (!tree.Oper.IsBinary)
                {
                    return tree;
                }
                type = tree.Type;
                operation = tree.Oper;
                op1 = tree.AsOp().Op1;
                op2 = tree.AsOp().Op2;
                break;
            }

            case GT_MUL:
            {
#if !TARGET_64BIT && !TARGET_WASM
                if (type is TYP_LONG)
                {
#if DEBUG
                    tree.AsOp().DebugCheckLongMul();
#endif
                    return tree;
                }
#endif
                goto case GT_ADD;
            }

            case GT_SUB:
            {
                assert((op1 is not null) && (op2 is not null));
                if (tree.HasOverflowCheck || !fgGlobalMorph)
                {
                    break;
                }
                if ((op2.Type is not TYP_BYREF) && opts.Tier0OptimizationEnabled)
                {
                    if (op2.Oper.IsCnsIntOrI && !op2.IsIconHandle())
                    {
                        op2.AsIntConCommon().SetValueTruncating(unchecked(-op2.AsIntConCommon().IconValue));
                        op2.AsIntCon().FieldSeq = null;
                        fgUpdateConstTreeValueNumber(op2);
                        operation = GT_ADD;
                        tree.SetOper(GT_ADD, PRESERVE_VN);
                        tree.Flags &= GTF_COMMON_MASK;
                        goto case GT_OR;
                    }
                    if (op1.Oper.IsCnsIntOrI)
                    {
                        noway_assert(varTypeIsIntegral(tree.Type));
                        if (op1.IsIntegralConst(0))
                        {
                            return new GenTreeUnOp(GT_NEG, op2.Type.ActualType, op2, tree, NodeThreading.None) {
                                _vnPair = tree._vnPair,
                            };
                        }
                        var negative = gtNewUnaryNode(GT_NEG, op2.Type.ActualType, op2);
                        fgMorphTreeDone(negative);
                        op2 = op1;
                        op1 = negative;
                        tree.AsOp().Op1 = op1;
                        tree.AsOp().Op2 = op2;
                        operation = GT_ADD;
                        tree.SetOper(GT_ADD, PRESERVE_VN);
                        tree.Flags &= GTF_COMMON_MASK;
                        goto case GT_OR;
                    }
                }
                if (opts.OptimizationEnabled && (op2.Oper is GT_NEG))
                {
                    if ((op1.Oper is GT_NEG) && gtCanSwapOrder(op1, op2))
                    {
                        var firstChild = op1.AsUnOp().Op1;
                        var secondChild = op2.AsUnOp().Op1;
                        tree.AsOp().Op1 = op1 = secondChild;
                        tree.AsOp().Op2 = op2 = firstChild;
                    }
                    else
                    {
                        operation = GT_ADD;
                        tree.SetOper(GT_ADD, PRESERVE_VN);
                        tree.AsOp().Op2 = op2 = op2.AsUnOp().Op1;
                    }
                }
                break;
            }

            case GT_DIV:
            case GT_UDIV:
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            case GT_MOD:
            case GT_UMOD:
#endif
            {
                assert((op1 is not null) && (op2 is not null));
#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
                if (!varTypeIsFloating(tree.Type))
                {
                    var exceptions = tree.Exceptions(this);
                    if ((operation is GT_DIV or GT_MOD) && ((exceptions & ExceptionSetFlags.ArithmeticException) == 0))
                    {
                        tree.Flags |= GTF_DIV_MOD_NO_OVERFLOW;
                    }
                    if ((exceptions & ExceptionSetFlags.DivideByZeroException) == 0)
                    {
                        tree.Flags |= GTF_DIV_MOD_NO_BY_ZERO;
                    }
                }
#endif
                if (opts.OptimizationDisabled)
                {
                    break;
                }
                if (op2.Oper.IsCnsFltOrDbl)
                {
                    if (op1.Oper is GT_NEG)
                    {
                        tree.AsOp().Op1 = op1 = op1.AsUnOp().Op1;
                        op2.AsDblCon().DconVal = -op2.AsDblCon().DconVal;
                        fgUpdateConstTreeValueNumber(op2);
                    }
                    if (fgGlobalMorph)
                    {
                        var divisor = op2.AsDblCon().DconVal;
                        bool transform;
                        if (type is TYP_DOUBLE)
                        {
                            transform = HasPreciseReciprocal(divisor);
                            divisor = 1.0 / divisor;
                        }
                        else
                        {
                            assert(type is TYP_FLOAT);
                            transform = HasPreciseReciprocal((float)divisor);
                            divisor = (float)(1.0 / divisor);
                        }
                        if (transform)
                        {
                            tree.SetOper(GT_MUL, PRESERVE_VN);
                            tree.Flags &= GTF_COMMON_MASK;
                            op2.AsDblCon().DconVal = divisor;
                            fgUpdateConstTreeValueNumber(op2);
                            operation = GT_MUL;
                            goto case GT_MUL;
                        }
                    }
                }
                break;
            }

            case GT_ADD:
            {
                if (tree.HasOverflowCheck)
                {
                    break;
                }
                goto case GT_OR;
            }

            case GT_OR:
            case GT_XOR:
            case GT_AND:
            {
                tree = fgOptimizeCommutativeArithmetic(tree.AsOp());
                if (!tree.Oper.IsSimple)
                {
                    return tree;
                }
                type = tree.Type;
                operation = tree.Oper;
                op1 = tree.AsUnOp().Op1;
                op2 = operation.IsBinary ? tree.AsOp().Op2 : null;
                break;
            }

            case GT_NOT:
            case GT_NEG:
            {
                assert(op1 is not null);
                if (opts.OptimizationDisabled)
                {
                    break;
                }
                if ((tree.Oper is GT_NEG) && (op1.Oper is GT_MUL or GT_DIV))
                {
                    var arithmetic = op1.AsOp();
                    var constant = arithmetic.Op2;
                    if ((constant.Oper.IsCnsIntOrI && !constant.IsIconHandle()) || constant.Oper.IsCnsFltOrDbl)
                    {
                        var canTransform = true;
                        if (constant.Oper.IsCnsIntOrI)
                        {
                            canTransform = arithmetic.Oper is GT_DIV
                                ? constant.AsIntCon().IconValue is not -1 and not 1
                                : !arithmetic.HasOverflowCheck;
                            if (canTransform)
                            {
                                constant.AsIntConCommon().SetValueTruncating(unchecked(-constant.AsIntConCommon().IconValue));
                                constant.AsIntCon().FieldSeq = null;
                            }
                        }
                        else
                        {
                            constant.AsDblCon().DconVal = -constant.AsDblCon().DconVal;
                        }
                        fgUpdateConstTreeValueNumber(constant);
                        if (canTransform)
                        {
                            arithmetic._vnPair = tree._vnPair;
                            return arithmetic;
                        }
                    }
                }
                noway_assert(!op1.Oper.IsConst || op1.IsIconHandle());
                break;
            }

            case GT_CKFINITE:
            {
                assert(op1 is not null);
                noway_assert(varTypeIsFloating(op1.Type));
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                MethodHasBoundsChecks = true;
                break;
            }

            case GT_IND:
            {
                assert(op1 is not null);
                if (op1.IsIconHandle())
                {
                    tree.Flags |= GTF_IND_NONFAULTING;
                    if (HandleKindDataIsInvariant(op1.AsIntCon().IconHandleFlag))
                    {
                        tree.Flags |= GTF_IND_INVARIANT;
                    }
                }
                var finalized = fgMorphFinalizeIndir(tree.AsIndir());
                if (finalized is not null)
                {
                    return finalized;
                }
                if (!varTypeIsStruct(tree.Type) && (op1.Oper is GT_COMMA) && fgGlobalMorph)
                {
                    var comma = op1.AsOp();
                    var flags = tree.Flags;
                    comma.Type = type;
                    comma.Flags = flags & ~GTF_REVERSE_OPS;
                    comma.SetMorphed(this);
                    while (comma.Op2.Oper is GT_COMMA)
                    {
                        comma = comma.Op2.AsOp();
                        comma.Type = type;
                        comma.Flags = (flags & ~(GTF_REVERSE_OPS | GTF_ASG | GTF_CALL))
                            | ((comma.Op1.Flags | comma.Op2.Flags) & (GTF_ASG | GTF_CALL));
                        comma.SetMorphed(this);
                    }
                    tree = op1;
                    var address = comma.Op2;
                    var indirection = gtNewIndir(type, address);
                    // Ordering may originate on the indirection, not its address.
                    indirection.Flags |= (flags & ~GTF_GLOB_EFFECT) | (address.Flags & GTF_ALL_EFFECT);
                    if (((flags & GTF_IND_NONFAULTING) != 0) && ((address.Flags & GTF_EXCEPT) == 0))
                    {
                        indirection.Flags &= ~GTF_EXCEPT;
                    }
                    indirection.Flags |= flags & GTF_GLOB_REF;
                    indirection.SetMorphed(this);
                    comma.Op2 = indirection;
                    comma.Flags |= indirection.Flags & GTF_ALL_EFFECT;
                    return tree;
                }
                break;
            }

            case GT_NULLCHECK:
            {
                assert(op1 is not null);
                if (opts.OptimizationEnabled && !tree.MayThrow(this))
                {
#if DEBUG
                    JITDUMP($"\nNULLCHECK on [{op1.TreeId:D6}] will always succeed\n");
#endif
                    if ((op1.Oper is GT_CALL) && !op1.AsCall().HasSideEffects(this))
                    {
                        // Helper properties can prove the root call effect-free
                        // even when its conservative cached flags still say CALL.
                        GenTree? effects = null;
                        gtExtractSideEffList(op1, ref effects, GTF_SIDE_EFFECT, ignoreRoot: true);
                        if (effects is not null)
                        {
                            tree = effects;
                            tree.SetMorphed(this, doChilren: true);
                        }
                        else
                        {
                            tree.BashToNOP();
                        }
                    }
                    else if ((op1.Flags & GTF_SIDE_EFFECT) != 0)
                    {
                        tree = gtUnusedValNode(op1);
                        tree.SetMorphed(this, doChilren: true);
                    }
                    else
                    {
                        tree.BashToNOP();
                    }
                    return tree;
                }
                break;
            }

            case GT_COLON:
            {
                if (fgGlobalMorph)
                {
                    var visitor = new MarkColonCondVisitor();
                    visitor.WalkTree(ref tree, null);
                }
                fgRemoveRestOfBlock = false;
                break;
            }

            case GT_COMMA:
            {
                assert((op1 is not null) && (op2 is not null));
                if (op2.Oper.IsStore || ((op2.Oper is GT_COMMA) && (op2.Type is TYP_VOID)) || fgIsThrow(op2))
                {
                    type = tree.Type = TYP_VOID;
                }
                GenTree? effects = null;
                gtExtractSideEffList(op1, ref effects, GTF_SIDE_EFFECT | GTF_MAKE_CSE);
                if (effects is not null)
                {
                    tree.AsOp().Op1 = op1 = effects;
                    gtUpdateNodeSideEffects(tree);
                }
                else
                {
                    op2.Flags |= tree.Flags & GTF_DONT_CSE;
                    return op2;
                }
                if (op2.IsNothingNode && (op1.Type is TYP_VOID) && !fgIsCommaThrow(tree))
                {
                    op1.Flags |= tree.Flags & GTF_DONT_CSE;
                    return op1;
                }
                break;
            }

            case GT_JTRUE:
            {
                assert(op1 is not null);
                if (fgRemoveRestOfBlock)
                {
                    if (fgIsCommaThrow(op1, forFolding: true))
                    {
#if DEBUG
                        JITDUMP($"Removing [{tree.TreeId:D6}] GT_JTRUE as the block now unconditionally throws an exception.\n");
#endif
                        return op1.AsOp().Op1;
                    }
                    noway_assert(op1.Oper.IsCompare && ((op1.Flags & GTF_EXCEPT) != 0));
#if DEBUG
                    JITDUMP($"Keeping side-effects by bashing [{tree.TreeId:D6}] GT_JTRUE into a GT_COMMA.\n");
#endif
                    tree = new GenTreeOp(GT_COMMA, tree.Type, op1, gtNewNothingNode(), tree, NodeThreading.None);
#if DEBUG
                    JITDUMP($"Also bashing [{op1.TreeId:D6}] (a relop) into a GT_COMMA.\n");
#endif
                    op1.SetOper(GT_COMMA);
                    op1.Flags &= GTF_COMMON_MASK & ~GTF_UNSIGNED;
                    op1.Type = op1.AsOp().Op1.Type;
                    return tree;
                }
                break;
            }

            case GT_INTRINSIC:
            {
                assert(op1 is not null);
                if (tree.AsIntrinsic().IntrinsicName is NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant)
                {
                    JITDUMP("\nExpanding RuntimeHelpers.IsKnownConstant to ");
                    if (op1.Oper.IsConst || gtIsTypeof(op1))
                    {
                        JITDUMP("true\n");
                        tree = gtNewTrue();
                    }
                    else
                    {
                        JITDUMP("false\n");
                        tree = gtNewFalse();
                        tree.SetMorphed(this);
                        tree = gtWrapWithSideEffects(tree, op1, GTF_ALL_EFFECT);
                    }
                    tree.SetMorphed(this);
                    return tree;
                }
                break;
            }

            case GT_RETURN:
            case GT_SWIFT_ERROR_RET:
            {
                ref var value = ref (operation is GT_RETURN ? ref tree.AsUnOp().Op1Ref : ref tree.AsOp().Op2Ref);
                if ((value is not null) && ((genReturnBB is null) || (compCurBB == genReturnBB))
                    && fgTryReplaceStructLocalWithFields(ref value))
                {
                    value = fgMorphTree(value);
                }
                break;
            }
        }

        assert(operation == tree.Oper);
        if (fgGlobalMorph && (operation is not GT_COLON) && !operation.IsStore)
        {
            if ((op1 is not null) && fgIsCommaThrow(op1, forFolding: true))
            {
                var propagated = fgPropagateCommaThrow(tree, op1.AsOp(), GTF_EMPTY);
                if (propagated is not null)
                {
                    return propagated;
                }
            }
            if ((op2 is not null) && fgIsCommaThrow(op2, forFolding: true))
            {
                assert(op1 is not null);
                var propagated = fgPropagateCommaThrow(tree, op2.AsOp(), op1.Flags & GTF_ALL_EFFECT);
                if (propagated is not null)
                {
                    return propagated;
                }
            }
        }
        if ((opts.compFlags & CLFLG_TREETRANS) == 0)
        {
            return tree;
        }
        return fgMorphSmpOpOptional(tree.AsUnOp(), ref assertionPropDone);
    }
}

internal struct MarkColonCondVisitor : IGenTreeVisitor<MarkColonCondVisitor>
{
    public static bool DoPreOrder => true;

    private readonly GenTreeStack _ancestors;

    public MarkColonCondVisitor()
    {
        _ancestors = [];
    }

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        use.Flags |= GTF_COLON_COND;
        return Compiler.WALK_CONTINUE;
    }

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<MarkColonCondVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
