// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private struct WasmLocalCopy
    {
        public GenTreeLclVar Store;
        public int SourceLclNum;
        public GenTree? SourceLastUse;
    }

    private struct WasmRematerializationCandidate
    {
        public BasicBlock Block;
        public GenTreeLclVar Store;
    }

    private struct WasmLocalCopyDefinitionVisitor(ArrayStack<WasmLocalCopy> pending) : ILocalDefVisitor
    {
        public readonly GenTree.VisitResult Visit<TDef>(TDef def)
            where TDef : struct, ILocalDef
        {
            for (var index = pending.Height() - 1; index >= 0; index--)
            {
                if (pending.Bottom(index).SourceLclNum == def.LclNum)
                {
                    RemovePendingWasmLocalCopy(pending, index);
                }
            }

            return GenTree.VisitResult.Continue;
        }
    }

    private ArrayStack<BasicBlock>? _wasmLocalCopyBlocks;
    private ArrayStack<BasicBlock>? _wasmRematBlocks;

    internal void RecordWasmLocalCopyBlock(BasicBlock block)
    {
        var blocks = _wasmLocalCopyBlocks ??= new ArrayStack<BasicBlock>();
        if ((blocks.Height() == 0) || !ReferenceEquals(blocks.Top(), block))
        {
            blocks.Push(block);
        }
    }

    internal void RecordWasmRematBlock(BasicBlock block)
    {
        var blocks = _wasmRematBlocks ??= new ArrayStack<BasicBlock>();
        if ((blocks.Height() == 0) || !ReferenceEquals(blocks.Top(), block))
        {
            blocks.Push(block);
        }
    }

    internal static bool fgWasmIsRematerializableValue(GenTree? node)
    {
        if (node is null)
        {
            return false;
        }

        if (node.Oper.IsConst)
        {
            return true;
        }

        const GenTreeFlags invariantLoad = GTF_IND_INVARIANT | GTF_IND_NONFAULTING;
        const GenTreeFlags unmovable = GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF | GTF_IND_VOLATILE;
        return (node.Oper is GT_IND) && ((node.Flags & invariantLoad) == invariantLoad) &&
            ((node.Flags & unmovable) == 0) && node.AsIndir().Addr.IsInvariant;
    }

    public PhaseStatus fgWasmForwardLocalCopies()
    {
        assert(Configuration.TARGET_WASM);

        if (!opts.OptimizationEnabled || !backendRequiresLocalVarLifetimes())
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var madeChanges = fgWasmRematerializeInvariantLocals();
        var blocks = _wasmLocalCopyBlocks;
        if (blocks is null)
        {
            return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
        }

        var pending = new ArrayStack<WasmLocalCopy>();
        static bool IsCopyableType(var_types type)
        {
            return varTypeIsEnregisterable(type) && !varTypeIsStruct(type) &&
                !varTypeIsGC(type) && !varTypeIsSmall(type);
        }

        bool HasHiddenDefs(int lclNum)
        {
            ref var variable = ref lvaGetDesc(lclNum);
            var parentLcl = variable.lvParentLcl;
            if (variable.lvIsStructField && lvaGetDesc(parentLcl).IsAddressExposed)
            {
                return true;
            }

            return variable.lvImplicitlyReferenced || variable.IsAddressExposed || variable.lvPromoted;
        }

        foreach (var block in blocks.BottomUpOrder())
        {
            if (block.HasFlag(BBF_REMOVED))
            {
                continue;
            }

            JITDUMP($"Forwarding local copies in {FMT_BB(block.bbNum)}\n");
            pending.Reset();
            LIR.Range range = block;
            foreach (var node in range)
            {
                if (pending.Height() != 0)
                {
                    if ((node.Oper is GT_LCL_VAR) && !node.IsContained)
                    {
                        var lclNum = node.AsLclVarCommon().LclNum;
                        for (var index = 0; index < pending.Height(); index++)
                        {
                            ref var copy = ref pending.Data()[index];
                            if ((copy.SourceLclNum == lclNum) && node.HasLastUse)
                            {
                                copy.SourceLastUse = node;
                                continue;
                            }

                            if ((copy.Store.LclNum != lclNum) || (node.Type != copy.Store.Type))
                            {
                                continue;
                            }

                            var store = copy.Store;
                            var forwardedSourceLclNum = copy.SourceLclNum;
                            node.Flags &= ~GTF_VAR_DEATH;
                            if (copy.SourceLastUse is not null)
                            {
                                copy.SourceLastUse.Flags &= ~GTF_VAR_DEATH;
                                node.Flags |= GTF_VAR_DEATH;
                            }

                            range.Remove(store.Data!);
                            range.Remove(store);
                            node.AsLclVar().LclNum = forwardedSourceLclNum;
                            node.AsLclVar().SsaNum = SsaConfig.RESERVED_SSA_NUM;

                            ref var targetDsc = ref lvaGetDesc(lclNum);
                            targetDsc.setLvRefCnt(0);
                            targetDsc.setLvRefCntWtd(0);
                            RemovePendingWasmLocalCopy(pending, index);

                            if (node.HasLastUse)
                            {
                                for (var other = 0; other < pending.Height(); other++)
                                {
                                    if (pending.Bottom(other).SourceLclNum == forwardedSourceLclNum)
                                    {
                                        pending.Data()[other].SourceLastUse = node;
                                    }
                                }
                            }

                            JITDUMP($"Forwarding V{forwardedSourceLclNum:D2} into [{node.TreeId:D6}] in place of " +
                                $"single-use copy V{lclNum:D2} [{store.TreeId:D6}]\n");
                            madeChanges = true;
                            break;
                        }
                    }
                    else if (node.Oper is GT_LCL_ADDR)
                    {
                        var lclNum = node.AsLclVarCommon().LclNum;
                        for (var index = pending.Height() - 1; index >= 0; index--)
                        {
                            if (pending.Bottom(index).SourceLclNum == lclNum)
                            {
                                RemovePendingWasmLocalCopy(pending, index);
                            }
                        }
                    }

                    var visitor = new WasmLocalCopyDefinitionVisitor(pending);
                    _ = node.VisitLogicalLocalDefs(this, ref visitor);
                }

                if ((node.Oper is not GT_STORE_LCL_VAR) || (pending.Height() >= 256))
                {
                    continue;
                }

                var storeNode = node.AsLclVar();
                var sourceNode = storeNode.Data;
                if ((sourceNode?.Oper is not GT_LCL_VAR) || sourceNode.IsContained ||
                    ((sourceNode._lirFlags & LIR.Flags.MultiplyUsed) != 0))
                {
                    continue;
                }

                var targetLclNum = storeNode.LclNum;
                var sourceLocalNode = sourceNode.AsLclVarCommon();
                var sourceLclNum = sourceLocalNode.LclNum;
                ref var target = ref lvaGetDesc(targetLclNum);
                ref var source = ref lvaGetDesc(sourceLclNum);
                if ((target.lvRefCnt() != 2) || target.lvImplicitlyReferenced ||
                    target.lvIsParam || target.lvIsStructField || target.lvIsOSRLocal ||
                    HasHiddenDefs(targetLclNum) || HasHiddenDefs(sourceLclNum) ||
                    !IsCopyableType(target.Type) || (source.Type != target.Type) ||
                    (storeNode.Type != target.Type) || (sourceLocalNode.Type != target.Type) ||
                    (targetLclNum == sourceLclNum))
                {
                    continue;
                }

                pending.Push(new WasmLocalCopy
                {
                    Store = storeNode,
                    SourceLclNum = sourceLclNum,
                    SourceLastUse = sourceLocalNode.HasLastUse ? sourceLocalNode : null,
                });
            }
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private bool fgWasmRematerializeInvariantLocals()
    {
        var rematBlocks = _wasmRematBlocks;
        if (rematBlocks is null)
        {
            return false;
        }

        var candidates = new Dictionary<int, WasmRematerializationCandidate>();
        var liveIntoEntries = VarSetOps.MakeCopy(this, fgFirstBB!.bbLiveIn);
        foreach (ref var clause in new EHClauses(this))
        {
            VarSetOps.UnionD(this, liveIntoEntries, clause.ebdHndBeg.bbLiveIn);
            if (clause.HasFilter)
            {
                VarSetOps.UnionD(this, liveIntoEntries, clause.ebdFilter.bbLiveIn);
            }
        }

        foreach (var block in rematBlocks.BottomUpOrder())
        {
            if (block.HasFlag(BBF_REMOVED))
            {
                continue;
            }

            LIR.Range range = block;
            foreach (var node in range)
            {
                if (node.Oper is not GT_STORE_LCL_VAR)
                {
                    continue;
                }

                var store = node.AsLclVar();
                var value = store.Data;
                if (!fgWasmIsRematerializableValue(value) ||
                    ((value!._lirFlags & LIR.Flags.MultiplyUsed) != 0))
                {
                    continue;
                }

                ref var descriptor = ref lvaGetDesc(store.LclNum);
                var type = descriptor.Type;
                if ((descriptor.lvRefCnt() != 2) || !descriptor.lvTracked || descriptor.lvIsParam ||
                    descriptor.lvIsStructField || descriptor.lvIsOSRLocal || descriptor.lvPromoted ||
                    descriptor.IsAddressExposed || descriptor.lvImplicitlyReferenced ||
                    !varTypeIsEnregisterable(type) || varTypeIsStruct(type) || varTypeIsGC(type) ||
                    varTypeIsSmall(type) || (store.Type != type) || (genActualType(value!) != type) ||
                    VarSetOps.IsMember(this, liveIntoEntries, descriptor._varIndex))
                {
                    continue;
                }

                candidates[store.LclNum] = new WasmRematerializationCandidate
                {
                    Block = block,
                    Store = store,
                };
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var removed = VarSetOps.MakeEmpty(this);
        var madeChanges = false;
        foreach (var block in Blocks)
        {
            if (candidates.Count == 0)
            {
                break;
            }

            LIR.Range range = block;
            for (var node = range.FirstNode; (node is not null) && (candidates.Count != 0);)
            {
                var next = node.Next;
                if ((node.Oper is GT_LCL_VAR) && !node.IsContained &&
                    candidates.TryGetValue(node.AsLclVarCommon().LclNum, out var candidate) &&
                    (block.bbWeight <= candidate.Block.bbWeight) && (node.Type == candidate.Store.Type))
                {
                    LIR.Range definitionRange = candidate.Block;
                    var value = candidate.Store.Data;
                    if (value is not null)
                    {
                        var valueRange = definitionRange.GetTreeRange(value, out var isClosed);
                        if (isClosed && range.TryGetUse(node, out var use))
                        {
                            var lclNum = node.AsLclVarCommon().LclNum;
                            JITDUMP(
                                $"Rematerializing [{value.TreeId:D6}] from {FMT_BB(candidate.Block.bbNum)} " +
                                $"at [{node.TreeId:D6}] in {FMT_BB(block.bbNum)} in place of single-use V{lclNum:D2}\n");

                            definitionRange.Remove(candidate.Store);
                            var movedValue = definitionRange.RemoveAndGetRange(valueRange);
                            range.InsertBefore(node, movedValue);
                            value._lirFlags |= node._lirFlags & LIR.Flags.MultiplyUsed;
                            use.ReplaceWith(value);
                            range.Remove(node);

                            ref var descriptor = ref lvaGetDesc(lclNum);
                            descriptor.setLvRefCnt(0);
                            descriptor.setLvRefCntWtd(0);
                            VarSetOps.AddElemD(this, removed, descriptor._varIndex);
                            _ = candidates.Remove(lclNum);
                            madeChanges = true;
                        }
                    }
                }

                node = next;
            }
        }

        if (madeChanges)
        {
            foreach (var block in Blocks)
            {
                VarSetOps.DiffD(this, block.bbLiveIn, removed);
                VarSetOps.DiffD(this, block.bbLiveOut, removed);
            }
        }

        return madeChanges;
    }

    private static void RemovePendingWasmLocalCopy(ArrayStack<WasmLocalCopy> pending, int index)
    {
        pending.Data()[index] = pending.Top();
        _ = pending.Pop();
    }

    private bool fgWasmMayEmitCall()
    {
        assert(compHndBBtabCount == 0);
        assert(fgUseThrowHelperBlocks());
        var codeGen = this.codeGen;
        assert(codeGen is not null);

        foreach (var block in Blocks)
        {
            foreach (var node in block)
            {
                switch (node.Oper)
                {
                    case GT_CALL:
                    case GT_LCLHEAP:
                    {
                        return true;
                    }

                    case GT_STOREIND:
                    {
                        if (codeGen!.GCInfo.gcIsWriteBarrierStoreIndNode(node.AsStoreInd()))
                        {
                            return true;
                        }
                        break;
                    }

                    case GT_STORE_BLK:
                    {
                        var kind = node.AsBlk()._kind;
                        if ((kind is not GenTreeBlk.BlkOpKindNativeOpcode) &&
                            (kind is not GenTreeBlk.BlkOpKindLoop))
                        {
                            return true;
                        }
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }
        }

        return false;
    }
}
