// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanReferenceAssociationTests
{
    [TestCase(TYP_INT, SRBM_ALLINT_INIT)]
    [TestCase(TYP_DOUBLE, SRBM_ALLFLOAT_INIT)]
    public static void EmptyCandidatesUseTheIntervalRegisterBank(var_types type, regMask expected)
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, type);
            GenTree tree = type is TYP_DOUBLE
                ? compiler.gtNewDconNode(TYP_DOUBLE, 1)
                : compiler.gtNewIconNode(TYP_INT, 1);

            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_NONE, 2);

            Assert.That(definition.registerAssignment, Is.EqualTo(expected));
            Assert.That(definition.getMultiRegIdx(), Is.EqualTo(2u));
            Assert.That(definition.getInterval(), Is.SameAs(interval));
            Assert.That(interval.firstRefPosition, Is.SameAs(definition));
            Assert.That(interval.recentRefPosition, Is.SameAs(definition));
            Assert.That(interval.lastRefPosition, Is.SameAs(definition));
            Assert.That(interval.isSingleDef, Is.True);
            Assert.That(definition.isFixedRegRef, Is.False);
            Assert.That(definition.regOptional, Is.False);
            Assert.That(allocator.refPositions, Has.Count.EqualTo(1));
        });
    }

    [TestCase(false, 4)]
    [TestCase(true, 3)]
    public static void FixedReferencesPrecedeIntervalReferencesAndSkipInternalUses(bool isInternal, int count)
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            interval.isInternal = isInternal;
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
#if DEBUG
            CurrentBuildNode(allocator) = tree;
#endif
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var use = allocator.newRefPosition(interval, 12, RefType.RefTypeUse, tree, SRBM_RAX);
            var physicalDefinition = allocator.refPositions[0];
            var record = allocator.physRegs[(int)regNumber.REG_RAX];

            Assert.That(allocator.refPositions, Has.Count.EqualTo(count));
            Assert.That(allocator.refPositions[1], Is.SameAs(definition));
            Assert.That(allocator.refPositions[count - 1], Is.SameAs(use));
            Assert.That(physicalDefinition.getReg(), Is.SameAs(record));
            Assert.That(physicalDefinition.refType, Is.EqualTo(RefType.RefTypeFixedReg));
            Assert.That(physicalDefinition.treeNode, Is.Null);
            Assert.That(record.firstRefPosition, Is.SameAs(physicalDefinition));
            Assert.That(record.lastRefPosition, Is.SameAs(isInternal ? physicalDefinition : allocator.refPositions[2]));
            Assert.That(physicalDefinition.nextRefPosition, Is.SameAs(isInternal ? null : allocator.refPositions[2]));
            Assert.That(definition.nextRefPosition, Is.SameAs(use));
            Assert.That(interval.firstRefPosition, Is.SameAs(definition));
            Assert.That(interval.lastRefPosition, Is.SameAs(use));
            Assert.That(interval.recentRefPosition, Is.SameAs(use));
            Assert.That(use.getInterval(), Is.SameAs(interval));
            Assert.That(use.nextRefPosition, Is.Null);
            Assert.That(use.lastUse, Is.True);
            Assert.That(definition.isFixedRegRef, Is.True);
            Assert.That(use.isFixedRegRef, Is.True);
            Assert.That(interval.isSingleDef, Is.True);
#if DEBUG
            Assert.That(physicalDefinition.buildNode, Is.SameAs(tree));
            Assert.That(definition.buildNode, Is.Null);
            Assert.That(definition.rpNum, Is.EqualTo(1u));
            Assert.That(use.rpNum, Is.EqualTo((uint)(count - 1)));
#endif
        });
    }

    [TestCase(regNumber.REG_RBX, SRBM_RBX)]
    [TestCase(regNumber.REG_XMM31, SRBM_XMM31)]
    [TestCase(regNumber.REG_K7, SRBM_K7)]
    public static void PhysicalReferencesRetainCanonicalRecordsAndOrderedLinks(regNumber reg, regMask mask)
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var first = allocator.newRefPosition(reg, 3, RefType.RefTypeFixedReg, null, mask);
            var second = allocator.newRefPosition(reg, 5, RefType.RefTypeFixedReg, null, mask);
            var record = allocator.physRegs[(int)reg];

            Assert.That(first.getReg(), Is.SameAs(record));
            Assert.That(second.getReg(), Is.SameAs(record));
            Assert.That(first.assignedReg(), Is.EqualTo(reg));
            Assert.That(first.nextRefPosition, Is.SameAs(second));
            Assert.That(record.firstRefPosition, Is.SameAs(first));
            Assert.That(record.recentRefPosition, Is.SameAs(second));
            Assert.That(record.lastRefPosition, Is.SameAs(second));
            Assert.That(second.nextRefPosition, Is.Null);
            Assert.That(first.regOptional, Is.False);
            Assert.That(first.getMultiRegIdx(), Is.Zero);
        });
    }

    [TestCase(SRBM_RAX | SRBM_RBX, SRBM_RBX, false, SRBM_RBX, false)]
    [TestCase(SRBM_RAX | SRBM_RBX, SRBM_RBX, true, SRBM_RAX | SRBM_RBX, false)]
    [TestCase(SRBM_RAX | SRBM_RBX | SRBM_RCX, SRBM_RBX | SRBM_RCX, true, SRBM_RBX | SRBM_RCX, false)]
    [TestCase(SRBM_RAX, SRBM_RBX, false, SRBM_RAX, true)]
    public static void TemporaryUsesNarrowDefinitionsWithoutIgnoringInterference(
        regMask defMask, regMask useMask, bool interfering, regMask expectedDefMask, bool conflicting)
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            interval.hasInterferingUses = interfering;
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, defMask);
            var use = allocator.newRefPosition(interval, 12, RefType.RefTypeUse, tree, useMask);

            Assert.That(definition.registerAssignment, Is.EqualTo(expectedDefMask));
            Assert.That(interval.hasConflictingDefUse, Is.EqualTo(conflicting));
            Assert.That(use.lastUse, Is.True);
            Assert.That(definition.nextRefPosition, Is.SameAs(use));
        });
    }

    [Test]
    public static void LocalLastUsesAreClearedOnlyByUsesInTheSameBlock()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewLocalInterval(compiler, allocator);
            CurrentBlockNumber(allocator) = 1;
            var definition = allocator.newRefPosition(interval, 1, RefType.RefTypeDef, null, SRBM_NONE);
            Assert.That(definition.lastUse, Is.True);
            var firstUse = allocator.newRefPosition(interval, 2, RefType.RefTypeUse, null, SRBM_NONE);
            Assert.That(definition.lastUse, Is.False);
            Assert.That(firstUse.lastUse, Is.True);

            CurrentBlockNumber(allocator) = 2;
            var secondUse = allocator.newRefPosition(interval, 3, RefType.RefTypeUse, null, SRBM_NONE);
            Assert.That(firstUse.lastUse, Is.True);
            Assert.That(secondUse.lastUse, Is.True);
            var exposedUse = allocator.newRefPosition(interval, 4, RefType.RefTypeExpUse, null, SRBM_NONE);
            Assert.That(secondUse.lastUse, Is.False);
            Assert.That(exposedUse.lastUse, Is.False);
            var secondDef = allocator.newRefPosition(interval, 5, RefType.RefTypeDummyDef, null, SRBM_NONE);

            Assert.That(secondDef.lastUse, Is.True);
            Assert.That(interval.isSingleDef, Is.False);
            Assert.That(definition.nextRefPosition, Is.SameAs(firstUse));
            Assert.That(firstUse.nextRefPosition, Is.SameAs(secondUse));
            Assert.That(secondUse.nextRefPosition, Is.SameAs(exposedUse));
            Assert.That(exposedUse.nextRefPosition, Is.SameAs(secondDef));
            Assert.That(interval.lastRefPosition, Is.SameAs(secondDef));
        });
    }

    [TestCase(RefType.RefTypeParamDef)]
    [TestCase(RefType.RefTypeZeroInit)]
    public static void EntryDefinitionsDoNotInsertFixedReferencesOrBecomeLastUses(RefType refType)
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewLocalInterval(compiler, allocator);
            var reference = allocator.newRefPosition(interval, 0, refType, null, SRBM_RAX);

            Assert.That(reference.lastUse, Is.False);
            Assert.That(reference.isFixedRegRef, Is.True);
            Assert.That(interval.isSingleDef, Is.True);
            Assert.That(allocator.refPositions, Has.Count.EqualTo(1));
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].firstRefPosition, Is.Null);
        });
    }

    [TestCase(RefType.RefTypeBB, SRBM_NONE)]
    [TestCase(RefType.RefTypeKillGCRefs, SRBM_RAX)]
    [TestCase(RefType.RefTypeKill, SRBM_RAX | SRBM_RBX)]
    public static void UnassociatedReferencesRetainTheirMasksAndHaveNoLinks(RefType type, regMask mask)
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var reference = allocator.newRefPosition(null, 4, type, null, mask);

            Assert.That(reference.referent, Is.Null);
            Assert.That(reference.nextRefPosition, Is.Null);
            Assert.That(reference.registerAssignment, Is.EqualTo(mask));
            Assert.That(allocator.refPositions[0], Is.SameAs(reference));
#if DEBUG
            Assert.That(reference.killedRegisters, Is.EqualTo(
                new regMaskTP(type is RefType.RefTypeKill ? mask : SRBM_NONE)));
#else
            Assert.That(reference.killedRegisters.IsEmpty, Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EditAndContinueDoesNotMergeRegisterPreferences(bool debugEnC)
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewLocalInterval(compiler, allocator);
            var originalPreferences = interval.registerPreferences;
            _ = allocator.newRefPosition(interval, 0, RefType.RefTypeParamDef, null, SRBM_RSI);

            Assert.That(interval.registerPreferences, Is.EqualTo(debugEnC ? originalPreferences : SRBM_RSI));
        }, debugEnC);
    }

#if DEBUG
    [Test]
    public static void StressOptionsPreservePreferencesAndExtendLocalLifetimes()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            StressMask(allocator) = 0x08 | 0x80;
            var interval = NewLocalInterval(compiler, allocator);
            var originalPreferences = interval.registerPreferences;
            var definition = allocator.newRefPosition(interval, 1, RefType.RefTypeDef, null, SRBM_RAX);
            var use = allocator.newRefPosition(interval, 2, RefType.RefTypeUse, null, SRBM_RAX);

            Assert.That(interval.registerPreferences, Is.EqualTo(originalPreferences));
            Assert.That(definition.lastUse, Is.False);
            Assert.That(use.lastUse, Is.False);
        });
    }

    [Test]
    public static void TypedReferenceDiagnosticsRetainNativeOrderingAndActualWeights()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            BlockInfo(allocator) = [new LsraBlockInfo { weight = 3 }];
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            compiler.verbose = true;

            var text = Capture(() => {
                _ = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
                _ = allocator.newRefPosition(null, 12, RefType.RefTypeKill, null, SRBM_RAX);
                _ = allocator.newRefPosition(null, 14, RefType.RefTypeBB, null, SRBM_NONE);
            });

            var expected =
                "<RefPosition #0   @10  RefTypeFixedReg <Reg:rax> BB00 regmask=[rax] minReg=1 wt=3.00>" + Environment.NewLine +
                "<RefPosition #1   @10  RefTypeDef <Ivl:0> CNS_INT BB00 regmask=[rax] minReg=1 fixed wt=12.00>" + Environment.NewLine +
                "<RefPosition #2   @12  RefTypeKill BB00 regmask=[rax] minReg=1 fixed>" + Environment.NewLine +
                "<RefPosition #3   @14  RefTypeBB BB00 regmask=[allMask] minReg=1 wt=3.00>" + Environment.NewLine;
            Assert.That(text, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void RegisterMaskDiagnosticsUseCurrentBanksAndFallbackRegisterNames()
    {
        WithCompiler(compiler => {
            CompilerAllMaskRegs(compiler) = SRBM_K1 | SRBM_K2;
            var text = Capture(() => {
                compiler.dumpRegMask(compiler.SRBM_ALLINT, TYP_INT);
                compiler.dumpRegMask(compiler.SRBM_ALLINT & ~SRBM_FPBASE, TYP_INT);
                compiler.dumpRegMask(compiler.SRBM_ALLFLOAT, TYP_DOUBLE);
                compiler.dumpRegMask(compiler.SRBM_ALLMASK, TYP_MASK);
                compiler.dumpRegMask(SRBM_K7, TYP_MASK);
                compiler.dumpRegMask(new regMaskTP(SRBM_RAX, SRBM_K7));
            });

            Assert.That(text, Is.EqualTo("[allInt][allIntButFP][allFloat][allMask][k7][rax k7]"));
        });
    }

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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBuildNode")]
    private static extern ref GenTree? CurrentBuildNode(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);
#endif

    private static LinearScan CreateAllocator(Compiler compiler)
    {
        var allocator = new LinearScan(compiler);
        for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
        {
            allocator.physRegs[index].init((regNumber)index);
        }

        return allocator;
    }

    private static Interval NewLocalInterval(Compiler compiler, LinearScan allocator)
    {
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTable[0].lvTracked = true;
        compiler.lvaTable[0]._varIndex = 0;
        allocator.localVarIntervals = new Interval?[1];
        var interval = NewInterval(allocator, TYP_INT);
        interval.setLocalNumber(compiler, 0, allocator);

        return interval;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockNumber")]
    private static extern ref uint CurrentBlockNumber(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithCompiler(Action<Compiler> action, bool debugEnC = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.compFloatingPointUsed = true;
        if (debugEnC)
        {
            flags.Set(JitFlags.JIT_FLAG_DEBUG_EnC);
            compiler.opts.compDbgEnC = true;
        }
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
