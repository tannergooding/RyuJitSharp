// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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

    internal void RecordWasmLocalCopyBlock(BasicBlock block)
    {
        var blocks = _wasmLocalCopyBlocks ??= new ArrayStack<BasicBlock>();
        if ((blocks.Height() == 0) || !ReferenceEquals(blocks.Top(), block))
        {
            blocks.Push(block);
        }
    }

    public PhaseStatus fgWasmForwardLocalCopies()
    {
        assert(Configuration.TARGET_WASM);

        var blocks = _wasmLocalCopyBlocks;
        if ((blocks is null) || !opts.OptimizationEnabled || !backendRequiresLocalVarLifetimes())
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var madeChanges = false;
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
