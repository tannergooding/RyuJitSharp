// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRegisterKillsAndCopiesTests
{
    [Test]
    public static void KillBuilderLinksInLocationOrderAndMarksFullMasksModified()
    {
        WithAllocator((compiler, allocator) => {
            var lowerMask = new regMaskTP(SRBM_RAX | SRBM_XMM2);
            var upperMask = regMaskTP.CreateFromRegNum(regNumber.REG_K1, SRBM_K1);

            var firstKill = AddKillForRegs(allocator, lowerMask, 10);
            var secondKill = AddKillForRegs(allocator, upperMask, 20);
            var registerSet = ((CodeGen)compiler.codeGen!).RegSet;

            Assert.That(KillHead(allocator), Is.SameAs(firstKill));
            Assert.That(KillTail(allocator), Is.SameAs(secondKill));
            Assert.That(firstKill.nextRefPosition, Is.SameAs(secondKill));
            Assert.That(secondKill.nextRefPosition, Is.Null);
            Assert.That(firstKill.killedRegisters, Is.EqualTo(lowerMask));
            Assert.That(secondKill.killedRegisters, Is.EqualTo(upperMask));
            Assert.That(firstKill.registerAssignment, Is.EqualTo(lowerMask.Lower));
            Assert.That(secondKill.registerAssignment, Is.EqualTo(SRBM_NONE));
            Assert.That(registerSet.rsRegsModified(lowerMask), Is.True);
            Assert.That(registerSet.rsRegsModified(upperMask), Is.True);
        });
    }

    [Test]
    public static void KilledRegisterSpillsItsLiveIntervalAndAdvancesFixedReference()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var killedUse = allocator.newRefPosition(interval, 15, RefType.RefTypeUse, tree, SRBM_RAX);
            _ = allocator.newRefPosition(interval, 30, RefType.RefTypeUse, tree, SRBM_RAX);
            interval.recentRefPosition = killedUse;
            killedUse.lastUse = false;
            var register = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, register, interval);

            var kill = allocator.newRefPosition(null, 20, RefType.RefTypeKill, null, SRBM_RAX);
            kill.killedRegisters = new regMaskTP(SRBM_RAX);
            SetCurrentFixedReferenceBeforeKill(allocator, register, regNumber.REG_RAX, kill);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);
            allocator.setCurrentBlockStartLocation(0);
            CurrentAllocationLocation(allocator) = kill.nodeLocation;
#if DEBUG
            compiler.verbose = true;
#endif

            var output = Capture(() => ProcessKills(allocator, kill));

            Assert.That(interval.isSpilled, Is.True);
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(killedUse.spillAfter, Is.True);
            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(NextFixedRef(allocator)[(int)regNumber.REG_RAX], Is.EqualTo(30u));
            Assert.That(BusyUntilKill(allocator).IsSet(regNumber.REG_RAX), Is.False);
            Assert.That(GetFreeCandidates(allocator, SRBM_RAX, TYP_INT), Is.EqualTo(SRBM_RAX));
#if DEBUG
            Assert.That(output, Does.Contain("None     "));
            Assert.That(output, Does.Contain("RAX"));
#endif
        });
    }

    [Test]
    public static void KillsProcessHighRegisterMaskBankAndFixedReferences()
    {
        WithAllocator((compiler, allocator) => {
            var register = allocator.physRegs[(int)regNumber.REG_K1];
            var firstFixed = allocator.newRefPosition(
                regNumber.REG_K1, 10, RefType.RefTypeFixedReg, null, SRBM_K1);
            _ = allocator.newRefPosition(regNumber.REG_K1, 30, RefType.RefTypeFixedReg, null, SRBM_K1);
            var kill = allocator.newRefPosition(null, 20, RefType.RefTypeKill, null, SRBM_K1);
            kill.killedRegisters = regMaskTP.CreateFromRegNum(regNumber.REG_K1, SRBM_K1);
            register.recentRefPosition = firstFixed;
            UpdateNextFixedRef(allocator, register, firstFixed.nextRefPosition, kill);
            BusyUntilKill(allocator) = regMaskTP.CreateFromRegNum(regNumber.REG_K1, SRBM_K1);

            ProcessKills(allocator, kill);

            Assert.That(NextFixedRef(allocator)[(int)regNumber.REG_K1], Is.EqualTo(30u));
            Assert.That(BusyUntilKill(allocator).IsSet(regNumber.REG_K1), Is.False);
        });
    }

    [Test]
    public static void GcKillSpillsOnlyGcIntervalsInItsCandidateSet()
    {
        WithAllocator((compiler, allocator) => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var gcInterval = NewInterval(allocator, TYP_REF);
            var gcTree = compiler.gtNewIconNode(TYP_INT, 2);
            _ = allocator.newRefPosition(gcInterval, 5, RefType.RefTypeDef, gcTree, SRBM_RAX);
            var gcUse = allocator.newRefPosition(gcInterval, 10, RefType.RefTypeUse, gcTree, SRBM_RAX);
            _ = allocator.newRefPosition(gcInterval, 30, RefType.RefTypeUse, gcTree, SRBM_RAX);
            gcInterval.recentRefPosition = gcUse;
            gcUse.lastUse = false;
            var gcRegister = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, gcRegister, gcInterval);

            var integerInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(integerInterval, 5, RefType.RefTypeDef, tree, SRBM_RBX);
            var integerUse = allocator.newRefPosition(integerInterval, 10, RefType.RefTypeUse, tree, SRBM_RBX);
            _ = allocator.newRefPosition(integerInterval, 30, RefType.RefTypeUse, tree, SRBM_RBX);
            integerInterval.recentRefPosition = integerUse;
            var integerRegister = allocator.physRegs[(int)regNumber.REG_RBX];
            AssignPhysReg(allocator, integerRegister, integerInterval);

            var kill = allocator.newRefPosition(null, 20, RefType.RefTypeKillGCRefs, null, SRBM_RAX | SRBM_RBX);
            allocator.setCurrentBlockStartLocation(0);
            CurrentAllocationLocation(allocator) = kill.nodeLocation;
#if DEBUG
            compiler.verbose = true;
#endif

            var output = Capture(() => SpillGCRefs(allocator, kill));

            Assert.That(gcInterval.isSpilled, Is.True);
            Assert.That(gcInterval.isActive, Is.False);
            Assert.That(gcInterval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(gcUse.spillAfter, Is.True);
            Assert.That(gcRegister.assignedInterval, Is.Null);
            Assert.That(integerInterval.isSpilled, Is.False);
            Assert.That(integerInterval.isActive, Is.True);
            Assert.That(integerInterval.physReg, Is.EqualTo(regNumber.REG_RBX));
            Assert.That(integerRegister.assignedInterval, Is.SameAs(integerInterval));
#if DEBUG
            Assert.That(output, Does.Contain("Done          "));
#endif
        });
    }

    [Test]
    public static void GcKillPreservesNativeNoGcDiagnosticWhenCandidatesAreNonGc()
    {
        WithAllocator((compiler, allocator) => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var interval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(interval, 5, RefType.RefTypeDef, tree, SRBM_RBX);
            _ = allocator.newRefPosition(interval, 10, RefType.RefTypeUse, tree, SRBM_RBX);
            var register = allocator.physRegs[(int)regNumber.REG_RBX];
            AssignPhysReg(allocator, register, interval);
            var kill = allocator.newRefPosition(null, 20, RefType.RefTypeKillGCRefs, null, SRBM_RBX);
            CurrentAllocationLocation(allocator) = kill.nodeLocation;
#if DEBUG
            compiler.verbose = true;
#endif

            var output = Capture(() => SpillGCRefs(allocator, kill));

            Assert.That(register.assignedInterval, Is.SameAs(interval));
            Assert.That(interval.isActive, Is.True);
#if DEBUG
            Assert.That(output, Does.Contain("None          "));
#else
            Assert.That(output, Is.Empty);
#endif
        });
    }

    [Test]
    public static void GcKillSpillsIntegerLocalWhoseRecentTreeCarriesGcType()
    {
        WithAllocator((compiler, allocator) => {
            var gcTree = compiler.gtNewLclvNode(TYP_REF, 0);
            var interval = NewInterval(allocator, TYP_INT);
            interval.isLocalVar = true;
            interval.varNum = 0;
            _ = allocator.newRefPosition(interval, 5, RefType.RefTypeDef, gcTree, SRBM_RAX);
            var recentUse = allocator.newRefPosition(interval, 10, RefType.RefTypeUse, gcTree, SRBM_RAX);
            _ = allocator.newRefPosition(interval, 30, RefType.RefTypeUse, gcTree, SRBM_RAX);
            interval.recentRefPosition = recentUse;
            var register = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, register, interval);
            var kill = allocator.newRefPosition(null, 20, RefType.RefTypeKillGCRefs, null, SRBM_RAX);
            allocator.setCurrentBlockStartLocation(0);
            CurrentAllocationLocation(allocator) = kill.nodeLocation;

            SpillGCRefs(allocator, kill);

            Assert.That(interval.isSpilled, Is.True);
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(recentUse.spillAfter, Is.True);
            Assert.That(register.assignedInterval, Is.Null);
        }, trackedLocal: true);
    }

    [Test]
    public static void CopyRegisterAllocationRetainsPrimaryAssignmentAndEmitsNativeEvent()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var copyUse = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, tree, SRBM_RBX);
            interval.recentRefPosition = definition;
            var relatedInterval = NewInterval(allocator, TYP_INT);
            interval.relatedInterval = relatedInterval;
            var primaryRegister = allocator.physRegs[(int)regNumber.REG_RAX];
            var copyRegister = allocator.physRegs[(int)regNumber.REG_RBX];
            AssignPhysReg(allocator, primaryRegister, interval);
#if DEBUG
            compiler.verbose = true;
#endif

            var output = Capture(() => {
                var result = AssignCopyRegMinimal(allocator, copyUse);
                Assert.That(result, Is.EqualTo(regNumber.REG_RBX));
            });

            Assert.That(copyUse.copyReg, Is.True);
            Assert.That(copyUse.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(interval.isActive, Is.True);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_RAX));
            Assert.That(interval.assignedReg, Is.SameAs(primaryRegister));
            Assert.That(interval.relatedInterval, Is.SameAs(relatedInterval));
            Assert.That(primaryRegister.assignedInterval, Is.SameAs(interval));
            Assert.That(copyRegister.assignedInterval, Is.SameAs(interval));
#if DEBUG
            Assert.That(output, Does.Contain("Copy     RBX "));
#else
            Assert.That(output, Is.Empty);
#endif
        });
    }

    [Test]
    public static void CopyRegisterAllocationPrintsNonzeroSelectionScore()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var copyUse = allocator.newRefPosition(
                interval, 20, RefType.RefTypeUse, tree, SRBM_RBX | SRBM_RCX);
            interval.recentRefPosition = definition;
            var primaryRegister = allocator.physRegs[(int)regNumber.REG_RAX];
            allocator.physRegs[(int)regNumber.REG_RBX].regOrder = 0;
            allocator.physRegs[(int)regNumber.REG_RCX].regOrder = 1;
            AssignPhysReg(allocator, primaryRegister, interval);
#if DEBUG
            compiler.verbose = true;
#endif

            var output = Capture(() => {
                var result = AssignCopyRegMinimal(allocator, copyUse);
                Assert.That(result, Is.EqualTo(regNumber.REG_RBX));
            });

#if DEBUG
            Assert.That(output, Does.Contain("ORDER(C) RBX "));
            Assert.That(output, Does.Not.Contain("Copy     RBX "));
#else
            Assert.That(output, Is.Empty);
#endif
        });
    }

    [Test]
    [Platform("Win")]
    public static void KillingUpperVectorSpillsRelatedLocalWithNativeSpillWeight()
    {
        WithAllocator((compiler, allocator) => {
            var localInterval = NewInterval(allocator, TYP_FLOAT);
            localInterval.isLocalVar = true;
            localInterval.varNum = 0;
            var localTree = compiler.gtNewLclvNode(TYP_INT, 0);
            _ = allocator.newRefPosition(localInterval, 5, RefType.RefTypeDef, localTree, SRBM_XMM6);
            var localUse = allocator.newRefPosition(localInterval, 9, RefType.RefTypeUse, localTree, SRBM_XMM6);
            localInterval.recentRefPosition = localUse;
            var localRegister = allocator.physRegs[(int)regNumber.REG_XMM6];
            AssignPhysReg(allocator, localRegister, localInterval);

            var upperInterval = NewInterval(allocator, TYP_FLOAT);
            IsUpperVector(upperInterval) = true;
            upperInterval.relatedInterval = localInterval;
            var vectorTree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(upperInterval, 10, RefType.RefTypeDef, vectorTree, SRBM_XMM8);
            var upperUse = allocator.newRefPosition(upperInterval, 15, RefType.RefTypeUse, vectorTree, SRBM_XMM8);
            _ = allocator.newRefPosition(upperInterval, 30, RefType.RefTypeUse, vectorTree, SRBM_XMM8);
            upperInterval.recentRefPosition = upperUse;
            upperUse.lastUse = false;
            var upperRegister = allocator.physRegs[(int)regNumber.REG_XMM8];
            AssignPhysReg(allocator, upperRegister, upperInterval);

            var kill = allocator.newRefPosition(null, 20, RefType.RefTypeKill, null, SRBM_XMM8);
            kill.killedRegisters = new regMaskTP(SRBM_XMM8);
            SetCurrentFixedReferenceBeforeKill(allocator, upperRegister, regNumber.REG_XMM8, kill);
            allocator.setCurrentBlockStartLocation(0);
            CurrentAllocationLocation(allocator) = kill.nodeLocation;
            var expectedSpillWeight = GetSpillWeight(allocator, localRegister);

            ProcessKills(allocator, kill);

            Assert.That(upperInterval.isSpilled, Is.True);
            Assert.That(upperInterval.isActive, Is.False);
            Assert.That(upperUse.spillAfter, Is.True);
            Assert.That(upperRegister.assignedInterval, Is.Null);
            Assert.That(localInterval.isSpilled, Is.True);
            Assert.That(localInterval.isActive, Is.True);
            Assert.That(localRegister.assignedInterval, Is.SameAs(localInterval));
            Assert.That(SpillCost(allocator)[(int)regNumber.REG_XMM6], Is.EqualTo(expectedSpillWeight));
            Assert.That(
                BitSetOps<Compiler, TrackedVarBitSetTraits>.IsMember(compiler, SplitOrSpilledVars(allocator)!, 0),
                Is.True);
        }, trackedLocal: true);
    }

    private static void SetCurrentFixedReferenceBeforeKill(
        LinearScan allocator, RegRecord regRecord, regNumber register, RefPosition kill)
    {
        var reference = regRecord.firstRefPosition
            ?? throw new InvalidOperationException("Killed-register tests require fixed-register references.");
        while ((reference.nextRefPosition is not null) &&
               (reference.nextRefPosition.nodeLocation < kill.nodeLocation))
        {
            reference = reference.nextRefPosition;
        }

        regRecord.recentRefPosition = reference;
        UpdateNextFixedRef(allocator, regRecord, reference.nextRefPosition, kill);
        Assert.That(NextFixedRef(allocator)[(int)register], Is.EqualTo(kill.nodeLocation));
    }

    private static Interval NewInterval(LinearScan allocator, var_types type) => NewIntervalCore(allocator, type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewIntervalCore(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void AssignPhysReg(LinearScan allocator, RegRecord register, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "processKills")]
    private static extern void ProcessKills(LinearScan allocator, RefPosition killRefPosition);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "addKillForRegs")]
    private static extern RefPosition AddKillForRegs(
        LinearScan allocator, regMaskTP registers, uint location);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "spillGCRefs")]
    private static extern void SpillGCRefs(LinearScan allocator, RefPosition killRefPosition);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignCopyRegMinimal")]
    private static extern regNumber AssignCopyRegMinimal(LinearScan allocator, RefPosition refPosition);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "updateNextFixedRef")]
    private static extern void UpdateNextFixedRef(
        LinearScan allocator, RegRecord regRecord, RefPosition? nextRefPosition, RefPosition? nextKill);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getSpillWeight")]
    private static extern double GetSpillWeight(LinearScan allocator, RegRecord regRecord);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getFreeCandidates")]
    private static extern regMask GetFreeCandidates(
        LinearScan allocator, regMask candidates, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_killHead")]
    private static extern ref RefPosition? KillHead(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_killTail")]
    private static extern ref RefPosition? KillTail(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nextFixedRef")]
    private static extern ref uint[] NextFixedRef(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentAllocationLocation")]
    private static extern ref uint CurrentAllocationLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_splitOrSpilledVars")]
    private static extern ref nint[]? SplitOrSpilledVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_spillCost")]
    private static extern ref double[] SpillCost(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "isUpperVector")]
    private static extern ref bool IsUpperVector(Interval interval);

#if DEBUG
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
#else
    private static string Capture(Action action)
    {
        action();
        return string.Empty;
    }
#endif

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool trackedLocal = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT | (trackedLocal ? CLFLG_REGVAR : 0);
        compiler.compFloatingPointUsed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        if (trackedLocal)
        {
            compiler.fgBBNumMax = 2;
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTrackedFixed = true;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true }];
        }

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
            for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
            {
                var register = allocator.physRegs[index];
                register.init((regNumber)index);
                register.regOrder = (byte)index;
            }

            if (trackedLocal)
            {
                var blocks = new LsraBlockInfo[compiler.fgBBNumMax + 1];
                Array.Fill(blocks, new LsraBlockInfo { weight = 1 });
                BlockInfo(allocator) = blocks;
                BbNumMaxBeforeResolution(allocator) = (uint)compiler.fgBBNumMax;
                allocator.initVarRegMaps();
            }
            else
            {
                BlockInfo(allocator) = [new LsraBlockInfo { weight = 1 }];
            }

            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);
}
