// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
internal static unsafe class LinearScanAllocationTraversalTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void DefinitionAndLastUsePreserveLocationOrder(bool delayed)
    {
        WithAllocator((compiler, allocator) => {
            _ = BeginBlock(compiler, allocator);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 3);
            var definition = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var use = allocator.newRefPosition(interval, 4, RefType.RefTypeUse, tree, SRBM_RAX);
            use.delayRegFree = delayed;
            AllocateRegisters(allocator);

            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(use.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(use.lastUse, Is.True);
            Assert.That(interval.isActive, Is.False);
            Assert.That(allocator.physRegs[(int)REG_RAX].assignedInterval, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FixedRegisterAndKillPrecedeLaterDefinitions(bool kill)
    {
        WithAllocator((compiler, allocator) => {
            _ = BeginBlock(compiler, allocator);
            if (kill)
            {
                _ = AddKillForRegs(allocator, new regMaskTP(SRBM_RAX), 2);
            }
            else
            {
                _ = allocator.newRefPosition(REG_RAX, 2,
                    RefType.RefTypeFixedReg, null, SRBM_RAX);
            }
            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(
                interval, 4, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 5), SRBM_RAX);

            AllocateRegisters(allocator);
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(interval.physReg, Is.EqualTo(REG_NA));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NextBlockClearsDeadAssignments(bool constant)
    {
        WithAllocator((compiler, allocator) => {
            var blocks = BeginBlocks(compiler, allocator, 2);
            var interval = NewInterval(allocator, TYP_INT);
            interval.isConstant = constant;
            var tree = compiler.gtNewIconNode(TYP_INT, 9);
            _ = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            _ = allocator.newRefPosition(interval, 4, RefType.RefTypeUse, tree, SRBM_RAX);
            CurrentBlockNumber(allocator) = (uint)blocks[1].bbNum;
            _ = allocator.newRefPosition(null, 6, RefType.RefTypeBB, null, SRBM_NONE);

            AllocateRegisters(allocator);
            Assert.That(allocator.physRegs[(int)REG_RAX].assignedInterval, Is.Null);
        });
    }

    [TestCase(RefType.RefTypeParamDef, false, false)]
    [TestCase(RefType.RefTypeZeroInit, false, true)]
    [TestCase(RefType.RefTypeParamDef, true, false)]
    public static void EntryDefinitionsRespectLowReferenceCountAndEhBoundary(
        RefType type, bool ehBoundary, bool expectEntryRegister)
    {
        WithAllocator((compiler, allocator) => {
            var block = BeginBlock(compiler, allocator, addBoundary: false);
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            allocator.localVarIntervals = new Interval?[1];
            EnregisterLocals(allocator) = true;
            LargeVectorVars(allocator) = SetOps.MakeEmpty(compiler);
            BlockInfo(allocator) = new LsraBlockInfo[block.bbNum + 1];
            BlockInfo(allocator)![block.bbNum].hasEHBoundaryIn = ehBoundary;
            var interval = NewInterval(allocator, TYP_INT);
            interval.setLocalNumber(compiler, 0, allocator);
            interval.isWriteThru = ehBoundary;
            var definition = allocator.newRefPosition(interval, 0, type, null, SRBM_RAX);
            _ = allocator.newRefPosition(null, 1, RefType.RefTypeBB, null, SRBM_NONE);
            var use = allocator.newRefPosition(interval, 4, RefType.RefTypeUse,
                compiler.gtNewIconNode(TYP_INT, 3), SRBM_RAX);

            AllocateRegisters(allocator);
            Assert.That(definition.registerAssignment,
                Is.EqualTo(expectEntryRegister ? SRBM_RAX : SRBM_NONE));
            Assert.That(interval.isSpilled, Is.EqualTo(!expectEntryRegister));
            Assert.That(use.reload, Is.EqualTo(!expectEntryRegister));
        });
    }

    [Test]
    public static void MismatchedUseCopiesWithoutChangingThePrimaryRegister()
    {
        WithAllocator((compiler, allocator) => {
            _ = BeginBlock(compiler, allocator);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 7);
            var definition = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var copiedUse = allocator.newRefPosition(interval, 4, RefType.RefTypeUse, tree, SRBM_RBX);
            copiedUse.lastUse = false;
            var finalUse = allocator.newRefPosition(interval, 6, RefType.RefTypeUse, tree, SRBM_RAX);

            AllocateRegisters(allocator);
            Assert.That(definition.registerAssignment, Is.AnyOf(SRBM_RAX, SRBM_RBX));
            Assert.That(copiedUse.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(finalUse.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(copiedUse.moveReg || finalUse.moveReg, Is.True);
            Assert.That(copiedUse.copyReg || finalUse.copyReg, Is.False);
        });
    }

    [Test]
    public static void DisplacedDefinitionSpillsBeforeItsLaterUse()
    {
        WithAllocator((compiler, allocator) => {
            _ = BeginBlock(compiler, allocator);
            var first = NewInterval(allocator, TYP_INT);
            var second = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 7);
            var firstDefinition = allocator.newRefPosition(first, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            _ = allocator.newRefPosition(second, 4, RefType.RefTypeDef, tree, SRBM_RAX);
            var firstUse = allocator.newRefPosition(first, 6, RefType.RefTypeUse, tree, SRBM_RAX);

            AllocateRegisters(allocator);
            Assert.That(firstDefinition.spillAfter, Is.True);
            Assert.That(firstUse.reload, Is.True);
            Assert.That(firstUse.registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void DummyDefinitionStartsTheNextBlockAndRecordsItsIncomingRegister()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTrackedFixed = true;
            compiler.lvaTrackedToVarNum = [0];
            EnregisterLocals(allocator) = true;
            allocator.localVarIntervals = new Interval?[1];
            LargeVectorVars(allocator) = SetOps.MakeEmpty(compiler);
            RegisterCandidateVars(allocator) = SetOps.MakeSingleton(compiler, 0);
            var blocks = BeginBlocks(compiler, allocator, 2);
            blocks[0].bbLiveIn = SetOps.MakeEmpty(compiler);
            blocks[0].bbLiveOut = SetOps.MakeEmpty(compiler);
            blocks[1].bbLiveIn = SetOps.MakeSingleton(compiler, 0);
            blocks[1].bbLiveOut = SetOps.MakeEmpty(compiler);
            BlockInfo(allocator)![blocks[1].bbNum].predBBNum = (uint)blocks[0].bbNum;
            allocator.initVarRegMaps();

            var interval = NewInterval(allocator, TYP_INT);
            interval.setLocalNumber(compiler, 0, allocator);
            CurrentBlockNumber(allocator) = (uint)blocks[1].bbNum;
            var dummy = allocator.newRefPosition(interval, 6, RefType.RefTypeDummyDef, null, SRBM_RAX);
            dummy.setRegOptional(true);
            _ = allocator.newRefPosition(null, 6, RefType.RefTypeBB, null, SRBM_NONE);
            var use = allocator.newRefPosition(interval, 8, RefType.RefTypeUse,
                compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);

            AllocateRegisters(allocator);
            Assert.That(dummy.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(allocator.getInVarToRegMap((uint)blocks[1].bbNum)![0], Is.EqualTo(REG_RAX));
            Assert.That(use.registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }

#if DEBUG
    [Test]
    public static void AllocationDumpPreservesNativeReferenceActionOrder()
    {
        WithAllocator((compiler, allocator) => {
            _ = BeginBlock(compiler, allocator);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 3);
            _ = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            _ = allocator.newRefPosition(interval, 4, RefType.RefTypeUse, tree, SRBM_RAX);
            compiler.verbose = true;

            var output = Capture(() => AllocateRegisters(allocator));
            var before = output.IndexOf("REFPOSITIONS BEFORE ALLOCATION", StringComparison.Ordinal);
            var alloc = output.IndexOf("Allocating Registers", StringComparison.Ordinal);
            var after = output.IndexOf("REFPOSITIONS AFTER ALLOCATION", StringComparison.Ordinal);
            Assert.That(before, Is.GreaterThanOrEqualTo(0));
            Assert.That(alloc, Is.GreaterThan(before));
            Assert.That(output, Does.Contain("Keep"));
            Assert.That(after, Is.GreaterThan(alloc));
        });
    }

    [Test]
    public static void ExposedUseDumpKeepsDefaultRegisterAndDoesNotExtendColumns()
    {
        WithAllocator((compiler, allocator) => {
            _ = BeginBlock(compiler, allocator);
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            allocator.localVarIntervals = new Interval?[1];
            EnregisterLocals(allocator) = true;
            LargeVectorVars(allocator) = SetOps.MakeEmpty(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            interval.setLocalNumber(compiler, 0, allocator);
            var register = allocator.physRegs[(int)REG_R15];
            interval.physReg = REG_R15;
            interval.assignedReg = register;
            register.assignedInterval = interval;
            _ = allocator.newRefPosition(interval, 2, RefType.RefTypeExpUse, null, SRBM_R15);
            compiler.verbose = true;

            var output = Capture(() => AllocateRegisters(allocator));

            Assert.That(output, Does.Contain($"Keep     {REG_NA.Name,-4}"));
            Assert.That(AllocationDumpRegisters(allocator).IsSet(REG_R15), Is.False);
        });
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [Test]
    public static void UnassignedNonlocalUpperVectorSaveDoesNotEmitNoReg()
    {
        WithAllocator((compiler, allocator) => {
            _ = BeginBlock(compiler, allocator);
            var interval = NewInterval(allocator, TYP_SIMD32);
            var save = allocator.newRefPosition(
                interval, 2, RefType.RefTypeUpperVectorSave, null, SRBM_XMM0);
            compiler.verbose = true;

            var output = Capture(() => AllocateRegisters(allocator));

            Assert.That(interval.physReg, Is.EqualTo(REG_NA));
            Assert.That(save.registerAssignment, Is.EqualTo(SRBM_NONE));
            Assert.That(output, Does.Not.Contain("NoReg"));
        });
    }
#endif

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [Test]
    public static void Amd64ProfilerDoesNotPreserveUpperVectorHalves()
    {
        var reference = new RefPosition(1, 4, null, RefType.RefTypeUpperVectorSave);
        var interval = new Interval(TYP_SIMD32, SRBM_XMM0);
        Assert.That(CanSkipUpperVectorSave(null, reference, interval), Is.False);
    }
#endif

    private static BasicBlock BeginBlock(Compiler compiler, LinearScan allocator, bool addBoundary = true)
    {
        var block = BeginBlocks(compiler, allocator, 1, addBoundary)[0];
        return block;
    }

    private static BasicBlock[] BeginBlocks(
        Compiler compiler, LinearScan allocator, int count, bool addBoundary = true)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = count;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgPredsComputed = true;
        SetBlockSequence(allocator);
        CurrentBlockNumber(allocator) = (uint)blocks[0].bbNum;
        if (addBoundary)
        {
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
        }
        return blocks;
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.lvaRefCountState = RCS_NORMAL;
        compiler.compFloatingPointUsed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateRegisters")]
    private static extern void AllocateRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "addKillForRegs")]
    private static extern RefPosition AddKillForRegs(LinearScan allocator, regMaskTP mask, uint location);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setBlockSequence")]
    private static extern void SetBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockNumber")]
    private static extern ref uint CurrentBlockNumber(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocals(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_largeVectorVars")]
    private static extern ref nint[] LargeVectorVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registerCandidateVars")]
    private static extern ref nint[] RegisterCandidateVars(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpRegisters")]
    private static extern ref regMaskTP AllocationDumpRegisters(LinearScan allocator);
#endif

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "canSkipUpperVectorSave")]
    private static extern bool CanSkipUpperVectorSave(
        LinearScan? _, RefPosition reference, Interval interval);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
}
#endif
