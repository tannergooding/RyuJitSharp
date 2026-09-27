// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp.

using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.JitFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe PhaseStatus fgExpandThreadLocalAccess()
    {
        if (!MethodHasTlsFieldAccess)
        {
            JITDUMP("Nothing to expand.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        // NativeAOT has no generated slow TLS helper and must expand even rarely run blocks.
        if (IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            return fgExpandHelper(fgExpandThreadLocalAccessForCallNativeAOT);
        }

        if (opts.OptimizationDisabled)
        {
            JITDUMP("Optimizations aren't allowed - bail out.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.jitFlags->IsSet(JIT_FLAG_SIZE_OPT))
        {
            JITDUMP("Optimized for size - bail out.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        return fgExpandHelper(fgExpandThreadLocalAccessForCall, skipRarelyRunBlocks: true);
    }

    private unsafe bool fgExpandThreadLocalAccessForCallNativeAOT(ref BasicBlock block, Statement stmt, GenTreeCall call)
    {
        assert(IsTargetAbi(CORINFO_NATIVEAOT_ABI));
        if (!call.IsHelperCall() || (call.HelperNum is not CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR))
        {
            return false;
        }

        if (!TargetOS.IsWindows || !TargetArchitecture.IsX64)
        {
            throw new FatalJitException("NativeAOT TLS expansion requires a port for this target.");
        }

#if DEBUG
        JITDUMP($"Expanding thread static local access for [{call.TreeId:D6}] in {FMT_BB(block.bbNum)}:\n");
        DISPTREE(call);
        JITDUMP("\n");
#endif

        var threadStaticInfo = default(CORINFO_THREAD_STATIC_INFO_NATIVEAOT);
        info.compCompHnd->getThreadLocalStaticInfo_NativeAOT(&threadStaticInfo);
#if DEBUG
        JITDUMP($"tlsRootObject= {dspPtr(threadStaticInfo.tlsRootObject.addr):X16}\n");
        JITDUMP($"tlsIndexObject= {dspPtr(threadStaticInfo.tlsIndexObject.addr):X16}\n");
        JITDUMP($"offsetOfThreadLocalStoragePointer= {DspThreadLocalOffset(threadStaticInfo.offsetOfThreadLocalStoragePointer)}\n");
        JITDUMP($"threadStaticBaseSlow= {dspPtr(threadStaticInfo.threadStaticBaseSlow.addr):X16}\n");
#endif

        var previous = block;
        ref var callUse = ref fgSplitBlockBeforeTree(block, stmt, call, out var firstNewStmt, out block);
        var debugInfo = stmt.DebugInfo;

        // The TLS blob is an unpinned managed object, so its result local is a GC ref.
        var finalLocal = lvaGrabTemp(shortLifetime: true, "Final offset");
        lvaTable[finalLocal].Type = TYP_REF;
        var finalValue = gtNewLclvNode(TYP_REF, finalLocal);

        while ((firstNewStmt is not null) && (firstNewStmt != stmt))
        {
            fgMorphStmtBlockOps(block, firstNewStmt);
            firstNewStmt = firstNewStmt.NextStmt;
        }

        // Windows TLS slot -> module TLS index -> module TLS base -> section-relative root slot.
        GenTree tlsValue = gtNewIconHandleNode(
            unchecked((nint)(uint)threadStaticInfo.offsetOfThreadLocalStoragePointer), GTF_ICON_TLS_HDL);
        tlsValue = gtNewIndir(TYP_I_IMPL, tlsValue, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);

        GenTree dllRef = gtNewIconHandleNode((nint)threadStaticInfo.tlsIndexObject.handle, GTF_ICON_CONST_PTR);
        dllRef = gtNewIndir(TYP_INT, dllRef, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        dllRef = gtNewCastNode(TYP_I_IMPL, dllRef, true, TYP_I_IMPL);
        dllRef = gtNewBinaryNode(GT_LSH, TYP_I_IMPL, dllRef, gtNewIconNode(TYP_I_IMPL, 3));
        tlsValue = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, tlsValue, dllRef);
        tlsValue = gtNewIndir(TYP_I_IMPL, tlsValue, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);

        var tlsRootOffset = gtNewIconNode(TYP_INT, (nint)threadStaticInfo.tlsRootObject.handle);
        tlsRootOffset.Flags |= GTF_ICON_SECREL_OFFSET;
        var tlsRootAddress = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, tlsValue, tlsRootOffset);

        var rootAddressLocal = lvaGrabTemp(shortLifetime: true, "TlsRootAddr access");
        lvaTable[rootAddressLocal].Type = TYP_I_IMPL;
        var rootAddressDef = gtNewStoreLclVarNode(rootAddressLocal, tlsRootAddress);
        var rootValue = gtNewIndir(TYP_REF, gtNewLclvNode(TYP_I_IMPL, rootAddressLocal),
            GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        var rootDef = gtNewStoreLclVarNode(finalLocal, rootValue);
        var rootPresent = gtNewBinaryNode(GT_NE, TYP_INT,
            CloneThreadLocalTree(finalValue), gtNewIconNode(TYP_I_IMPL, 0));
        var rootCondition = gtNewUnaryNode(GT_JTRUE, TYP_VOID, rootPresent);

        var rootCheck = fgNewBBFromTreeAfter(BBJ_COND, previous, rootAddressDef, debugInfo);
        fgInsertStmtAfter(rootCheck, rootCheck.FirstStmt!, fgNewStmtFromTree(rootCondition));
        fgInsertStmtAfter(rootCheck, rootCheck.FirstStmt!, fgNewStmtFromTree(rootDef));

        var slowCall = gtNewIndCallNode(TYP_REF,
            gtNewIconHandleNode((nint)threadStaticInfo.threadStaticBaseSlow.addr, GTF_ICON_TLS_HDL));
        _ = slowCall.Args.PushBack(NewCallArg.CreateForPrimitive(gtNewLclvNode(TYP_I_IMPL, rootAddressLocal)));
        fgMorphArgs(slowCall);
        var fallback = fgNewBBFromTreeAfter(BBJ_ALWAYS, rootCheck,
            gtNewStoreLclVarNode(finalLocal, slowCall), debugInfo, true);
        var fast = fgNewBBFromTreeAfter(BBJ_ALWAYS, fallback,
            gtNewStoreLclVarNode(finalLocal, CloneThreadLocalTree(finalValue)), debugInfo, true);

        callUse = finalValue;
        fgMorphStmtBlockOps(block, stmt);
        gtUpdateStmtSideEffects(stmt);

        var trueEdge = fgAddRefPred(fast, rootCheck);
        var falseEdge = fgAddRefPred(fallback, rootCheck);
        rootCheck.SetCond(trueEdge, falseEdge);
        trueEdge.Likelihood = 1.0;
        falseEdge.Likelihood = 0.0;
        fallback.TargetEdge = fgAddRefPred(block, fallback);
        fast.TargetEdge = fgAddRefPred(block, fast);

        block.inheritWeight(previous);
        rootCheck.inheritWeight(previous);
        fast.inheritWeight(previous);
        fallback.inheritWeightPercentage(rootCheck, 0);
        fgRedirectEdge(ref previous.TargetEdgeRef, rootCheck);

        assert(BasicBlock.sameEHRegion(previous, block));
        assert(BasicBlock.sameEHRegion(previous, rootCheck));
        assert(BasicBlock.sameEHRegion(previous, fast));
        JITDUMP($"tlsRootNullCondBB: {FMT_BB(rootCheck.bbNum)}\n");
        JITDUMP($"fallbackBb: {FMT_BB(fallback.bbNum)}\n");
        JITDUMP($"fastPathBb: {FMT_BB(fast.bbNum)}\n");
        return true;
    }

    private unsafe bool fgExpandThreadLocalAccessForCall(ref BasicBlock block, Statement stmt, GenTreeCall call)
    {
        assert(!IsAot);
        var helper = call.HelperNum;
        if (helper is not (CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
            or CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
            or CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2))
        {
            return false;
        }

        if (!TargetOS.IsWindows || !TargetArchitecture.IsX64)
        {
            throw new FatalJitException("CoreCLR TLS expansion requires a port for this target.");
        }

#if DEBUG
        JITDUMP($"Expanding thread static local access for [{call.TreeId:D6}] in {FMT_BB(block.bbNum)}:\n");
        DISPTREE(call);
        JITDUMP("\n");
#endif
        var metadata = default(CORINFO_THREAD_STATIC_BLOCKS_INFO);
        info.compCompHnd->getThreadLocalStaticBlocksInfo(&metadata);
#if DEBUG
        JITDUMP("getThreadLocalStaticBlocksInfo\n:");
        JITDUMP($"tlsIndex= {dspPtr(metadata.tlsIndex.addr):X16}\n");
        JITDUMP($"tlsGetAddrFtnPtr= {dspPtr(metadata.tlsGetAddrFtnPtr):X16}\n");
        JITDUMP($"tlsIndexObject= {dspPtr(metadata.tlsIndexObject):X16}\n");
        JITDUMP($"threadVarsSection= {dspPtr(metadata.threadVarsSection):X16}\n");
        JITDUMP($"offsetOfThreadLocalStoragePointer= {DspThreadLocalOffset(metadata.offsetOfThreadLocalStoragePointer)}\n");
        JITDUMP($"offsetOfMaxThreadStaticBlocks= {DspThreadLocalOffset(metadata.offsetOfMaxThreadStaticBlocks)}\n");
        JITDUMP($"offsetOfThreadStaticBlocks= {DspThreadLocalOffset(metadata.offsetOfThreadStaticBlocks)}\n");
        JITDUMP($"offsetOfBaseOfThreadLocalData= {DspThreadLocalOffset(metadata.offsetOfBaseOfThreadLocalData)}\n");
#endif
        assert(call.Args.CountUserArgs() == 1);
        var previous = block;
        ref var callUse = ref fgSplitBlockBeforeTree(block, stmt, call, out var firstNewStmt, out block);
        var debugInfo = stmt.DebugInfo;
        while ((firstNewStmt is not null) && (firstNewStmt != stmt))
        {
            fgMorphStmtBlockOps(block, firstNewStmt);
            firstNewStmt = firstNewStmt.NextStmt;
        }

        var resultLocal = lvaGrabTemp(shortLifetime: true, "TLS field access");
        lvaTable[resultLocal].Type = call.Type;
        var resultValue = gtNewLclvNode(call.Type, resultLocal);
        callUse = CloneThreadLocalTree(resultValue);
        fgMorphStmtBlockOps(block, stmt);
        gtUpdateStmtSideEffects(stmt);

        var tlsLocal = lvaGrabTemp(shortLifetime: true, "TLS access");
        lvaTable[tlsLocal].Type = TYP_I_IMPL;
        var tlsIndex = (nuint)metadata.tlsIndex.addr;
        GenTree? dllRef = null;

        if (tlsIndex != 0)
        {
            dllRef = gtNewIconHandleNode(unchecked((nint)(tlsIndex * TARGET_POINTER_SIZE)), GTF_ICON_TLS_HDL);
        }

        GenTree tlsValue = gtNewIconHandleNode(unchecked((nint)(uint)metadata.offsetOfThreadLocalStoragePointer),
            GTF_ICON_TLS_HDL);
        tlsValue = gtNewIndir(TYP_I_IMPL, tlsValue, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);

        if (dllRef is not null)
        {
            tlsValue = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, tlsValue, dllRef);
        }

        tlsValue = gtNewIndir(TYP_I_IMPL, tlsValue, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        var tlsDef = gtNewStoreLclVarNode(tlsLocal, tlsValue);
        var tlsLocalValue = gtNewLclVarNode(TYP_I_IMPL, tlsLocal);
        var typeIndex = call.Args.GetUserArgByIndex(0)?.Node
            ?? throw new FatalJitException("Thread static helper requires a type index.");
        assert(typeIndex.Type.ActualType is TYP_INT);

        if (helper is CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2)
        {
            var index = gtFoldExpr(gtNewCastNode(TYP_I_IMPL, CloneThreadLocalTree(typeIndex), true, TYP_I_IMPL));
            var offset = gtFoldExpr(gtNewBinaryNode(GT_ADD, TYP_I_IMPL, index,
                gtNewIconNode(TYP_I_IMPL, unchecked((nint)(uint)metadata.offsetOfBaseOfThreadLocalData))));
            var baseAddress = gtNewBinaryNode(GT_ADD, TYP_I_IMPL,
                CloneThreadLocalTree(tlsLocalValue), offset);
            var baseStore = gtNewStoreLclVarNode(resultLocal, baseAddress);
            var compute = fgNewBBFromTreeAfter(BBJ_ALWAYS, previous, tlsDef, debugInfo, true);
            fgInsertStmtAfter(compute, compute.FirstStmt!, fgNewStmtFromTree(baseStore));
            compute.TargetEdge = fgAddRefPred(block, compute);
            assert(previous.Kind is BBJ_ALWAYS);
            fgRedirectEdge(ref previous.TargetEdgeRef, compute);
            block.inheritWeight(previous);
            compute.inheritWeight(previous);
            assert(BasicBlock.sameEHRegion(previous, block));
            assert(BasicBlock.sameEHRegion(previous, compute));
            return true;
        }

        var maxOffset = gtNewIconNode(TYP_I_IMPL, unchecked((nint)(uint)metadata.offsetOfMaxThreadStaticBlocks));
        var maxAddress = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, CloneThreadLocalTree(tlsLocalValue), maxOffset);
        var maxCount = gtNewIndir(TYP_INT, maxAddress, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        var blocksOffset = gtNewIconNode(TYP_I_IMPL, unchecked((nint)(uint)metadata.offsetOfThreadStaticBlocks));
        var blocksAddress = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, CloneThreadLocalTree(tlsLocalValue), blocksOffset);
        var blocks = gtNewIndir(TYP_REF, blocksAddress, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        var countExceeded = gtNewBinaryNode(GT_LE, TYP_INT, maxCount, CloneThreadLocalTree(typeIndex));
        var maxCondition = gtNewUnaryNode(GT_JTRUE, TYP_VOID, countExceeded);

        var pointerSize = gtNewIconNode(TYP_INT, TARGET_POINTER_SIZE);
        var scaledIndex = gtFoldExpr(gtNewBinaryNode(GT_MUL, TYP_INT, CloneThreadLocalTree(typeIndex), pointerSize));
        scaledIndex = gtFoldExpr(gtNewCastNode(TYP_I_IMPL, scaledIndex, true, TYP_I_IMPL));
        var blockAddress = gtNewBinaryNode(GT_ADD, TYP_BYREF, blocks, scaledIndex);
        var blockValue = gtNewIndir(TYP_BYREF, blockAddress, GTF_IND_NONFAULTING);
        var blockBaseLocal = lvaGrabTemp(shortLifetime: true, "ThreadStaticBlockBase access");
        lvaTable[blockBaseLocal].Type = TYP_BYREF;
        var blockBaseDef = gtNewStoreLclVarNode(blockBaseLocal, blockValue);
        var blockBaseValue = gtNewLclVarNode(TYP_BYREF, blockBaseLocal);
        var blockPresent = gtNewBinaryNode(GT_NE, TYP_INT,
            blockBaseValue, gtNewIconNode(TYP_I_IMPL, 0));
        var blockCondition = gtNewUnaryNode(GT_JTRUE, TYP_VOID, blockPresent);

        var maxCheck = fgNewBBFromTreeAfter(BBJ_COND, previous, tlsDef, debugInfo);
        fgInsertStmtAfter(maxCheck, maxCheck.FirstStmt!, fgNewStmtFromTree(maxCondition));
        var blockCheck = fgNewBBFromTreeAfter(BBJ_COND, maxCheck, blockBaseDef, debugInfo);
        fgInsertStmtAfter(blockCheck, blockCheck.FirstStmt!, fgNewStmtFromTree(blockCondition));
        var fallback = fgNewBBFromTreeAfter(BBJ_ALWAYS, blockCheck,
            gtNewStoreLclVarNode(resultLocal, call), debugInfo, true);
        var fast = fgNewBBFromTreeAfter(BBJ_ALWAYS, fallback,
            gtNewStoreLclVarNode(resultLocal, CloneThreadLocalTree(blockBaseValue)), debugInfo, true);

        assert(previous.Kind is BBJ_ALWAYS);
        fgRedirectEdge(ref previous.TargetEdgeRef, maxCheck);
        var maxTrue = fgAddRefPred(fallback, maxCheck);
        var maxFalse = fgAddRefPred(blockCheck, maxCheck);
        maxCheck.SetCond(maxTrue, maxFalse);
        maxTrue.Likelihood = 0.0;
        maxFalse.Likelihood = 1.0;
        var blockTrue = fgAddRefPred(fast, blockCheck);
        var blockFalse = fgAddRefPred(fallback, blockCheck);
        blockCheck.SetCond(blockTrue, blockFalse);
        blockTrue.Likelihood = 1.0;
        blockFalse.Likelihood = 0.0;
        fast.TargetEdge = fgAddRefPred(block, fast);
        fallback.TargetEdge = fgAddRefPred(block, fallback);

        block.inheritWeight(previous);
        maxCheck.inheritWeight(previous);
        blockCheck.inheritWeight(previous);
        fast.inheritWeight(previous);
        fallback.inheritWeightPercentage(previous, 0);
        assert(BasicBlock.sameEHRegion(previous, block));
        assert(BasicBlock.sameEHRegion(previous, maxCheck));
        assert(BasicBlock.sameEHRegion(previous, blockCheck));
        assert(BasicBlock.sameEHRegion(previous, fast));
        return true;
    }

    private GenTree CloneThreadLocalTree(GenTree tree)
        => gtCloneExpr(tree) ?? throw new FatalJitException("Cannot clone a thread static expression.");

#if DEBUG
    private uint DspThreadLocalOffset(int offset)
        => unchecked((uint)dspOffset(unchecked((nint)(uint)offset)));
#endif
}
