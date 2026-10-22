// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private unsafe void insertUpperVectorSave(GenTree tree, RefPosition reference, Interval upper, BasicBlock block)
    {
        if (!Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED    );
            return;
        }

        if (Configuration.DEBUG)
        {
            JITDUMP($"Inserting UpperVectorSave for RP #{reference.rpNum} before {tree.TreeId}.{tree.Oper.Name}:\n");
        }
        var localInterval = upper.relatedInterval;
        if (localInterval is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.UpperVectorSaveRequiresItsLocalInterval
    );
            return;
        }
        assert(localInterval.isLocalVar && ReferenceEquals(reference.getInterval(), upper));
        var localReg = localInterval.physReg;
        if (localReg == REG_NA)
        {
            return;
        }

        if (Configuration.DEBUG && tree.Oper is GT_CALL)
        {
            assert(!tree.AsCall().IsNoReturn);
        }
        ref var local = ref _compiler.lvaGetDesc(checked((int)localInterval.varNum));
        assert(LsraConfiguration.VarTypeNeedsPartialCalleeSave(local.GetRegisterType()));
        var spillReg = reference.assignedReg();
        var spillToMemory = Configuration.TARGET_ARM64 ? reference.spillAfter : spillReg == REG_NA;
        assert(Configuration.TARGET_ARM64 ? spillReg != REG_NA : !reference.spillAfter);
        var saveType = Configuration.TARGET_ARM64 ? TYP_DOUBLE : TYP_SIMD16;
        var source = _compiler.gtNewLclvNode(local.Type, checked((int)localInterval.varNum));
        source.RegNum = localReg;
        if (Configuration.DEBUG)
        {
            source._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
        }
        var save = new GenTreeIntrinsic(saveType, source, NI_SIMD_UpperSave, methodHandle: null) {
            RegNum = spillReg,
        };
        if (Configuration.FEATURE_READYTORUN)
        {
            save.EntryPoint = new CORINFO_CONST_LOOKUP { accessType = IAT_VALUE };
        }

        if (Configuration.DEBUG)
        {
            save._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
        }
        if (spillToMemory)
        {
            save.Flags |= GTF_SPILL;
            upper.physReg = REG_NA;
        }
        else
        {
            assert((genSingleTypeRegMask(spillReg) & TargetRuntimeMetadata.FloatingCalleeSavedMask) != SRBM_NONE);
            upper.physReg = spillReg;
        }

        block.InsertBefore(tree, LIR.SeqTree(_compiler, save));
        DISPTREE(save);
        JITDUMP("\n");
    }

    private unsafe void insertUpperVectorRestore(GenTree? tree, RefPosition reference, Interval upper, BasicBlock block)
    {
        if (!Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED    );
            return;
        }

        if (Configuration.DEBUG)
        {
            JITDUMP($"Inserting UpperVectorRestore for RP #{reference.rpNum} ");
        }
        var localInterval = upper.relatedInterval;
        if (localInterval is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.UpperVectorRestoreRequiresItsLocalInterval
    );
            return;
        }
        assert(localInterval.isLocalVar && localInterval.physReg != REG_NA);
        ref var local = ref _compiler.lvaGetDesc(checked((int)localInterval.varNum));
        assert(LsraConfiguration.VarTypeNeedsPartialCalleeSave(local.GetRegisterType()));

        var source = _compiler.gtNewLclvNode(local.Type, checked((int)localInterval.varNum));
        source.RegNum = localInterval.physReg;
        if (Configuration.DEBUG)
        {
            source._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
        }
        var restore = new GenTreeIntrinsic(local.Type, source, NI_SIMD_UpperRestore, methodHandle: null);
        if (Configuration.FEATURE_READYTORUN)
        {
            restore.EntryPoint = new CORINFO_CONST_LOOKUP { accessType = IAT_VALUE };
        }

        if (Configuration.DEBUG)
        {
            restore._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
        }
        var restoreReg = upper.physReg;
        if (restoreReg == REG_NA)
        {
            assert(localInterval.isSpilled);
            if (Configuration.TARGET_AMD64)
            {
                assert(reference.assignedReg() == REG_NA);
                restore.Flags |= GTF_NOREG_AT_USE;
            }
            else
            {
                restore.Flags |= GTF_SPILLED;
                assert(reference.assignedReg() != REG_NA);
                restoreReg = reference.assignedReg();
            }
        }
        restore.RegNum = restoreReg;

        if (tree is not null)
        {
            var useNode = tree;
            while (true)
            {
                if (!block.TryGetUse(useNode, out var use))
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.UpperVectorRestoreRequiresAnOwningUse);
                    return;
                }
                useNode = use.User();
                if (!useNode.IsContained)
                {
                    break;
                }
            }
            JITDUMP($"before {useNode.TreeId}.{useNode.Oper.Name}:\n");
            block.InsertBefore(useNode, LIR.SeqTree(_compiler, restore));
        }
        else
        {
            JITDUMP($"at end of {FMT_BB(block.bbNum)}:\n");
            if (block.Kind is BBJ_COND or BBJ_SWITCH)
            {
                noway_assert(!block.IsEmpty);
                var branch = block.LastNode;
                if (branch is null)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.VectorRestoreBeforeABranchRequiresATerminator
    );
                    return;
                }
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
}
