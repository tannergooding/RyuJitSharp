// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    private class MorphInitBlockHelper
    {
        protected readonly Compiler _compiler;
        protected readonly bool _initBlock;
        protected GenTree _store;
        protected GenTree _src;
        protected uint _blockSize;
        protected ClassLayout? _blockLayout;
        protected int _dstLclNum = BAD_VAR_NUM;
        protected GenTreeLclVarCommon? _dstLclNode;
        protected int _dstLclOffset;
        protected bool _dstSingleStoreLclVar;
        protected BlockTransformation _transformationDecision;
        protected GenTree? _result;

        // Local-table growth can replace the descriptor array.
        protected ref LclVarDsc DstVarDsc => ref _compiler.lvaGetDesc(_dstLclNum);

        protected enum BlockTransformation
        {
            Undefined,
            FieldByField,
            OneStoreBlock,
            StructBlock,
            SkipMultiRegSrc,
            SkipSingleRegCallSrc,
            Nop,
        }

        public static GenTree MorphInitBlock(Compiler compiler, GenTree tree)
        {
            var helper = new MorphInitBlockHelper(compiler, tree, initBlock: true);
            return helper.Morph();
        }

        protected MorphInitBlockHelper(Compiler compiler, GenTree store, bool initBlock)
        {
            assert(store.Oper.IsStore);
            assert((initBlock == store.IsInitBlkOp) && (!initBlock == store.IsCopyBlkOp));
            _compiler = compiler;
            _initBlock = initBlock;
            _store = store;
            _src = store.Data;
        }

        protected virtual string GetHelperName() => "MorphInitBlock";

        protected GenTree Morph()
        {
            JITDUMP($"{GetHelperName()}:\n");
            var sideEffects = EliminateCommas(out var commaPool);

            PrepareDst();
            PrepareSrc();
            PropagateBlockAssertions();
            TrySpecialCases();

            if (_transformationDecision is BlockTransformation.Undefined)
            {
                MorphStructCases();
            }

            PropagateExpansionAssertions();
            assert(_transformationDecision is not BlockTransformation.Undefined);
            assert(_result is not null);
            _result.SetMorphed(_compiler);

            while (sideEffects is not null)
            {
                if (commaPool is not null)
                {
                    var comma = commaPool;
                    commaPool = commaPool.Next;
                    assert(comma.Oper is GT_COMMA);
                    comma.Type = TYP_VOID;
                    comma.AsOp().Op1 = sideEffects;
                    comma.AsOp().Op2 = _result;
                    comma.Flags = (sideEffects.Flags | _result.Flags) & GTF_ALL_EFFECT;
                    _result = comma;
                }
                else
                {
                    _result = _compiler.gtNewBinaryNode(GT_COMMA, TYP_VOID, sideEffects, _result);
                }

                _result.SetMorphed(_compiler);
                sideEffects = sideEffects.Next;
            }

            JITDUMP($"{GetHelperName()} (after):\n");
            DISPTREE(_result);
            return _result;
        }

        protected void PrepareDst()
        {
            if (_store.Oper.IsLocalStore)
            {
                _dstLclNode = _store.AsLclVarCommon();
                _dstLclOffset = _dstLclNode.LclOffs;
                _dstLclNum = _dstLclNode.LclNum;

                if (_compiler.optLocalAssertionProp && (_compiler.optAssertionCount > 0))
                {
                    _compiler.fgKillDependentAssertions(_dstLclNum, _store);
                }
            }
            else
            {
                assert(_store.Oper is GT_STOREIND or GT_STORE_BLK);
            }

            if (_store.Type is TYP_STRUCT)
            {
                _blockLayout = _store.GetLayout(_compiler);
                _blockSize = _blockLayout.Size;
            }
            else
            {
                _blockSize = (uint)_store.Type.Size;
            }

            assert(_blockSize != 0);

#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf($"PrepareDst for [{_store.TreeId:D6}] ");
                if (_dstLclNode is not null)
                {
                    jitprintf($"have found a local var V{_dstLclNum:D2}.\n");
                }
                else
                {
                    jitprintf("have not found a local var.\n");
                }
            }
#endif
        }

        protected void PropagateBlockAssertions()
        {
            // Generate assertions before expansion loses the original block operation.
            if (_compiler.optLocalAssertionProp)
            {
                _compiler.fgAssertionGen(_store);
            }
        }

        protected void PropagateExpansionAssertions()
        {
            if (_compiler.optLocalAssertionProp && (_transformationDecision is BlockTransformation.OneStoreBlock))
            {
                _compiler.fgAssertionGen(_store);
            }
        }

        protected virtual void PrepareSrc()
        {
            _src = _store.Data;
        }

        protected virtual void TrySpecialCases()
        {
        }

        protected virtual void MorphStructCases()
        {
            if ((_dstLclNum != BAD_VAR_NUM) && DstVarDsc.lvPromoted && !DstVarDsc.lvDoNotEnregister)
            {
                TryInitFieldByField();
            }

            if (_transformationDecision is BlockTransformation.Undefined)
            {
                TryPrimitiveInit();
            }

            if (_transformationDecision is BlockTransformation.Undefined)
            {
                _result = _store;
                _transformationDecision = BlockTransformation.StructBlock;

                if (_dstLclNum != BAD_VAR_NUM)
                {
                    if (_store.Oper is GT_STORE_LCL_FLD)
                    {
                        _compiler.lvaSetVarDoNotEnregister(_dstLclNum, DoNotEnregisterReason.LocalField);
                    }
                    else if (DstVarDsc.lvPromoted)
                    {
                        _compiler.lvaSetVarDoNotEnregister(_dstLclNum, DoNotEnregisterReason.BlockOp);
                    }
                }
            }
        }

        private void TryInitFieldByField()
        {
            assert((_dstLclNum != BAD_VAR_NUM) && DstVarDsc.lvPromoted);
            assert(_dstLclNode is not null);

            if (DstVarDsc.IsAddressExposed && DstVarDsc.lvContainsHoles)
            {
                JITDUMP(" dest is address exposed and contains holes.\n");
                return;
            }

            if (_dstLclOffset != 0)
            {
                JITDUMP(" dest not at a zero offset.\n");
                return;
            }

            if (DstVarDsc.lvExactSize != _blockSize)
            {
                JITDUMP(" dest size mismatch.\n");
                return;
            }

            var initVal = _src.Oper is GT_INIT_VAL ? _src.AsUnOp().Op1 : _src;

            if (initVal.Oper is not GT_CNS_INT)
            {
                JITDUMP(" source is not constant.\n");
                return;
            }

            var initPattern = (byte)(initVal.AsIntCon().IconValue & 0xFF);

            if (initPattern != 0)
            {
                for (var i = 0; i < DstVarDsc.lvFieldCnt; i++)
                {
                    ref var fieldDsc = ref _compiler.lvaGetDesc(DstVarDsc.lvFieldLclStart + i);
                    if (varTypeIsGC(fieldDsc.Type))
                    {
                        JITDUMP(" dest contains GC fields and source constant is not 0.\n");
                        return;
                    }
                }
            }

            JITDUMP(" using field by field initialization.\n");
            GenTree? tree = null;

            for (var i = 0; i < DstVarDsc.lvFieldCnt; i++)
            {
                var fieldLclNum = DstVarDsc.lvFieldLclStart + i;

                if (_compiler.fgGlobalMorph && _dstLclNode.IsLastUse(i))
                {
                    JITDUMP($"Field-by-field init skipping write to dead field V{fieldLclNum:D2}\n");
                    _compiler.fgKillDependentAssertionsSingle(_dstLclNum, _store);
                    continue;
                }

                var fieldType = _compiler.lvaGetDesc(fieldLclNum).Type;
                var src = _compiler.gtNewConWithPattern(fieldType, initPattern);
                src.SetMorphed(_compiler);
                var store = _compiler.gtNewTempStore(fieldLclNum, src);

                if (_compiler.optLocalAssertionProp)
                {
                    _compiler.fgAssertionGen(store);
                }

                store.SetMorphed(_compiler);
                if (tree is not null)
                {
                    tree = _compiler.gtNewBinaryNode(GT_COMMA, TYP_VOID, tree, store);
                    tree.SetMorphed(_compiler);
                }
                else
                {
                    tree = store;
                }
            }

            if (tree is null)
            {
                tree = _compiler.gtNewNothingNode();
                tree.SetMorphed(_compiler);
            }

            _result = tree;
            _transformationDecision = BlockTransformation.FieldByField;
        }

        private void TryPrimitiveInit()
        {
            if (_src.IsIntegralConst(0) && (_dstLclNum != BAD_VAR_NUM) && (DstVarDsc.Type.Size == _blockSize))
            {
                var lclType = DstVarDsc.Type;
                if (varTypeIsSimd(lclType))
                {
                    _src = _compiler.gtNewZeroConNode(lclType);
                    _src.SetMorphed(_compiler);
                }
                else
                {
                    _src = _src.BashToZeroConst(lclType);
                }

                var storeType = DstVarDsc.lvNormalizeOnLoad ? lclType : lclType.ActualType;
                _store = new GenTreeLclVar(GT_STORE_LCL_VAR, storeType, _dstLclNum, _src, _store, NodeThreading.None);
                _store.Flags |= GTF_VAR_DEF;
                _dstLclNode = _store.AsLclVarCommon();
                _result = _store;
                _transformationDecision = BlockTransformation.OneStoreBlock;
            }
        }

        private GenTree? EliminateCommas(out GenTree? commaPool)
        {
            GenTree? sideEffects = null;
            GenTree? commas = null;

            void AddSideEffect(GenTree sideEffect)
            {
                sideEffect.Next = sideEffects;
                sideEffects = sideEffect;
            }

            void AddComma(GenTree comma)
            {
                AddSideEffect(comma.AsOp().Op1);
                comma.Next = commas;
                commas = comma;
            }

            var src = _store.Data;

            if (!_store.IsReverseOp && _store.Oper.IsIndir && (src.Oper is GT_COMMA))
            {
                var address = _store.AsIndir().Addr;
                if (((address.Flags & GTF_ALL_EFFECT) != 0) ||
                    (((src.Flags & GTF_ASG) != 0) && !address.Oper.IsInvariant))
                {
                    var addressLclNum = _compiler.lvaGrabTemp(shortLifetime: true, "Block morph LHS addr");
                    var tempStore = _compiler.gtNewTempStore(addressLclNum, address);
                    tempStore.SetMorphed(_compiler);
                    AddSideEffect(tempStore);
                    var tempRead = _compiler.gtNewLclvNode(address.Type.ActualType, addressLclNum);
                    tempRead.SetMorphed(_compiler);
                    _store.AsUnOp().Op1 = tempRead;
                    _compiler.gtUpdateNodeSideEffects(_store);
                }
            }

            while (src.Oper is GT_COMMA)
            {
                AddComma(src);
                src = src.AsOp().Op2;
            }

            if (sideEffects is not null)
            {
                _store.DataRef = src;
                _compiler.gtUpdateNodeSideEffects(_store);
            }

            commaPool = commas;
            return sideEffects;
        }
    }
}
