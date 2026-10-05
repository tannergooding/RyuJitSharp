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
        private readonly Compiler _compiler;
#if FEATURE_HW_INTRINSICS && TARGET_X86
        private readonly Lowering _lowering;
#endif
        private LIR.Range? _range;

        public DecomposeLongs(Compiler compiler, Lowering lowering)
        {
            _compiler = compiler;
#if FEATURE_HW_INTRINSICS && TARGET_X86
            _lowering = lowering;
#endif
        }

        private LIR.Range Range()
        {
            assert(_range is not null);
            return _range;
        }

        public void PrepareForDecomposition()
        {
            PromoteLongVars();
        }

        public void DecomposeBlock(BasicBlock block)
        {
            assert(block == _compiler.compCurBB);
            assert(block.IsEmpty || block.IsLIR);
            _range = block;
            DecomposeRangeHelper();
        }

        public static void DecomposeRange(Compiler compiler, Lowering lowering, LIR.Range range)
        {
            assert(compiler is not null);
            var decomposer = new DecomposeLongs(compiler, lowering) { _range = range };
            decomposer.DecomposeRangeHelper();
        }

        private void DecomposeRangeHelper()
        {
            assert(_range is not null);
            var node = Range().FirstNode;
            while (node is not null)
            {
                node = DecomposeNode(node);
            }
        }

        private GenTree? DecomposeNode(GenTree tree)
        {
            if ((tree.Type is TYP_INT) && tree.Oper.IsLocal)
            {
                ref var varDsc = ref _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum);
                if (varTypeIsLong(varDsc.Type) && varDsc.lvPromoted)
                {
                    JITDUMP("Changing implicit reference to lo half of long lclVar to an explicit reference of its promoted half:\n");
                    DISPTREERANGE(Range(), tree);
                    tree.AsLclVarCommon().LclNum = varDsc.lvFieldLclStart;
                    return tree.Next;
                }
            }

#if FEATURE_HW_INTRINSICS && TARGET_X86
            if ((tree.Type is not TYP_LONG) &&
                !((tree.Oper is GT_CAST) && varTypeIsLong(tree.AsCast().CastOp.Type) && varTypeIsFloating(tree.Type)))
#else
            if (tree.Type is not TYP_LONG)
#endif
            {
                return tree.Next;
            }

            if (Range().TryGetUse(tree, out var use))
            {
                return DecomposeNode(tree, ref use);
            }

            LIR.Use.MakeDummyUse(Range(), tree, out var dummyUse);
            return DecomposeNode(tree, ref dummyUse);
        }

        private GenTree? DecomposeNode(GenTree tree, ref LIR.Use use)
        {
#if FEATURE_HW_INTRINSICS && TARGET_X86
            if (!use.IsDummyUse())
            {
                var user = use.User();
                if ((tree.Type is TYP_LONG) &&
                    ((user.Oper is GT_HWINTRINSIC) || ((user.Oper is GT_CAST) && varTypeIsFloating(user.Type))))
                {
                    if ((tree.Oper is GT_CNS_LNG) ||
                        ((tree.Oper is GT_IND or GT_LCL_FLD) && _lowering.IsSafeToContainMem(user, tree)))
                    {
                        if (user.Oper is GT_HWINTRINSIC)
                        {
                            var intrinsicId = user.AsHWIntrinsic().HWIntrinsicId;
                            assert(HWIntrinsicInfo.IsVectorCreate(intrinsicId) ||
                                HWIntrinsicInfo.IsVectorCreateScalar(intrinsicId) ||
                                HWIntrinsicInfo.IsVectorCreateScalarUnsafe(intrinsicId));
                        }

                        return tree.Next;
                    }
                }
                else if ((user.Oper is GT_STOREIND) && (tree.Oper is GT_HWINTRINSIC) &&
                    _compiler.opts.Tier0OptimizationEnabled)
                {
                    if (HWIntrinsicInfo.IsVectorToScalar(tree.AsHWIntrinsic().HWIntrinsicId) &&
                        _lowering.IsSafeToContainMem(user, tree))
                    {
                        return tree.Next;
                    }
                }
            }

            if ((tree.Oper is GT_STOREIND) && (tree.AsStoreInd().Data.Oper is GT_HWINTRINSIC))
            {
                assert(HWIntrinsicInfo.IsVectorToScalar(tree.AsStoreInd().Data.AsHWIntrinsic().HWIntrinsicId));
                return tree.Next;
            }
#endif

            JITDUMP("Decomposing TYP_LONG tree.  BEFORE:\n");
            DISPTREERANGE(Range(), tree);

            GenTree? nextNode = null;
            switch (tree.Oper)
            {
                case GT_LCL_VAR:
                {
                    nextNode = DecomposeLclVar(ref use);
                    break;
                }

                case GT_LCL_FLD:
                {
                    nextNode = DecomposeLclFld(ref use);
                    break;
                }

                case GT_STORE_LCL_VAR:
                {
                    nextNode = DecomposeStoreLclVar(ref use);
                    break;
                }

                case GT_CAST:
                {
                    nextNode = DecomposeCast(ref use);
                    break;
                }

                case GT_CNS_LNG:
                {
                    nextNode = DecomposeCnsLng(ref use);
                    break;
                }

                case GT_CALL:
                {
                    nextNode = DecomposeCall(ref use);
                    break;
                }

                case GT_RETURN:
                case GT_SWIFT_ERROR_RET:
                {
                    assert(GetRetValueRef(tree.AsUnOp()).Oper is GT_LONG);
                    break;
                }

                case GT_STOREIND:
                {
                    nextNode = DecomposeStoreInd(ref use);
                    break;
                }

                case GT_STORE_LCL_FLD:
                {
                    nextNode = DecomposeStoreLclFld(ref use);
                    break;
                }

                case GT_IND:
                {
                    nextNode = DecomposeInd(ref use);
                    break;
                }

                case GT_NOT:
                {
                    nextNode = DecomposeNot(ref use);
                    break;
                }

                case GT_NEG:
                {
                    nextNode = DecomposeNeg(ref use);
                    break;
                }

                case GT_ADD:
                case GT_SUB:
                case GT_OR:
                case GT_XOR:
                case GT_AND:
                {
                    nextNode = DecomposeArith(ref use);
                    break;
                }

                case GT_MUL:
                {
                    nextNode = DecomposeMul(ref use);
                    break;
                }

                case GT_UMOD:
                {
                    nextNode = DecomposeUMod(ref use);
                    break;
                }

                case GT_LSH:
                case GT_RSH:
                case GT_RSZ:
                {
                    nextNode = DecomposeShift(ref use);
                    break;
                }

                case GT_ROL:
                case GT_ROR:
                {
                    nextNode = DecomposeRotate(ref use);
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case GT_HWINTRINSIC:
                {
                    nextNode = DecomposeHWIntrinsic(ref use);
                    break;
                }
#endif

                case GT_SELECT:
                {
                    nextNode = DecomposeSelect(ref use);
                    break;
                }

                case GT_LOCKADD:
                case GT_XORR:
                case GT_XAND:
                case GT_XADD:
                case GT_XCHG:
                case GT_CMPXCHG:
                {
                    NYI("Interlocked operations on TYP_LONG");
                    throw new NotImplementedException("Interlocked operations on TYP_LONG");
                }

                default:
                {
                    JITDUMP($"Illegal TYP_LONG node {tree.Oper.Name} in Decomposition.");
                    throw new InvalidOperationException("Illegal TYP_LONG node in Decomposition.");
                }
            }

            if ((use.Def().Oper is GT_LONG) && !use.IsDummyUse() && (use.User().Oper is GT_FIELD_LIST))
            {
                _ = DecomposeFieldList(use.User().AsFieldList(), use.Def().AsOp());
            }

            JITDUMP("Decomposing TYP_LONG tree.  AFTER:\n");
            DISPTREERANGE(Range(), use.Def());

            if (_compiler.opts.OptimizationEnabled && !use.IsDummyUse() &&
                (use.User().Oper is GT_CAST) && (use.User().Type is TYP_INT) && (use.Def().Oper is GT_LONG))
            {
                nextNode = OptimizeCastFromDecomposedLong(use.User().AsCast(), nextNode);
            }

            return nextNode;
        }

        private GenTree? FinalizeDecomposition(ref LIR.Use use, GenTree loResult, GenTree hiResult,
            GenTree insertResultAfter)
        {
            assert(use.IsInitialized());
#if DEBUG
            assert(Range().Contains(loResult));
            assert(Range().Contains(hiResult));
#endif

            var gtLong = new GenTreeOp(GT_LONG, TYP_LONG, loResult, hiResult);
            if (use.IsDummyUse())
            {
                gtLong.IsUnusedValue = true;
            }

            loResult.IsUnusedValue = false;
            hiResult.IsUnusedValue = false;
            Range().InsertAfter(insertResultAfter, gtLong);
            use.ReplaceWith(gtLong);

            return gtLong.Next;
        }

        private GenTree? OptimizeCastFromDecomposedLong(GenTreeCast cast, GenTree? nextNode)
        {
            var src = cast.CastOp.AsOp();
            var dstType = cast.CastType;
            assert(src.Oper is GT_LONG);
            assert(dstType.ActualType is TYP_INT);
            if (cast.HasOverflowCheck)
            {
                return nextNode;
            }

            var loSrc = src.Op1;
            var hiSrc = src.Op2;
#if DEBUG
            JITDUMP($"Optimizing a truncating cast [{cast.TreeId:D6}] from decomposed LONG [{src.TreeId:D6}]\n");
            GenTree treeToDisplay = cast;
#endif
            if ((hiSrc.Flags & (GTF_ALL_EFFECT | GTF_SET_FLAGS)) == 0)
            {
#if DEBUG
                JITDUMP($"Removing the HI part of [{src.TreeId:D6}] and marking its operands unused:\n");
#endif
                DISPNODE(hiSrc);
                Range().Remove(hiSrc, true);
            }
            else
            {
#if DEBUG
                JITDUMP($"The HI part of [{src.TreeId:D6}] has side effects, marking it unused\n");
#endif
                hiSrc.IsUnusedValue = true;
            }

            JITDUMP("Removing the LONG source:\n");
            DISPNODE(src);
            Range().Remove(src);
            if (varTypeIsSmall(dstType))
            {
#if DEBUG
                JITDUMP($"Cast is to a small type, keeping it, the new source is [{loSrc.TreeId:D6}]\n");
#endif
                cast.Op1 = loSrc;
            }
            else
            {
                if (Range().TryGetUse(cast, out var useOfCast))
                {
                    useOfCast.ReplaceWith(loSrc);
                }
                else
                {
                    loSrc.IsUnusedValue = true;
                }

                if (nextNode == cast)
                {
                    nextNode = cast.Next;
                }

#if DEBUG
                treeToDisplay = loSrc;
#endif
                JITDUMP("Removing the cast:\n");
                DISPNODE(cast);
                Range().Remove(cast);
            }

            JITDUMP("Final result:\n");
#if DEBUG
            DISPTREERANGE(Range(), treeToDisplay);
#endif
            return nextNode;
        }

        private GenTree? StoreNodeToVar(ref LIR.Use use)
        {
            if (use.IsDummyUse())
            {
                return use.Def().Next;
            }

            var tree = use.Def();
            var user = use.User();
            if (user.Oper is GT_STORE_LCL_VAR)
            {
                _compiler.lvaGetDesc(user.AsLclVar().LclNum).IsMultiRegDest = true;
                return tree.Next;
            }

            var lclNum = use.ReplaceWithLclVar(_compiler);
            _compiler.lvaGetDesc(lclNum).IsMultiRegDest = true;
            if (_compiler.lvaEnregMultiRegVars)
            {
                TryPromoteLongVar(lclNum);
            }

            return DecomposeLclVar(ref use);
        }

        private GenTree RepresentOpAsLocalVar(GenTree op, GenTree user, ref GenTree edge)
        {
            if (op.Oper is GT_LCL_VAR)
            {
                return op;
            }

            var opUse = new LIR.Use(Range(), ref edge, user);
            _ = opUse.ReplaceWithLclVar(_compiler);

            return edge;
        }

        private GenTree EnsureIntSized(GenTree node, bool signExtend)
        {
            if (!varTypeIsSmall(node.Type))
            {
                assert(node.Type.Size == TYP_INT.Size);
                return node;
            }

            if ((node.Oper is GT_LCL_VAR) && !_compiler.lvaGetDesc(node.AsLclVarCommon().LclNum).lvNormalizeOnLoad)
            {
                node.Type = TYP_INT;
                return node;
            }

            var cast = _compiler.gtNewCastNode(TYP_INT, node, !signExtend, node.Type);
            Range().InsertAfter(node, cast);

            return cast;
        }

        private static genTreeOps GetHiOper(genTreeOps oper)
        {
            return oper switch {
                GT_ADD => GT_ADD_HI,
                GT_SUB => GT_SUB_HI,
                GT_OR => GT_OR,
                GT_AND => GT_AND,
                GT_XOR => GT_XOR,
                _ => throw new InvalidOperationException("GetHiOper called for invalid oper"),
            };
        }

        private static genTreeOps GetLoOper(genTreeOps oper)
        {
            return oper switch {
                GT_ADD => GT_ADD_LO,
                GT_SUB => GT_SUB_LO,
                GT_OR => GT_OR,
                GT_AND => GT_AND,
                GT_XOR => GT_XOR,
                _ => throw new InvalidOperationException("GetLoOper called for invalid oper"),
            };
        }

        private void PromoteLongVars()
        {
            if (!_compiler.compEnregLocals)
            {
                return;
            }

            var startLvaCount = _compiler.lvaCount;
            for (var lclNum = 0; lclNum < startLvaCount; lclNum++)
            {
                if (!varTypeIsLong(_compiler.lvaGetDesc(lclNum).Type))
                {
                    continue;
                }

                TryPromoteLongVar(lclNum);
            }

#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("\nlvaTable after PromoteLongVars\n");
                _compiler.lvaTableDump();
            }
#endif
        }

        private void TryPromoteLongVar(int lclNum)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
            assert(varDsc.Type is TYP_LONG);
            if (varDsc.lvDoNotEnregister || (varDsc.lvRefCnt() == 0) || varDsc.lvIsStructField ||
                _compiler.fgNoStructPromotion || (_compiler.fgNoStructParamPromotion && varDsc.lvIsParam))
            {
                return;
            }

#if FEATURE_HW_INTRINSICS && TARGET_X86
            if (varDsc.lvIsParam)
            {
                // Promotion blocks combined SIMD reads of long parameters.
                return;
            }
#endif
            varDsc.lvFieldCnt = 2;
            varDsc.lvFieldLclStart = _compiler.lvaCount;
            varDsc.lvPromoted = true;
            varDsc.lvContainsHoles = false;
            var isParam = varDsc.lvIsParam;
            JITDUMP($"\nPromoting long local V{lclNum:D2}:");

            for (var index = 0; index < 2; index++)
            {
                var fieldLclNum = _compiler.lvaGrabTemp(false,
                    $"field V{lclNum:D2}.{(index == 0 ? "lo" : "hi")} (fldOffset=0x{index * 4:x})");
                varDsc = ref _compiler.lvaGetDesc(lclNum);

                ref var fieldVarDsc = ref _compiler.lvaGetDesc(fieldLclNum);
                fieldVarDsc.Type = TYP_INT;
                fieldVarDsc.lvIsStructField = true;
                fieldVarDsc.lvFldOffset = (byte)(index * TYP_INT.Size);
                fieldVarDsc.lvFldOrdinal = (byte)index;
                fieldVarDsc.lvParentLcl = lclNum;
                if (isParam)
                {
                    fieldVarDsc.lvIsParam = true;
                    _compiler.lvaSetVarDoNotEnregister(fieldLclNum, DoNotEnregisterReason.LongParamField);
                    fieldVarDsc.lvIsRegArg = varDsc.lvIsRegArg;
                }
            }

#if TARGET_ARM
            if (varDsc.lvIsParam)
            {
                _compiler.lvaSetVarDoNotEnregister(lclNum, DoNotEnregisterReason.IsStructArg);
            }
#endif
        }
    }
}
#endif
