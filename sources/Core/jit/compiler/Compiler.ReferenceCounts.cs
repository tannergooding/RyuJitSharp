// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe PhaseStatus lvaMarkLocalVars()
    {
        JITDUMP("\n*************** In lvaMarkLocalVars()");
        if (compMethodRequiresPInvokeFrame)
        {
            assert(!opts.ShouldUsePInvokeHelpers || (info.compLvFrameListRoot == BAD_VAR_NUM));
            if (!opts.ShouldUsePInvokeHelpers)
            {
                noway_assert((info.compLvFrameListRoot >= info.compLocalsCount) && (info.compLvFrameListRoot < lvaCount));
            }
        }

        var originalLocalCount = lvaCount;
#if JIT32_GCENCODER
        if (compLocallocUsed)
        {
            lvaLocAllocSPvar = lvaGrabTempWithImplicitUse(false, "LocAllocSPvar");
            lvaGetDesc(lvaLocAllocSPvar).Type = TYP_I_IMPL;
        }
#endif
        lvaRefCountState = RCS_NORMAL;
#if DEBUG
        const bool setSlotNumbers = true;
#else
        var setSlotNumbers = opts.compScopeInfo && (info.compVarScopesCount > 0);
#endif
        lvaComputeRefCounts(isRecompute: false, setSlotNumbers);

        if (!PreciseRefCountsRequired)
        {
            return lvaCount != originalLocalCount ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
        }

        var reportParamTypeArg = lvaReportParamTypeArg();
        if (lvaKeepAliveAndReportThis())
        {
            lvaGetDesc(info.compThisArg).lvImplicitlyReferenced = reportParamTypeArg;
        }
        else if (lvaReportParamTypeArg())
        {
            assert(info.compTypeCtxtArg != BAD_VAR_NUM);
            lvaGetDesc(info.compTypeCtxtArg).lvImplicitlyReferenced = reportParamTypeArg;
        }

        assert(PreciseRefCountsRequired);

        return lvaCount != originalLocalCount ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    public bool backendRequiresLocalVarLifetimes()
    {
        if (!opts.MinOpts)
        {
            return true;
        }

        var allocator = _regAlloc;
        assert(allocator is not null);

        return allocator.WillEnregisterLocalVars();
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    public static bool varTypeNeedsPartialCalleeSave(var_types type)
    {
        assert(type is not TYP_STRUCT);
#if TARGET_AMD64
        return type is TYP_SIMD32 or TYP_SIMD64;
#elif TARGET_ARM64
        return type is TYP_SIMD16 or TYP_SIMD12;
#else
        throw new System.NotImplementedException("Partial SIMD callee saves are unsupported on this target.");
#endif
    }
#endif

    public void lvaComputeRefCounts(bool isRecompute, bool setSlotNumbers)
    {
#if WINDOWS_AMD64_ABI
        JITDUMP("\n*** lvaComputeRefCounts ***\n");
        if (!PreciseRefCountsRequired)
        {
            if (isRecompute)
            {
#if DEBUG
                for (var localNumber = 0; localNumber < lvaCount; localNumber++)
                {
                    ref var descriptor = ref lvaGetDesc(localNumber);
                    var specialVarargsParameter = descriptor.lvIsParam && lvaIsArgAccessedViaVarArgsCookie(localNumber);
                    if (specialVarargsParameter)
                    {
                        assert(descriptor.lvRefCnt() == 0);
                    }
                    else
                    {
                        assert(descriptor.lvImplicitlyReferenced);
                    }
                    assert(!descriptor.lvTracked);
                }
#endif
                return;
            }

            for (var localNumber = 0; localNumber < lvaCount; localNumber++)
            {
                ref var descriptor = ref lvaGetDesc(localNumber);
                descriptor.setLvRefCnt(0);
                descriptor.setLvRefCntWtd(BB_ZERO_WEIGHT);
                var specialVarargsParameter = descriptor.lvIsParam && lvaIsArgAccessedViaVarArgsCookie(localNumber);
                if (!specialVarargsParameter)
                {
                    descriptor.lvImplicitlyReferenced = true;
                }
                descriptor.lvTracked = false;
                if (setSlotNumbers)
                {
                    descriptor.lvSlotNum = localNumber;
                }
                assert(descriptor.Type is not TYP_UNDEF and not TYP_VOID and not TYP_UNKNOWN);
            }

            lvaCurEpoch = unchecked(lvaCurEpoch + 1);
            lvaTrackedCount = 0;
            lvaTrackedCountInSizeTUnits = 0;
            return;
        }

        lvaComputePreciseRefCounts(isRecompute, setSlotNumbers);
#else
        throw new System.NotImplementedException("Local reference accounting outside Windows AMD64 is not ported.");
#endif
    }

    private void lvaComputePreciseRefCounts(bool isRecompute, bool setSlotNumbers)
    {
        for (var localNumber = 0; localNumber < lvaCount; localNumber++)
        {
            ref var descriptor = ref lvaGetDesc(localNumber);
            descriptor.setLvRefCnt(0);
            descriptor.setLvRefCntWtd(BB_ZERO_WEIGHT);
            if (setSlotNumbers)
            {
                descriptor.lvSlotNum = localNumber;
            }
            if (!isRecompute)
            {
                descriptor.lvSingleDef = descriptor.lvIsParam || descriptor.lvIsParamRegTarget;
                descriptor.lvSingleDefRegCandidate = descriptor.lvIsParam || descriptor.lvIsParamRegTarget;
                descriptor.lvAllDefsAreNoGc = !descriptor.lvImplicitlyReferenced;
            }
        }

        var oldContextInUse = lvaGenericsContextInUse;
        lvaGenericsContextInUse = false;
        JITDUMP("\n*** lvaComputePreciseRefCounts -- explicit counts ***\n");

        foreach (var block in Blocks)
        {
            if (block.IsLIR)
            {
                assert(isRecompute);
                var weight = block.getBBWeight(this);
                foreach (var node in block)
                {
                    if (node.Oper.IsAnyLocal)
                    {
                        ref var descriptor = ref lvaGetDesc(node.AsLclVarCommon().LclNum);
                        // Enregistering an EH local saves loads, but its defs must
                        // still update the stack home, so give those defs no weight.
                        if (descriptor.lvTracked && descriptor.IsLiveInOutOfHandler && !descriptor.lvDoNotEnregister &&
                            ((node.Flags & GTF_VAR_DEF) != 0))
                        {
                            descriptor.incRefCnts(0, this);
                        }
                        else
                        {
                            descriptor.incRefCnts(weight, this);
                        }
                        if ((node.Flags & GTF_VAR_CONTEXT) != 0)
                        {
                            assert(node.Oper is GT_LCL_VAR);
                            lvaGenericsContextInUse = true;
                        }
                    }
                }
            }
            else
            {
                lvaMarkLocalVars(block);
            }
        }

        if (oldContextInUse && !lvaGenericsContextInUse)
        {
            JITDUMP("\n** Generics context no longer in use\n");
        }
        else if (lvaGenericsContextInUse && !oldContextInUse)
        {
            assert(false, "unexpected new use of generics context");
        }

        JITDUMP("\n*** lvaComputePreciseRefCounts -- implicit counts ***\n");
        for (var localNumber = 0; localNumber < lvaCount; localNumber++)
        {
            ref var descriptor = ref lvaGetDesc(localNumber);
            if (descriptor.lvIsRegArg)
            {
                if ((localNumber < info.compArgsCount) && (descriptor.lvRefCnt() > 0))
                {
                    descriptor.incRefCnts(BB_UNITY_WEIGHT, this);
                    descriptor.incRefCnts(BB_UNITY_WEIGHT, this);
                }
                if (descriptor.lvIsStructField && varTypeIsStruct(lvaGetDesc(descriptor.lvParentLcl).Type))
                {
                    descriptor.incRefCnts(BB_UNITY_WEIGHT, this);
                }
            }
            else if (descriptor.lvIsParamRegTarget && (descriptor.lvRefCnt() > 0))
            {
                descriptor.incRefCnts(BB_UNITY_WEIGHT, this);
                descriptor.incRefCnts(BB_UNITY_WEIGHT, this);
            }

            if (compJmpOpUsed && descriptor.lvIsParam && (descriptor.lvRefCnt() == 0) &&
                !lvaIsArgAccessedViaVarArgsCookie(localNumber))
            {
                descriptor.lvImplicitlyReferenced = true;
            }
            if (descriptor.lvPinned && descriptor.lvAllDefsAreNoGc)
            {
                descriptor.lvPinned = false;
                JITDUMP($"V{localNumber:D2} was unpinned as all def candidates were local.\n");
            }
        }
    }

    private void lvaMarkLclRefs(GenTree tree, BasicBlock block, Statement statement)
    {
        var weight = block.getBBWeight(this);
        if ((tree.Oper is GT_CALL) && compMethodRequiresPInvokeFrame)
        {
            assert(!opts.ShouldUsePInvokeHelpers || (info.compLvFrameListRoot == BAD_VAR_NUM));
            if (!opts.ShouldUsePInvokeHelpers)
            {
                ref var frameRoot = ref lvaGetDesc(info.compLvFrameListRoot);
                frameRoot.incRefCnts(weight, this);
                frameRoot.incRefCnts(weight, this);
            }
        }

        if (tree.Oper is GT_LCL_ADDR)
        {
            ref var addressLocal = ref lvaGetDesc(tree.AsLclVarCommon().LclNum);
#if DEBUG
            assert(addressLocal.IsAddressExposed || addressLocal.IsDefinedViaAddress);
#endif
            addressLocal.incRefCnts(weight, this);
            return;
        }
        if (!tree.Oper.IsLocal)
        {
            return;
        }

        if ((tree.Flags & GTF_VAR_CONTEXT) != 0)
        {
            assert(tree.Oper is GT_LCL_VAR);
            if (!lvaGenericsContextInUse)
            {
#if DEBUG
                JITDUMP($"-- generic context in use at [{tree.TreeId:D6}]\n");
#endif
                lvaGenericsContextInUse = true;
            }
        }

        var localNumber = tree.AsLclVarCommon().LclNum;
        ref var descriptor = ref lvaGetDesc(localNumber);
        descriptor.incRefCnts(weight, this);
#if DEBUG
        if (descriptor.lvIsStructField)
        {
            assert(!lvaGetDesc(descriptor.lvParentLcl).lvUndoneStructPromotion);
        }
#endif
        if (descriptor.IsAddressExposed)
        {
            descriptor.lvAllDefsAreNoGc = false;
        }
        if (!tree.Oper.IsScalarLocal)
        {
            return;
        }
        if ((_domTree is not null) && IsDominatedByExceptionalEntry(block))
        {
            SetHasExceptionalUsesHint(ref descriptor);
        }

        if (tree.Oper is GT_STORE_LCL_VAR)
        {
            var value = tree.AsLclVar().Data;
            if (descriptor.lvPinned && descriptor.lvAllDefsAreNoGc && !value.IsNotGcDef())
            {
                descriptor.lvAllDefsAreNoGc = false;
            }
            if (!descriptor.lvDisqualifySingleDefRegCandidate)
            {
                var inLoop = block.HasFlag(BBF_BACKWARD_JUMP);
                var isReturn = block.Kind is BBJ_RETURN;
                var needsExplicitZeroInit = fgVarNeedsExplicitZeroInit(localNumber, inLoop, isReturn);
                if (descriptor.lvSingleDefRegCandidate || needsExplicitZeroInit)
                {
#if DEBUG
                    if (needsExplicitZeroInit)
                    {
                        descriptor.lvSingleDefDisqualifyReason = (byte)'Z';
                        JITDUMP($"V{localNumber:D2} needs explicit zero init. Disqualified as a single-def register candidate.\n");
                    }
                    else
                    {
                        descriptor.lvSingleDefDisqualifyReason = (byte)'M';
                        JITDUMP($"V{localNumber:D2} has multiple definitions. Disqualified as a single-def register candidate.\n");
                    }
#endif
                    descriptor.lvSingleDefRegCandidate = false;
                    descriptor.lvDisqualifySingleDefRegCandidate = true;
                }
                else if (!descriptor.lvDoNotEnregister)
                {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                    if (!varTypeNeedsPartialCalleeSave(descriptor.GetRegisterType()))
#endif
                    {
                        descriptor.lvSingleDefRegCandidate = true;
                        JITDUMP($"Marking EH Var V{localNumber:D2} as a register candidate.\n");
                    }
                }
            }
        }

        assert((tree.Type == descriptor.Type) || (tree.Type == descriptor.Type.ActualType) ||
            ((tree.Type is TYP_BYREF) && (descriptor.Type is TYP_I_IMPL)) ||
            ((tree.Type is TYP_INT) && (descriptor.Type is TYP_LONG)));
    }

    private bool IsDominatedByExceptionalEntry(BasicBlock block)
    {
        assert(_domTree is not null);

        return block.HasFlag(BBF_DOMINATED_BY_EXCEPTIONAL_ENTRY);
    }

    private static void SetHasExceptionalUsesHint(ref LclVarDsc descriptor)
    {
        descriptor.lvHasExceptionalUsesHint = true;
    }

    private void lvaMarkLocalVars(BasicBlock block)
    {
#if DEBUG
        JITDUMP($"\n*** marking local variables in block {FMT_BB(block.bbNum)} (weight={refCntWtd2str(block.getBBWeight(this))})\n");
#endif
        for (var statement = block.GetFirstNonPhiDef(); statement is not null; statement = statement.NextStmt)
        {
            var visitor = new MarkLocalVarsVisitor(this, block, statement);
            DISPSTMT(statement);
            _ = visitor.WalkTree(ref statement.RootNodeRef, null);
        }
    }

    private struct MarkLocalVarsVisitor : IGenTreeVisitor<MarkLocalVarsVisitor>
    {
        private readonly Compiler _compiler;
        private readonly BasicBlock _block;
        private readonly Statement _statement;
        private readonly GenTreeStack _ancestors;

        public MarkLocalVarsVisitor(Compiler compiler, BasicBlock block, Statement statement)
        {
            _compiler = compiler;
            _block = block;
            _statement = statement;
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            _compiler.lvaMarkLclRefs(use, _block, _statement);
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<MarkLocalVarsVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
