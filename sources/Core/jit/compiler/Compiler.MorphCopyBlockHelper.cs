// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Numerics;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree fgMorphCopyBlock(GenTree tree) => MorphCopyBlockHelper.MorphCopyBlock(this, tree);

    private sealed class MorphCopyBlockHelper : MorphInitBlockHelper
    {
        private int _srcLclNum = BAD_VAR_NUM;
        private GenTreeLclVarCommon? _srcLclNode;
        private int _srcLclOffset;
        private bool _srcSingleStoreLclVar;
        private bool _dstDoFldStore;
        private bool _srcDoFldStore;

        private ref LclVarDsc SrcVarDsc => ref _compiler.lvaGetDesc(_srcLclNum);

        public static GenTree MorphCopyBlock(Compiler compiler, GenTree tree)
        {
            var helper = new MorphCopyBlockHelper(compiler, tree);
            return helper.Morph();
        }

        private MorphCopyBlockHelper(Compiler compiler, GenTree store)
            : base(compiler, store, initBlock: false)
        {
        }

        protected override string GetHelperName() => "MorphCopyBlock";

        protected override void PrepareSrc()
        {
            _src = _store.Data;
            if (_src.Oper.IsLocal)
            {
                _srcLclNode = _src.AsLclVarCommon();
                _srcLclOffset = _srcLclNode.LclOffs;
                _srcLclNum = _srcLclNode.LclNum;
            }

            assert(_store.Type == _src.Type);
            if (_store.Type is TYP_STRUCT)
            {
                assert(_blockLayout is not null);
                assert(_blockLayout.CanAssignFrom(_src.GetLayout(_compiler)));
            }
        }

        protected override void TrySpecialCases()
        {
            if (_src.IsMultiRegNode)
            {
                assert(_store.Oper is GT_STORE_LCL_VAR);
                DstVarDsc.IsMultiRegDest = true;
                JITDUMP("Not morphing a multireg node return\n");
                _transformationDecision = BlockTransformation.SkipMultiRegSrc;
                _result = _store;
            }
            else if ((_src.Oper is GT_CALL) && (_store.Oper is GT_STORE_LCL_VAR)
                && DstVarDsc.CanBeReplacedWithItsField(_compiler))
            {
                JITDUMP("Not morphing a single reg call return\n");
                _transformationDecision = BlockTransformation.SkipSingleRegCallSrc;
                _result = _store;
            }
        }

        protected override void MorphStructCases()
        {
            JITDUMP("block store to morph:\n");
            DISPTREE(_store);
            if ((_dstLclNum != BAD_VAR_NUM) && DstVarDsc.lvPromoted)
            {
                noway_assert(varTypeIsStruct(DstVarDsc.Type));
                noway_assert(!_compiler.opts.MinOpts);
                if (_blockSize == DstVarDsc.lvExactSize)
                {
                    JITDUMP(" (m_dstDoFldStore=true)");
                    _dstDoFldStore = true;
                }
                else
                {
                    JITDUMP(" with mismatched dest size");
                }
            }
            if ((_srcLclNum != BAD_VAR_NUM) && SrcVarDsc.lvPromoted)
            {
                noway_assert(varTypeIsStruct(SrcVarDsc.Type));
                noway_assert(!_compiler.opts.MinOpts);
                if (_blockSize == SrcVarDsc.lvExactSize)
                {
                    JITDUMP(" (m_srcDoFldStore=true)");
                    _srcDoFldStore = true;
                }
                else
                {
                    JITDUMP(" with mismatched src size");
                }
            }

            // Removing an SSA definition would leave its downstream uses dangling.
            if ((_dstLclNum != BAD_VAR_NUM) && (_srcLclNum == _dstLclNum)
                && (_dstLclOffset == _srcLclOffset) && !_store.AsLclVarCommon().HasSsaIdentity)
            {
                JITDUMP("Self-copy; replaced with a NOP.\n");
                _transformationDecision = BlockTransformation.Nop;
                _result = _compiler.gtNewNothingNode();
                return;
            }

            // Avoid making enregisterable, register-sized structs into field accesses.
            var requiresCopyBlock = ((_store.Oper is GT_STORE_LCL_VAR) && DstVarDsc.lvRegStruct)
                || ((_src.Oper is GT_LCL_VAR) && SrcVarDsc.lvRegStruct);
#if TARGET_ARM
            if (_store.Oper.IsIndir && _store.AsIndir().IsUnaligned)
            {
                JITDUMP(" store is unaligned");
                requiresCopyBlock = true;
            }
            if (_src.Oper.IsIndir && _src.AsIndir().IsUnaligned)
            {
                JITDUMP(" src is unaligned");
                requiresCopyBlock = true;
            }
#endif
            // Lowering handles call results without spilling them just to access fields.
            if ((_srcLclNum == BAD_VAR_NUM) && !_src.Oper.IsIndir)
            {
                JITDUMP(" src is not an L-value");
                requiresCopyBlock = true;
            }

            if (!requiresCopyBlock)
            {
                // Prefer fields that can be enregistered, single-field copies, and
                // GC-field stores that would otherwise need a block-copy helper.
                var dstProfitable = (_dstLclNum != BAD_VAR_NUM)
                    && (!DstVarDsc.lvDoNotEnregister || (DstVarDsc.lvFieldCnt == 1));
                var srcProfitable = (_srcLclNum != BAD_VAR_NUM)
                    && (!SrcVarDsc.lvDoNotEnregister || (SrcVarDsc.HasGCPtr && (_dstLclNum == BAD_VAR_NUM))
                        || (SrcVarDsc.lvFieldCnt == 1));
                if (_dstDoFldStore && _srcDoFldStore && (dstProfitable || srcProfitable))
                {
                    var mismatchedTypes = false;
                    if (DstVarDsc.Layout != SrcVarDsc.Layout)
                    {
                        if (DstVarDsc.lvFieldCnt != SrcVarDsc.lvFieldCnt)
                        {
                            mismatchedTypes = true;
                        }
                        else
                        {
                            for (var i = 0; i < DstVarDsc.lvFieldCnt; i++)
                            {
                                ref var destinationField = ref _compiler.lvaGetDesc(DstVarDsc.lvFieldLclStart + i);
                                ref var sourceField = ref _compiler.lvaGetDesc(SrcVarDsc.lvFieldLclStart + i);
                                if ((destinationField.Type != sourceField.Type)
                                    || (destinationField.lvFldOffset != sourceField.lvFldOffset))
                                {
                                    mismatchedTypes = true;
                                    break;
                                }
                            }
                        }
                        if (mismatchedTypes)
                        {
                            requiresCopyBlock = true;
                            JITDUMP(" with mismatched types");
                        }
                    }
                }
                else if (_dstDoFldStore && dstProfitable)
                {
                    if ((DstVarDsc.lvFieldCnt == 1) && (_srcLclNum != BAD_VAR_NUM)
                        && (_blockSize == SrcVarDsc.Type.Size))
                    {
                        var destinationType = _compiler.lvaGetDesc(DstVarDsc.lvFieldLclStart).Type;
                        // Equal widths alone do not permit, for example, float = int.
                        if (SrcVarDsc.Type == destinationType)
                        {
                            _srcSingleStoreLclVar = true;
                        }
                    }
                }
                else if (_srcDoFldStore && srcProfitable)
                {
                    if ((SrcVarDsc.lvFieldCnt == 1) && (_dstLclNum != BAD_VAR_NUM)
                        && (_blockSize == DstVarDsc.Type.Size))
                    {
                        var sourceType = _compiler.lvaGetDesc(SrcVarDsc.lvFieldLclStart).Type;
                        if (DstVarDsc.Type == sourceType)
                        {
                            _dstSingleStoreLclVar = true;
                        }
                    }
                }
                else
                {
                    assert(!(_dstDoFldStore && dstProfitable) && !(_srcDoFldStore && srcProfitable));
                    requiresCopyBlock = true;
                    JITDUMP(" with no promoted structs");
                }
            }

            if (requiresCopyBlock)
            {
                _dstDoFldStore = false;
                _srcDoFldStore = false;
            }
            JITDUMP(requiresCopyBlock ? " this requires a CopyBlock.\n" : " using field by field stores.\n");

            if (requiresCopyBlock)
            {
                TryPrimitiveCopy();
                if (_transformationDecision is BlockTransformation.Undefined)
                {
                    _result = _store;
                    _transformationDecision = BlockTransformation.StructBlock;
                }
            }
            else
            {
                _result = CopyFieldByField();
                _transformationDecision = BlockTransformation.FieldByField;
            }

            if (!_dstDoFldStore && (_dstLclNum != BAD_VAR_NUM) && !_dstSingleStoreLclVar)
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
            if (!_srcDoFldStore && (_srcLclNum != BAD_VAR_NUM) && !_srcSingleStoreLclVar)
            {
                if (_src.Oper is GT_LCL_FLD)
                {
                    _compiler.lvaSetVarDoNotEnregister(_srcLclNum, DoNotEnregisterReason.LocalField);
                }
                else if (SrcVarDsc.lvPromoted)
                {
                    _compiler.lvaSetVarDoNotEnregister(_srcLclNum, DoNotEnregisterReason.BlockOp);
                }
            }
        }

        private void TryPrimitiveCopy()
        {
            if (_store.Type is not TYP_STRUCT)
            {
                return;
            }
            if (_compiler.opts.OptimizationDisabled && (_blockSize >= TYP_INT.Size))
            {
                return;
            }

            var storeType = TYP_UNDEF;
            if (_store.Oper is GT_STORE_LCL_FLD)
            {
                if (_blockSize == DstVarDsc.Type.Size)
                {
                    storeType = DstVarDsc.Type;
                }
            }
            else if (!_store.Oper.IsIndir)
            {
                return;
            }

            if (_srcLclNum != BAD_VAR_NUM)
            {
                if ((storeType is TYP_UNDEF) && (_blockSize == SrcVarDsc.Type.Size))
                {
                    storeType = SrcVarDsc.Type;
                }
            }
            else if (!_src.Oper.IsIndir)
            {
                return;
            }
            if (storeType is TYP_UNDEF)
            {
                return;
            }

            GenTree RetypeNode(GenTree node, int localNumber, bool isUse)
            {
                var data = isUse ? null : node.Data;
                if (node.Oper.IsIndir)
                {
                    var indirection = isUse
                        ? new GenTreeIndir(GT_IND, storeType, node.AsIndir().Addr, null, node, NodeThreading.None)
                        : new GenTreeStoreInd(storeType, node.AsIndir().Addr, node.Data, node, NodeThreading.None);
                    indirection.Flags = node.Flags;
                    return indirection;
                }

                ref var descriptor = ref _compiler.lvaGetDesc(localNumber);
                var local = node.AsLclVarCommon();
                GenTreeLclVarCommon replacement;
                if (descriptor.Type == storeType)
                {
                    var type = descriptor.lvNormalizeOnLoad ? descriptor.Type : descriptor.Type.ActualType;
                    replacement = new GenTreeLclVar(isUse ? GT_LCL_VAR : GT_STORE_LCL_VAR, type,
                        localNumber, data, node, NodeThreading.None) {
                        Flags = node.Flags & ~GTF_VAR_USEASG,
                    };
                }
                else
                {
                    replacement = new GenTreeLclFld(isUse ? GT_LCL_FLD : GT_STORE_LCL_FLD, storeType,
                        localNumber, local.LclOffs, data, layout: null, node, NodeThreading.None) {
                        Flags = node.Flags,
                    };
                }
                replacement.CopySsaIdentityFrom(local);
                return replacement;
            }

            _store = RetypeNode(_store, _dstLclNum, isUse: false);
            _src = RetypeNode(_src, _srcLclNum, isUse: true);
            _store.DataRef = _src;
            if (_dstLclNum != BAD_VAR_NUM)
            {
                _dstLclNode = _store.AsLclVarCommon();
            }
            if (_srcLclNum != BAD_VAR_NUM)
            {
                _srcLclNode = _src.AsLclVarCommon();
            }
            _result = _store;
            _transformationDecision = BlockTransformation.OneStoreBlock;
        }

        private GenTree CopyFieldByField()
        {
            GenTree? result = null;
            GenTree? address = null;
            target_ssize_t addressBaseOffset = 0;
            FieldSeq? addressBaseFields = null;
            GenTree? addressSpill = null;
            var addressSpillTemp = BAD_VAR_NUM;
            GenTree? addressSpillStore = null;
            var fieldCount = 0;
            var dyingFieldCount = 0;
            if (_dstDoFldStore)
            {
                fieldCount = DstVarDsc.lvFieldCnt;
                if (_compiler.fgGlobalMorph)
                {
                    assert(_dstLclNode is not null);
                    dyingFieldCount = BitOperations.PopCount((uint)(_dstLclNode.Flags & DstVarDsc.AllFieldDeathFlags));
                    assert(dyingFieldCount <= fieldCount);
                }
            }

            if (_dstDoFldStore && _srcDoFldStore)
            {
                assert((_dstLclNum != BAD_VAR_NUM) && (_srcLclNum != BAD_VAR_NUM));
                assert(DstVarDsc.lvFieldCnt == SrcVarDsc.lvFieldCnt);
            }
            else if (_dstDoFldStore)
            {
                if (_srcLclNum == BAD_VAR_NUM)
                {
                    address = _src.AsIndir().Addr;
                    _compiler.gtPeelOffsets(ref address, out addressBaseOffset, out addressBaseFields);
                    if ((fieldCount - dyingFieldCount) > 1)
                    {
                        if (CanReuseAddressForDecomposedStore(address))
                        {
                            noway_assert((address.Flags & GTF_PERSISTENT_SIDE_EFFECTS) == 0);
                        }
                        else
                        {
                            addressSpill = address;
                        }
                    }
                }
            }
            else
            {
                assert(_srcDoFldStore);
                fieldCount = SrcVarDsc.lvFieldCnt;
                _dstLclNode?.Flags &= ~(GTF_VAR_DEF | GTF_VAR_USEASG);
                if (_dstLclNum == BAD_VAR_NUM)
                {
                    address = _store.AsIndir().Addr;
                    _compiler.gtPeelOffsets(ref address, out addressBaseOffset, out addressBaseFields);
                    if (SrcVarDsc.lvFieldCnt > 1)
                    {
                        if (CanReuseAddressForDecomposedStore(address))
                        {
                            noway_assert((address.Flags & GTF_PERSISTENT_SIDE_EFFECTS) == 0);
                        }
                        else
                        {
                            addressSpill = address;
                        }
                    }
                }
            }

            if (addressSpill is not null)
            {
                // At most one address is spilled. Descriptor access below must
                // re-index lvaTable because allocating this temp can grow it.
                addressSpillTemp = _compiler.lvaGrabTemp(shortLifetime: true, "BlockOp address local");
                addressSpillStore = _compiler.gtNewTempStore(addressSpillTemp, addressSpill);
                addressSpillStore.SetMorphed(_compiler);
            }

            GenTree PostOrderAssertionProp(GenTree tree)
            {
                if (_compiler.fgGlobalMorph && _compiler.optLocalAssertionProp && (_compiler.optAssertionCount > 0))
                {
                    var optimized = tree;
                    var again = JitConfig.JitEnablePostorderLocalAssertionProp > 0;
                    var didOptimize = false;
                    while (again)
                    {
                        assert(optimized is not null);
                        tree = optimized;
                        optimized = _compiler.optLocalAssertionPropTree(_compiler.apLocalPostorder, tree);
                        again = optimized is not null;
                        didOptimize |= again;
                    }
                    if (didOptimize)
                    {
                        _compiler.gtUpdateNodeSideEffects(tree);
                    }
                }
                return tree;
            }

            static GenTreeFlags GetCopyIndirFlags(var_types type)
            {
                // Non-GC pieces of a whole struct copy need not be atomic.
                return varTypeIsGC(type) ? GTF_EMPTY : GTF_IND_ALLOW_NON_ATOMIC;
            }

            GenTree GrabAddress(int offset)
            {
                GenTree clone;
                if (addressSpill is not null)
                {
                    assert(addressSpillTemp != BAD_VAR_NUM);
                    clone = _compiler.gtNewLclvNode(addressSpill.Type, addressSpillTemp);
                    clone.SetMorphed(_compiler);
                }
                else
                {
                    assert(address is not null);
                    if (result is null)
                    {
                        clone = address;
                    }
                    else
                    {
                        noway_assert((address.Flags & GTF_PERSISTENT_SIDE_EFFECTS) == 0);
                        clone = _compiler.gtCloneExpr(address);
                        JITDUMP("Multiple Fields Clone created:\n");
                        DISPTREE(clone);
                        clone = _compiler.fgMorphTree(clone);
                    }
                }

                var fullOffset = unchecked(addressBaseOffset + offset);
                if ((fullOffset != 0) || (addressBaseFields is not null))
                {
                    var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, (nint)fullOffset);
                    offsetNode.SetMorphed(_compiler);
                    offsetNode.FieldSeq = addressBaseFields;
                    clone = _compiler.gtNewBinaryNode(GT_ADD, varTypeIsGC(clone.Type) ? TYP_BYREF : TYP_I_IMPL, clone, offsetNode);
                    // Do not propagate a large constant address into every field.
                    clone.Flags |= GTF_DONT_CSE;
                    clone.SetMorphed(_compiler);
                }
                return clone;
            }

            if (dyingFieldCount == fieldCount)
            {
                JITDUMP("All fields of destination of field-by-field copy are dying, skipping entirely\n");
                if (_srcLclNum != BAD_VAR_NUM)
                {
                    return _compiler.gtNewNothingNode();
                }
                JITDUMP("  ...but keeping a nullcheck\n");
                return _compiler.gtNewIndir(TYP_BYTE, GrabAddress(0));
            }

            for (var i = 0; i < fieldCount; i++)
            {
                if (_dstDoFldStore && _compiler.fgGlobalMorph)
                {
                    assert(_dstLclNode is not null);
                    if (_dstLclNode.IsLastUse(i))
                    {
                        JITDUMP($"Field-by-field copy skipping write to dead field V{DstVarDsc.lvFieldLclStart + i:D2}\n");
                        _compiler.fgKillDependentAssertionsSingle(_dstLclNum, _store);
                        continue;
                    }
                }

                GenTree? sourceField = null;
                if (_srcDoFldStore)
                {
                    noway_assert((_srcLclNum != BAD_VAR_NUM) && (_srcLclNode is not null));
                    var fieldLocal = SrcVarDsc.lvFieldLclStart + i;
                    sourceField = _compiler.gtNewLclvNode(_compiler.lvaGetDesc(fieldLocal).Type, fieldLocal);
                }
                else
                {
                    noway_assert(_dstDoFldStore && (_dstLclNum != BAD_VAR_NUM));
                    var destinationLocal = DstVarDsc.lvFieldLclStart + i;
                    if (_srcSingleStoreLclVar)
                    {
                        noway_assert((fieldCount == 1) && (_srcLclNum != BAD_VAR_NUM) && (addressSpill is null));
                        sourceField = _compiler.gtNewLclvNode(SrcVarDsc.Type, _srcLclNum);
                    }
                    else
                    {
                        var offset = _compiler.lvaGetDesc(destinationLocal).lvFldOffset;
                        var type = _compiler.lvaGetDesc(destinationLocal).Type;
                        var done = false;
                        if ((offset == 0) && (_srcLclNum != BAD_VAR_NUM))
                        {
                            // A full-width use through a different type remains a field.
                            noway_assert(_srcLclNode is not null);
                            assert(type is not TYP_STRUCT);
                            if (type.Size == SrcVarDsc.lvExactSize)
                            {
                                var replacement = new GenTreeLclFld(GT_LCL_FLD, type, _srcLclNum, _srcLclNode.LclOffs,
                                    data: null, layout: null, _srcLclNode, NodeThreading.None) {
                                    Flags = _srcLclNode.Flags & GTF_COMMON_MASK,
                                };
                                replacement.CopySsaIdentityFrom(_srcLclNode);
                                _srcLclNode = replacement;
                                _src = replacement;
                                _store.DataRef = replacement;
                                _compiler.lvaSetVarDoNotEnregister(_srcLclNum, DoNotEnregisterReason.LocalField);
                                sourceField = replacement;
                                done = true;
                            }
                        }
                        if (!done)
                        {
                            if (_srcLclNum != BAD_VAR_NUM)
                            {
                                sourceField = _compiler.gtNewLclFldNode(type, _srcLclNum, checked((ushort)(_srcLclOffset + offset)));
                                _compiler.lvaSetVarDoNotEnregister(_srcLclNum, DoNotEnregisterReason.LocalField);
                            }
                            else
                            {
                                sourceField = _compiler.gtNewIndir(type, GrabAddress(offset), GetCopyIndirFlags(type));
                            }
                        }
                    }
                }
                assert(sourceField is not null);
                sourceField = PostOrderAssertionProp(sourceField);
                sourceField.SetMorphed(_compiler);

                GenTree destinationStore;
                if (_dstDoFldStore)
                {
                    noway_assert(_dstLclNum != BAD_VAR_NUM);
                    destinationStore = _compiler.gtNewStoreLclVarNode(DstVarDsc.lvFieldLclStart + i, sourceField);
                }
                else
                {
                    noway_assert(_srcDoFldStore);
                    if (_dstSingleStoreLclVar)
                    {
                        noway_assert((fieldCount == 1) && (_dstLclNum != BAD_VAR_NUM) && (addressSpill is null));
                        destinationStore = _compiler.gtNewStoreLclVarNode(_dstLclNum, sourceField);
                    }
                    else
                    {
                        var sourceLocal = SrcVarDsc.lvFieldLclStart + i;
                        var offset = _compiler.lvaGetDesc(sourceLocal).lvFldOffset;
                        var type = _compiler.lvaGetDesc(sourceLocal).Type;
                        if (_dstLclNum != BAD_VAR_NUM)
                        {
                            destinationStore = _compiler.gtNewStoreLclFldNode(type, _dstLclNum,
                                checked((ushort)(_dstLclOffset + offset)), sourceField);
                            _compiler.lvaSetVarDoNotEnregister(_dstLclNum, DoNotEnregisterReason.LocalField);
                        }
                        else
                        {
                            var fieldAddress = GrabAddress(offset);
                            var flags = GetCopyIndirFlags(type);
                            if (_store.Oper is GT_STORE_BLK or GT_STOREIND)
                            {
                                flags |= _store.Flags & (GTF_IND_TGT_NOT_HEAP | GTF_IND_TGT_HEAP);
                                if ((_store.Oper is GT_STORE_BLK) && _store.AsBlk().Layout.IsStackOnly(_compiler))
                                {
                                    flags |= GTF_IND_TGT_NOT_HEAP;
                                }
                            }
                            destinationStore = _compiler.gtNewStoreIndNode(type, fieldAddress, sourceField, flags);
                        }
                    }
                }

                if (destinationStore.Type != sourceField.Type)
                {
                    noway_assert(destinationStore.Type.ActualType == sourceField.Type.ActualType);
                    assert(sourceField.Oper.IsIntegralConst);
                }
                destinationStore = PostOrderAssertionProp(destinationStore);
                destinationStore.SetMorphed(_compiler);
                if (_compiler.optLocalAssertionProp)
                {
                    _compiler.fgAssertionGen(destinationStore);
                }
                if (addressSpillStore is not null)
                {
                    result = _compiler.gtNewCommaNode(TYP_VOID, addressSpillStore, destinationStore);
                    addressSpillStore = null;
                    result.SetMorphed(_compiler);
                }
                else if (result is not null)
                {
                    result = _compiler.gtNewCommaNode(TYP_VOID, result, destinationStore);
                    result.SetMorphed(_compiler);
                }
                else
                {
                    result = destinationStore;
                }
            }

            assert(result is not null);
            return result;
        }

        private bool CanReuseAddressForDecomposedStore(GenTree address)
        {
            if (address.Oper.IsLocalRead)
            {
                var local = address.AsLclVarCommon().LclNum;
                ref var descriptor = ref _compiler.lvaGetDesc(local);
                if (descriptor.IsAddressExposed)
                {
                    // The address may point to itself.
                    return false;
                }
                if (_dstLclNum == BAD_VAR_NUM)
                {
                    return true;
                }

                // Copying the first field must not overwrite the address used
                // to load subsequent fields.
                return (local != _dstLclNum) && (!descriptor.lvIsStructField || (descriptor.lvParentLcl != _dstLclNum));
            }
            return address.Oper.IsInvariant;
        }
    }
}
