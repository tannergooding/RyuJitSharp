// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
#if DEBUG
using System.IO;
using System.Text;
#endif
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanReferenceBuildingTests
{
#if DEBUG
    [Test]
    [NonParallelizable]
    public static void IntervalCreationDumpsBeforeAnyReferencesAreAttached()
    {
        WithAllocator((compiler, allocator) => {
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                compiler.verbose = true;
                s_jitstdout = writer;
                _ = NewInterval(allocator, TYP_INT);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                Does.StartWith("Interval  0: int RefPositions {} physReg:"));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);
#endif

    [Test]
    public static void DefinitionsAndUsesShareTemporaryIntervalAndPreserveTargetPreferences()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_INT, 123);
            ReferenceBuildLocation(allocator) = 10;
            var definition = BuildDef(allocator, source, SRBM_RAX | SRBM_RBX, 0);

            Assert.That(definition.refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(definition.nodeLocation, Is.EqualTo(11));
            Assert.That(definition.treeNode, Is.SameAs(source));
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX | SRBM_RBX));
            Assert.That(allocator.intervals, Has.Count.EqualTo(1));

            ReferenceBuildLocation(allocator) = 12;
            var use = BuildUse(allocator, source, SRBM_RCX, 0);

            Assert.That(use.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(use.nodeLocation, Is.EqualTo(12));
            Assert.That(use.treeNode, Is.Null);
            Assert.That(use.getInterval(), Is.SameAs(definition.getInterval()));
            Assert.That(use.registerAssignment, Is.EqualTo(SRBM_RCX));
            Assert.That(definition.nextRefPosition, Is.SameAs(use));
            TargetPreferredUse(allocator) = use;
            TargetPreferredUse2(allocator) = use;
            TargetPreferredUse3(allocator) = use;
            ReferenceBuildLocation(allocator) = 14;
            var consumer = compiler.gtNewIconNode(TYP_INT, 456);
            var consumerDefinition = BuildDef(allocator, consumer, SRBM_RDX | SRBM_RSI, 0);

            Assert.That(use.getInterval().relatedInterval, Is.SameAs(consumerDefinition.getInterval()));
        });
    }

    [Test]
    public static void DelayedUseMarksTheFollowingDefinitionAsInterfering()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_INT, 123);
            ReferenceBuildLocation(allocator) = 10;
            var definition = BuildDef(allocator, source, SRBM_RAX | SRBM_RBX, 0);

            ReferenceBuildLocation(allocator) = 12;
            var use = BuildUse(allocator, source, SRBM_NONE, 0);
            SetDelayFree(allocator, use);

            ReferenceBuildLocation(allocator) = 14;
            var nextDefinition = BuildDef(allocator, compiler.gtNewIconNode(TYP_INT, 456), SRBM_RCX | SRBM_RDX, 0);

            Assert.That(use.delayRegFree, Is.True);
            Assert.That(nextDefinition.getInterval().hasInterferingUses, Is.True);
            Assert.That(definition.getInterval().hasInterferingUses, Is.False);
        });
    }

    [Test]
    public static void DefinitionListMatchesMultiRegisterReferencesByIndexAndMaintainsOrder()
    {
        WithAllocator((compiler, _) => {
            var definitions = new RefInfoList();
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var first = new RefPosition(0, 1, tree, RefType.RefTypeDef);
            var second = new RefPosition(0, 1, tree, RefType.RefTypeDef);
            first.setMultiRegIdx(0);
            second.setMultiRegIdx(1);
            var firstNode = new RefInfoListNode { refPosition = first, treeNode = tree };
            var secondNode = new RefInfoListNode { refPosition = second, treeNode = tree };

            definitions.Append(firstNode);
            definitions.Append(secondNode);

            Assert.That(definitions.RemoveListNode(tree, 1), Is.SameAs(secondNode));
            Assert.That(definitions.First, Is.SameAs(firstNode));
            Assert.That(firstNode.next, Is.Null);
            Assert.That(definitions.RemoveListNode(tree, 0), Is.SameAs(firstNode));
            Assert.That(definitions.First, Is.Null);
        });
    }

    [Test]
    public static void LastUseOfCandidateLocalRemovesItFromLiveSetAndBuildsLocalUse()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [
                new() { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;

            var localInterval = new Interval(TYP_INT, SRBM_RAX | SRBM_RBX);
            allocator.localVarIntervals = [localInterval];
            localInterval.setLocalNumber(compiler, 0, allocator);
            var definition = new RefPosition(0, 1, null, RefType.RefTypeDef);
            definition.setInterval(localInterval);
            localInterval.firstRefPosition = definition;
            localInterval.recentRefPosition = definition;
            localInterval.lastRefPosition = definition;

            LiveVariables(allocator) = [1];
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.Flags |= GTF_VAR_DEATH;
            ReferenceBuildLocation(allocator) = 4;
            var use = BuildUse(allocator, local, SRBM_RAX | SRBM_RBX, 0);

            Assert.That(LiveVariables(allocator)[0] & 1, Is.EqualTo((nint)0));
            Assert.That(use.getInterval(), Is.SameAs(localInterval));
            Assert.That(use.treeNode, Is.SameAs(local));
            Assert.That(use.lastUse, Is.True);
            Assert.That(definition.nextRefPosition, Is.SameAs(use));
        });
    }

    [Test]
    public static void UpperVectorRestoreForALiveLocalIsCreatedAsRegisterOptional()
    {
        WithAllocator((compiler, allocator) => {
            compiler.compFloatingPointUsed = true;
            compiler.lvaTable = [
                new() { Type = TYP_SIMD32, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;

            var localInterval = new Interval(TYP_SIMD32, SRBM_ALLFLOAT_INIT);
            allocator.localVarIntervals = [localInterval];
            localInterval.setLocalNumber(compiler, 0, allocator);
            localInterval.isPartiallySpilled = true;

            var upperInterval = new Interval(TYP_SIMD16, SRBM_ALLFLOAT_INIT) {
                isUpperVector = true,
                relatedInterval = localInterval,
            };
            allocator.intervals.Add(localInterval);
            allocator.intervals.Add(upperInterval);
            var save = allocator.newRefPosition(upperInterval, 2, RefType.RefTypeUpperVectorSave, null,
                SRBM_FLT_CALLEE_SAVED);
            save.liveVarUpperSave = true;

            var node = compiler.gtNewLclvNode(TYP_SIMD32, 0);
            BuildUpperVectorRestoreRefPosition(allocator, localInterval, 4, node, true, 0);

            var restore = upperInterval.lastRefPosition
                ?? throw new AssertionException("The upper-vector restore reference was not created.");
            Assert.That(restore.refType, Is.EqualTo(RefType.RefTypeUpperVectorRestore));
            Assert.That(restore.regOptional, Is.True);
            Assert.That(restore.getMultiRegIdx(), Is.Zero);
            Assert.That(restore.treeNode, Is.SameAs(node));
            Assert.That(save.nextRefPosition, Is.SameAs(restore));
            Assert.That(save.skipSaveRestore, Is.False);
            Assert.That(save.liveVarUpperSave, Is.True);
            Assert.That(localInterval.isPartiallySpilled, Is.False);
        });
    }

    [Test]
    public static void RegisterTypeByIndexMapsScalarLongLocalsToIntegerRegisterType()
    {
        WithAllocator((compiler, allocator) => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);

            Assert.That(GetRegisterTypeByIndex(allocator, local, 0), Is.EqualTo(TYP_INT));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildUse")]
    private static extern RefPosition BuildUse(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getRegisterTypeByIndex")]
    private static extern var_types GetRegisterTypeByIndex(LinearScan allocator, GenTree tree, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setDelayFree")]
    private static extern void SetDelayFree(LinearScan allocator, RefPosition use);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildUpperVectorRestoreRefPosition")]
    private static extern void BuildUpperVectorRestoreRefPosition(
        LinearScan allocator, Interval localInterval, uint location, GenTree? node, bool isUse, uint multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentLiveVariables")]
    private static extern ref nint[] LiveVariables(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse")]
    private static extern ref RefPosition? TargetPreferredUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse2")]
    private static extern ref RefPosition? TargetPreferredUse2(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse3")]
    private static extern ref RefPosition? TargetPreferredUse3(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

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
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
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
}
