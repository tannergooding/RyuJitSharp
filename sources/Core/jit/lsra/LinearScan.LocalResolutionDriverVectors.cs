// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    private unsafe void insertUpperVectorSave(GenTree tree, RefPosition reference, Interval upper, BasicBlock block)
    {
#if DEBUG
        JITDUMP($"Inserting UpperVectorSave for RP #{reference.rpNum} before {tree.TreeId}.{tree.Oper.Name}:\n");
#endif
        var localInterval = upper.relatedInterval
            ?? throw new FatalJitException("An upper-vector save requires its local interval.");
        assert(localInterval.isLocalVar && ReferenceEquals(reference.getInterval(), upper));
        var localReg = localInterval.physReg;
        if (localReg == REG_NA)
        {
            return;
        }

#if DEBUG
        if (tree.Oper is GT_CALL)
        {
            assert(!tree.AsCall().IsNoReturn);
        }
#endif
        ref var local = ref _compiler.lvaGetDesc(checked((int)localInterval.varNum));
        assert(Compiler.varTypeNeedsPartialCalleeSave(local.GetRegisterType()));
        var spillReg = reference.assignedReg();
        assert(!reference.spillAfter);
        var source = _compiler.gtNewLclvNode(local.Type, checked((int)localInterval.varNum));
        source.RegNum = localReg;
#if DEBUG
        source._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
        var save = new GenTreeIntrinsic(TYP_SIMD16, source, NI_SIMD_UpperSave, methodHandle: null)
        {
            RegNum = spillReg,
#if FEATURE_READYTORUN
            EntryPoint = new CORINFO_CONST_LOOKUP { accessType = IAT_VALUE },
#endif
        };
#if DEBUG
        save._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
        if (spillReg == REG_NA)
        {
            save.Flags |= GTF_SPILL;
            upper.physReg = REG_NA;
        }
        else
        {
            assert((genSingleTypeRegMask(spillReg) & SRBM_FLT_CALLEE_SAVED) != SRBM_NONE);
            upper.physReg = spillReg;
        }

        block.InsertBefore(tree, LIR.SeqTree(_compiler, save));
        DISPTREE(save);
        JITDUMP("\n");
    }

    private unsafe void insertUpperVectorRestore(GenTree? tree, RefPosition reference, Interval upper, BasicBlock block)
    {
#if DEBUG
        JITDUMP($"Inserting UpperVectorRestore for RP #{reference.rpNum} ");
#endif
        var localInterval = upper.relatedInterval
            ?? throw new FatalJitException("An upper-vector restore requires its local interval.");
        assert(localInterval.isLocalVar && localInterval.physReg != REG_NA);
        ref var local = ref _compiler.lvaGetDesc(checked((int)localInterval.varNum));
        assert(Compiler.varTypeNeedsPartialCalleeSave(local.GetRegisterType()));

        var source = _compiler.gtNewLclvNode(local.Type, checked((int)localInterval.varNum));
        source.RegNum = localInterval.physReg;
#if DEBUG
        source._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
        var restore = new GenTreeIntrinsic(local.Type, source, NI_SIMD_UpperRestore, methodHandle: null);
#if FEATURE_READYTORUN
        restore.EntryPoint = new CORINFO_CONST_LOOKUP { accessType = IAT_VALUE };
#endif
#if DEBUG
        restore._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
        var restoreReg = upper.physReg;
        if (restoreReg == REG_NA)
        {
            assert(localInterval.isSpilled && reference.assignedReg() == REG_NA);
            restore.Flags |= GTF_NOREG_AT_USE;
        }
        restore.RegNum = restoreReg;

        if (tree is not null)
        {
            var useNode = tree;
            while (true)
            {
                if (!block.TryGetUse(useNode, out var use))
                {
                    throw new FatalJitException("An upper-vector restore requires an owning use.");
                }
                useNode = use.User();
                if (!useNode.IsContained)
                {
                    break;
                }
            }
#if DEBUG
            JITDUMP($"before {useNode.TreeId}.{useNode.Oper.Name}:\n");
#endif
            block.InsertBefore(useNode, LIR.SeqTree(_compiler, restore));
        }
        else
        {
            JITDUMP($"at end of {FMT_BB(block.bbNum)}:\n");
            if (block.Kind is BBJ_COND or BBJ_SWITCH)
            {
                var branch = block.LastNode
                    ?? throw new FatalJitException("A vector restore before a branch requires a terminator.");
                assert(branch.Oper.IsConditionalJump || branch.Oper is GT_SWITCH_TABLE or GT_SWITCH);
                block.InsertBefore(branch, LIR.SeqTree(_compiler, restore));
            }
            else
            {
                assert(block.Kind is BBJ_ALWAYS);
                block.InsertAtEnd(LIR.SeqTree(_compiler, restore));
            }
        }
        DISPTREE(restore);
        JITDUMP("\n");
    }
#endif
}
