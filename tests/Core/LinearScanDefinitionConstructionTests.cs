// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanDefinitionConstructionTests
{
    [Test]
    public static void InternalRegisterDefinitionsCreateMatchingDelayedUses()
    {
        WithAllocator((compiler, allocator) => {
            compiler.compFloatingPointUsed = true;
            var intTree = compiler.gtNewIconNode(TYP_INT, 19);
            var floatTree = compiler.gtNewDconNode(TYP_DOUBLE, 2.5);
            ReferenceBuildLocation(allocator) = 4;
            SetInternalRegistersDelayFree(allocator) = true;

            var intDefinition = BuildInternalIntRegisterDef(allocator, intTree, SRBM_RAX);
            var floatDefinition = BuildInternalFloatRegisterDef(allocator, floatTree, SRBM_XMM0);
            BuildInternalRegisterUses(allocator);

            Assert.That(intDefinition.getInterval().isInternal, Is.True);
            Assert.That(intDefinition.getInterval().registerType, Is.EqualTo(TYP_INT));
            Assert.That(floatDefinition.getInterval().isInternal, Is.True);
            Assert.That(floatDefinition.getInterval().registerType, Is.EqualTo(TYP_FLOAT));

            var intUse = intDefinition.nextRefPosition
                ?? throw new AssertionException("The internal integer use was not created.");
            var floatUse = floatDefinition.nextRefPosition
                ?? throw new AssertionException("The internal floating-point use was not created.");
            Assert.That(intUse.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(floatUse.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(intUse.treeNode, Is.SameAs(intTree));
            Assert.That(floatUse.treeNode, Is.SameAs(floatTree));
            Assert.That(intUse.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(floatUse.registerAssignment, Is.EqualTo(SRBM_XMM0));
            Assert.That(intUse.delayRegFree, Is.True);
            Assert.That(floatUse.delayRegFree, Is.True);
            Assert.That(PendingDelayFree(allocator), Is.True);
        });
    }

    [Test]
    public static void DefinitionWithKillsBuildsKillReferencesBeforeTheDefinition()
    {
        WithAllocator((compiler, allocator) => {
            ReferenceBuildLocation(allocator) = 4;
            var tree = new GenTreeOp(GT_DIV, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 23), compiler.gtNewIconNode(TYP_INT, 5));

            BuildDefWithKills(allocator, tree, 1, SRBM_RAX, new regMaskTP(SRBM_RAX | SRBM_RDX));

            Assert.That(allocator.refPositions[0].refType, Is.EqualTo(RefType.RefTypeKill));
            Assert.That(allocator.refPositions[0].nodeLocation, Is.EqualTo(5));
            Assert.That(allocator.refPositions[0].getKilledRegisters(), Is.EqualTo(new regMaskTP(SRBM_RAX | SRBM_RDX)));
            var definition = allocator.intervals[^1].firstRefPosition
                ?? throw new AssertionException("The call result definition was not created.");
            Assert.That(definition.refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(definition.nodeLocation, Is.EqualTo(5));
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void KillConstructionAddsGCReferenceKillForStartPreemptGc()
    {
        WithAllocator((compiler, allocator) => {
            var tree = new GenTree(GT_START_PREEMPTGC, TYP_VOID);

            Assert.That(BuildKillPositionsForNode(allocator, tree, 7, new regMaskTP(SRBM_NONE)), Is.True);

            var kill = allocator.refPositions[^1];
            Assert.That(kill.refType, Is.EqualTo(RefType.RefTypeKillGCRefs));
            Assert.That(kill.nodeLocation, Is.EqualTo(7));
            Assert.That(kill.treeNode, Is.SameAs(tree));
            Assert.That(kill.registerAssignment, Is.Not.EqualTo(SRBM_NONE));
        });
    }

    [Test]
    public static void KillConstructionAddsGcReferenceKillForUnmanagedCalls()
    {
        WithAllocator((compiler, allocator) => {
            var tree = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            tree.Flags |= GTF_CALL_UNMANAGED;

            Assert.That(BuildKillPositionsForNode(allocator, tree, 9, new regMaskTP(SRBM_NONE)), Is.True);

            var kill = allocator.refPositions[^1];
            Assert.That(kill.refType, Is.EqualTo(RefType.RefTypeKillGCRefs));
            Assert.That(kill.nodeLocation, Is.EqualTo(9));
            Assert.That(kill.treeNode, Is.SameAs(tree));
        });
    }

    [Test]
    public static void FloatRegisterKillAddsUpperVectorSaveForLiveTemporary()
    {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        WithAllocator((compiler, allocator) => {
            compiler.compFloatingPointUsed = true;
            var vector = new GenTreeVecCon(TYP_SIMD32);
            ReferenceBuildLocation(allocator) = 2;
            var definition = BuildDef(allocator, vector, SRBM_NONE, 0);
            ReferenceBuildLocation(allocator) = 4;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var floatKillSet = GetKillSetForCall(allocator, call);

            BuildKills(allocator, call, floatKillSet);

            var save = allocator.refPositions.FindLast(
                reference => reference.refType is RefType.RefTypeUpperVectorSave)
                ?? throw new AssertionException("The live vector temporary has no upper-vector save reference.");
            Assert.That(save.getInterval(), Is.SameAs(definition.getInterval()));
            Assert.That(save.nodeLocation, Is.EqualTo(5));
            Assert.That(save.registerAssignment, Is.EqualTo(SRBM_FLT_CALLEE_SAVED));
        });
#endif
    }

    [Test]
    public static void FloatRegisterKillSavesTrackedLargeVectorLocalsFromBlockLiveness()
    {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        WithAllocator((compiler, allocator) => {
            compiler.compFloatingPointUsed = true;
            compiler.lvaTable = [
                new() { Type = TYP_SIMD32, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;

#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBJ_ALWAYS);
            block.bbLiveIn = [1];
            block.bbVarDef = [0];
            compiler.compCurBB = block;

            var localInterval = new Interval(TYP_SIMD32, SRBM_ALLFLOAT_INIT);
            allocator.localVarIntervals = [localInterval];
            localInterval.setLocalNumber(compiler, 0, allocator);
            SetNeedToKillFloatRegisters(allocator) = true;
            SetCurrentLiveVariables(allocator) = [1];
            LargeVectorVars(allocator) = [1];
            LargeVectorCalleeSaveCandidateVars(allocator) = [0];
            var candidateUpperInterval = NewInterval(allocator, TYP_SIMD16);
            candidateUpperInterval.relatedInterval = localInterval;
            candidateUpperInterval.isUpperVector = true;

            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var floatKillSet = GetKillSetForCall(allocator, call);
            BuildKills(allocator, call, floatKillSet);

            Assert.That(localInterval.isPartiallySpilled, Is.True);
            var upperInterval = allocator.intervals.Find(interval => interval.IsUpperVector())
                ?? throw new AssertionException("The candidate local has no upper-vector interval.");
            Assert.That(upperInterval.relatedInterval, Is.SameAs(localInterval));
            var save = upperInterval.lastRefPosition
                ?? throw new AssertionException("The large vector local has no upper-vector save.");
            Assert.That(save.refType, Is.EqualTo(RefType.RefTypeUpperVectorSave));
            Assert.That(save.liveVarUpperSave, Is.True);
            Assert.That(save.regOptional, Is.True);
        }, enregisterLocalVars: true);
#endif
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE && WINDOWS_AMD64_ABI
    [Test]
    public static void NonLocalStructCallUsesWindowsAbiReturnClassification()
    {
        WithAllocator((compiler, allocator) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getClassSize = &GetStructClassSize;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;

            var call = new GenTreeCall(TYP_STRUCT) {
                _callType = gtCallTypes.CT_USER_FUNC,
                RetClsHnd = (CORINFO_CLASS_STRUCT_*)1,
            };

            Assert.That(GetUpperVectorTemporaryType(allocator, call), Is.EqualTo(TYP_UNKNOWN));
        });
    }
#endif

    [Test]
    public static void CallKillPreferencesExcludeTrashedRegisters()
    {
        WithAllocator((compiler, allocator) => {
            compiler.compFloatingPointUsed = true;
            var tree = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            ReferenceBuildLocation(allocator) = 2;
            var definition = BuildDef(allocator, tree, SRBM_NONE, 0);
            var interval = definition.getInterval();
            var killSet = GetKillSetForCall(allocator, tree);

            BuildKills(allocator, tree, killSet);

            Assert.That(interval.preferCalleeSave, Is.True);
            Assert.That(interval.registerAversion, Is.EqualTo(compiler.SRBM_INT_CALLEE_TRASH));
            Assert.That(interval.registerPreferences & compiler.SRBM_INT_CALLEE_TRASH, Is.EqualTo(SRBM_NONE));
        });
    }

#if TARGET_AMD64
    [Test]
    public static void HelperCallKillSetsMatchWindowsAmd64Masks()
    {
        WithAllocator((compiler, _) => {
            var calleeTrash = GetCalleeTrashKillMask(compiler);

            Assert.That(HelperCallKillSet(compiler, CORINFO_HELP_ASSIGN_REF),
                Is.EqualTo(new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT)));
            Assert.That(HelperCallKillSet(compiler, CORINFO_HELP_CHECKED_ASSIGN_REF),
                Is.EqualTo(new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT)));
            Assert.That(HelperCallKillSet(compiler, CORINFO_HELP_STOP_FOR_GC),
                Is.EqualTo(RemoveRegisterSets(calleeTrash, SRBM_INTRET, SRBM_FLOATRET, SRBM_NONE)));
            Assert.That(HelperCallKillSet(compiler, CORINFO_HELP_INIT_PINVOKE_FRAME), Is.EqualTo(calleeTrash));
            Assert.That(HelperCallKillSet(compiler, CORINFO_HELP_VALIDATE_INDIRECT_CALL),
                Is.EqualTo(RemoveRegisterSets(
                    new regMaskTP(compiler.SRBM_INT_CALLEE_TRASH), SRBM_R10 | SRBM_RCX, SRBM_NONE, SRBM_NONE)));
            Assert.That(HelperCallKillSet(compiler, CORINFO_HELP_PROF_FCN_LEAVE),
                Is.EqualTo(RemoveRegisterSets(calleeTrash, SRBM_INTRET, SRBM_FLOATRET, SRBM_NONE)));
        });
    }

    [Test]
    public static void IndirectStoreKillSetUsesWriteBarrierClassification()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [
                new() { Type = TYP_BYREF },
                new() { Type = TYP_REF },
            ];
            compiler.lvaCount = 2;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var value = compiler.gtNewLclvNode(TYP_REF, 1);
            var store = new GenTreeStoreInd(TYP_REF, address, value);

            Assert.That(GetKillSetForStoreInd(allocator, store),
                Is.EqualTo(HelperCallKillSet(compiler, CORINFO_HELP_CHECKED_ASSIGN_REF)));

            store.Flags |= GTF_IND_TGT_NOT_HEAP;
            Assert.That(GetKillSetForStoreInd(allocator, store), Is.EqualTo(new regMaskTP(SRBM_NONE)));
        });
    }

#if DEBUG
    [Test]
    public static void DebugKillSetDispatcherMatchesScalarAndCallKills()
    {
        WithAllocator((compiler, allocator) => {
            var dividend = compiler.gtNewIconNode(TYP_INT, 13);
            var divisor = compiler.gtNewIconNode(TYP_INT, 3);
            var divide = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor);
            Assert.That(GetKillSetForNode(allocator, divide), Is.EqualTo(new regMaskTP(SRBM_RAX | SRBM_RDX)));

            var shift = new GenTreeOp(GT_LSH, TYP_INT, dividend, divisor);
            Assert.That(GetKillSetForNode(allocator, shift), Is.EqualTo(new regMaskTP(SRBM_RCX)));
            shift.Op2.IsContained = true;
            Assert.That(GetKillSetForNode(allocator, shift), Is.EqualTo(new regMaskTP(SRBM_NONE)));

            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            Assert.That(GetKillSetForNode(allocator, call), Is.EqualTo(
                RemoveRegisterSets(GetCalleeTrashKillMask(compiler), SRBM_NONE,
                    compiler.SRBM_FLT_CALLEE_TRASH, compiler.SRBM_MSK_CALLEE_TRASH)));
            Assert.That(GetKillSetForNode(allocator, new GenTreeUnOp(GT_RETURNTRAP, TYP_INT, dividend)),
                Is.EqualTo(HelperCallKillSet(compiler, CORINFO_HELP_STOP_FOR_GC)));
            Assert.That(GetKillSetForNode(allocator, new GenTreeOp(GT_PATCHPOINT, TYP_VOID, dividend, divisor)),
                Is.EqualTo(HelperCallKillSet(compiler, CORINFO_HELP_PATCHPOINT)));
            Assert.That(GetKillSetForNode(allocator, new GenTreeUnOp(GT_PATCHPOINT_FORCED, TYP_VOID, dividend)),
                Is.EqualTo(HelperCallKillSet(compiler, CORINFO_HELP_PATCHPOINT_FORCED)));
        });
    }
#endif
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildInternalIntRegisterDefForNode")]
    private static extern RefPosition BuildInternalIntRegisterDef(
        LinearScan allocator, GenTree tree, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildInternalFloatRegisterDefForNode")]
    private static extern RefPosition BuildInternalFloatRegisterDef(
        LinearScan allocator, GenTree tree, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildInternalRegisterUses")]
    private static extern void BuildInternalRegisterUses(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDefWithKills")]
    private static extern void BuildDefWithKills(
        LinearScan allocator, GenTree tree, int destinationCount, regMask candidates, regMaskTP killMask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildKillPositionsForNode")]
    private static extern bool BuildKillPositionsForNode(
        LinearScan allocator, GenTree tree, uint location, regMaskTP killMask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildKills")]
    private static extern void BuildKills(LinearScan allocator, GenTree tree, regMaskTP killMask);

#if TARGET_AMD64
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "compHelperCallKillSet")]
    private static extern regMaskTP HelperCallKillSet(Compiler compiler, CorInfoHelpFunc helper);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getKillSetForStoreInd")]
    private static extern regMaskTP GetKillSetForStoreInd(LinearScan allocator, GenTreeStoreInd tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getKillSetForCall")]
    private static extern regMaskTP GetKillSetForCall(LinearScan allocator, GenTreeCall tree);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getKillSetForNode")]
    private static extern regMaskTP GetKillSetForNode(LinearScan allocator, GenTree tree);
#endif
#endif

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_largeVectorVars")]
    private static extern ref nint[] LargeVectorVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_largeVectorCalleeSaveCandidateVars")]
    private static extern ref nint[] LargeVectorCalleeSaveCandidateVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getUpperVectorTemporaryType")]
    private static extern var_types GetUpperVectorTemporaryType(LinearScan allocator, GenTree tree);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_setInternalRegistersDelayFree")]
    private static extern ref bool SetInternalRegistersDelayFree(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pendingDelayFree")]
    private static extern ref bool PendingDelayFree(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentLiveVariables")]
    private static extern ref nint[] SetCurrentLiveVariables(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_needToKillFloatRegisters")]
    private static extern ref bool SetNeedToKillFloatRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask CompilerIntCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask CompilerMaskCalleeTrash(Compiler compiler);

    private static regMaskTP GetCalleeTrashKillMask(Compiler compiler)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(
            compiler.SRBM_INT_CALLEE_TRASH | compiler.SRBM_FLT_CALLEE_TRASH,
            compiler.SRBM_MSK_CALLEE_TRASH);
#else
        return new regMaskTP(
            compiler.SRBM_INT_CALLEE_TRASH | compiler.SRBM_FLT_CALLEE_TRASH |
            compiler.SRBM_MSK_CALLEE_TRASH);
#endif
    }

    private static regMaskTP RemoveRegisterSets(
        regMaskTP mask, regMask intRegisters, regMask floatRegisters, regMask maskRegisters)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(mask.Lower & ~(intRegisters | floatRegisters), mask.Upper & ~maskRegisters);
#else
        return new regMaskTP(mask.Lower & ~(intRegisters | floatRegisters | maskRegisters));
#endif
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool enregisterLocalVars = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        if (enregisterLocalVars)
        {
            compiler.opts.compFlags |= CLFLG_REGVAR;
        }
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        CompilerIntCalleeTrash(compiler) = SRBM_INT_CALLEE_TRASH_INIT;
        CompilerFloatCalleeTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
        CompilerMaskCalleeTrash(compiler) = SRBM_MSK_CALLEE_TRASH_INIT;
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        codeGen.RegSet.rsClearRegsModified();
        try
        {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE && WINDOWS_AMD64_ABI
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetStructClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* classHandle) => 16;
#endif
}
