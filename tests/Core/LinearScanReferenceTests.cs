// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanReferenceTests
{
    [TestCase(SRBM_RAX | SRBM_RBX, SRBM_RBX | SRBM_RCX, SRBM_NONE, false, SRBM_RBX)]
    [TestCase(SRBM_RAX, SRBM_RBX, SRBM_RBX, false, SRBM_RAX)]
    [TestCase(SRBM_RAX, SRBM_RBX | SRBM_RCX, SRBM_NONE, false, SRBM_RBX | SRBM_RCX)]
    [TestCase(SRBM_RAX | SRBM_RCX, SRBM_RBX, SRBM_NONE, false, SRBM_RAX | SRBM_RCX)]
    [TestCase(SRBM_RAX, SRBM_RBX, SRBM_NONE, false, SRBM_RAX | SRBM_RBX)]
    [TestCase(SRBM_RAX, SRBM_RBX, SRBM_NONE, true, SRBM_RBX)]
    public static void PreferencesPreserveAversionAndKillSetSemantics(
        regMask original, regMask incoming, regMask aversion, bool preferCalleeSave, regMask expected)
    {
        var interval = new Interval(TYP_INT, original) {
            registerAversion = aversion,
            preferCalleeSave = preferCalleeSave,
        };

        interval.mergeRegisterPreferences(incoming);

        Assert.That(interval.registerPreferences, Is.EqualTo(expected));
    }

    [TestCase(regNumber.REG_RBX, TYP_INT, true)]
    [TestCase(regNumber.REG_XMM31, TYP_FLOAT, false)]
    [TestCase(regNumber.REG_K7, TYP_MASK, false)]
    public static void PhysicalReferencesPreserveRegisterBanks(regNumber reg, var_types type, bool calleeSave)
    {
        var record = new RegRecord();
        record.init(reg);
        var reference = new RefPosition(1, 2, null, RefType.RefTypeFixedReg);
        reference.setReg(record);

        Assert.That(record.registerType, Is.EqualTo(type));
        Assert.That(record.isCalleeSave, Is.EqualTo(calleeSave));
        Assert.That(reference.getReg(), Is.SameAs(record));
        Assert.That(reference.assignedReg(), Is.EqualTo(reg));
        Assert.That(reference.isFixedRefOfReg(reg), Is.True);
        Assert.That(reference.isIntervalRef(), Is.False);
    }

    [TestCase(false, false, 16u)]
    [TestCase(true, false, 10u)]
    [TestCase(false, true, 10u)]
    public static void ReferenceRangesRespectLastUseSpillsAndDelayedFree(bool lastUse, bool spillAfter, uint end)
    {
        var interval = new Interval(TYP_INT, SRBM_RAX);
        var use = new RefPosition(1, 15, null, RefType.RefTypeUse) { delayRegFree = true };
        var definition = new RefPosition(1, 10, null, RefType.RefTypeDef) {
            nextRefPosition = use,
            lastUse = lastUse,
            spillAfter = spillAfter,
        };
        definition.setInterval(interval);
        use.setInterval(interval);
        interval.firstRefPosition = definition;
        interval.lastRefPosition = use;

        Assert.That(definition.getRangeEndLocation(), Is.EqualTo(end));
        Assert.That(interval.getNextRefPosition(), Is.SameAs(definition));
        interval.recentRefPosition = definition;
        Assert.That(interval.getNextRefPosition(), Is.SameAs(use));
        interval.recentRefPosition = use;
        Assert.That(interval.getNextRefLocation(), Is.EqualTo(MaxLocation));
    }

    [Test]
    public static void LocalIntervalsUseTrackedIndicesRatherThanLocalNumbers()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 2;
            compiler.lvaTable[0].lvTracked = true;
            compiler.lvaTable[0]._varIndex = 1;
            var allocator = new LinearScan(compiler) { localVarIntervals = new Interval?[2] };
            var interval = new Interval(TYP_INT, SRBM_RAX);

            interval.setLocalNumber(compiler, 0, allocator);

            Assert.That(allocator.localVarIntervals[0], Is.Null);
            Assert.That(allocator.localVarIntervals[1], Is.SameAs(interval));
            Assert.That(interval.getVarIndex(compiler), Is.EqualTo(1u));
            Assert.That(Unsafe.AreSame(ref interval.getLocalVar(compiler), ref compiler.lvaTable[0]), Is.True);
        });
    }

    [Test]
    public static void RawReferencesRetainIdentityAndConsumeTheBuildNodeOnce()
    {
        WithCompiler(compiler => {
            var allocator = new LinearScan(compiler);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
#if DEBUG
            CurrentBuildNode(allocator) = tree;
#endif
            var definition = allocator.newRefPositionRaw(10, tree, RefType.RefTypeDef);
            var use = allocator.newRefPositionRaw(12, tree, RefType.RefTypeUse);

            Assert.That(allocator.refPositions[0], Is.SameAs(definition));
            Assert.That(allocator.refPositions[1], Is.SameAs(use));
            Assert.That(definition.assignedReg(), Is.EqualTo(regNumber.REG_NA));
            Assert.That(use.treeNode, Is.SameAs(tree));
#if DEBUG
            Assert.That(definition.rpNum, Is.EqualTo(0u));
            Assert.That(use.rpNum, Is.EqualTo(1u));
            Assert.That(definition.buildNode, Is.SameAs(tree));
            Assert.That(use.buildNode, Is.Null);
#endif
        });
    }

#if DEBUG
    [Test]
    public static void RelatedIntervalsPreserveNativeDiagnosticText()
    {
        WithCompiler(compiler => {
            compiler.verbose = true;
            var local = new Interval(TYP_INT, SRBM_RAX) { isLocalVar = true, varNum = 7, intervalIndex = 2 };
            var temporary = new Interval(TYP_INT, SRBM_RBX) { isInternal = true, intervalIndex = 3 };
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                temporary.assignRelatedInterval(local);
                Assert.That(temporary.assignRelatedIntervalIfUnassigned(temporary), Is.False);
                temporary.tinyDump();
                local.tinyDump();
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }
            var expected = "Assigning related <V07/L2> to <T3>" + Environment.NewLine +
                "Interval <T3> already has a related interval" + Environment.NewLine +
                "<Ivl:3 internal> <Ivl:2 V07> ";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
            Assert.That(temporary.relatedInterval, Is.SameAs(local));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBuildNode")]
    private static extern ref GenTree? CurrentBuildNode(LinearScan allocator);
#endif

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
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
