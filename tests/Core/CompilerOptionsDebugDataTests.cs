// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerOptionsDebugDataTests
{
    private static readonly string[] SidecarFields =
    [
        "compMinOptsIsUsed",
        "compGcChecks",
        "compEnablePCRelAddr",
        "compProcedureSplittingEH",
        "dspCode",
        "dspEHTable",
        "dspDebugInfo",
        "dspInstrs",
        "dspLines",
        "varNames",
        "disAsmSpilled",
        "disasmWithGC",
        "disAddr",
        "disAsm2",
        "dspOrder",
        "dspUnwind",
        "compLongAddress",
        "dspGCtbls",
        "dspMetrics",
        "compJitAlignLoopForJcc",
        "_dspCode",
        "_dspEHTable",
        "_dspGCtbls",
        "_dspMetrics",
        "_compJitAlignLoopForJcc",
    ];

    [Test]
    public static void DebugOnlyOptionStateIsNotStoredInOptions()
    {
        foreach (var name in SidecarFields)
        {
            Assert.That(
                typeof(Compiler.Options).GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null,
                name);
        }
    }

    [Test]
    public static void DebugCompilerStateIsStoredInCompilerDebugData()
    {
        var compilerFieldNames = new[]
        {
            "compCodeGenDone",
            "compNumStatementLinksTraversed",
            "compSizeEstimate",
            "compCycleEstimate",
            "compPoisoningAnyImplicitByrefs",
            "compGSSecurityCheckBlocker",
            "compActiveStressModes",
            "compGenTreeID",
            "compDebugBreak",
            "_compCyclesAtEndOfInlining",
            "_compCycles",
            "s_compStressModeNames",
            "s_compStressAreaHashes",
            "s_didComponentUnitTests",
        };

        foreach (var name in compilerFieldNames)
        {
            Assert.That(
                typeof(Compiler).GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null,
                name);
        }

        Assert.That(
            typeof(Compiler).GetField(
                "compAllowStress",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            Is.Null);
        Assert.That(
            typeof(Compiler.DebugData).GetField(
                "CompAllowStress",
                BindingFlags.Instance | BindingFlags.NonPublic),
            Is.Not.Null);

        var debugDataFields = new[]
        {
            "CodeGenDone",
            "NumStatementLinksTraversed",
            "SizeEstimate",
            "CycleEstimate",
            "PoisoningAnyImplicitByrefs",
            "GSSecurityCheckBlocker",
            "CompActiveStressModes",
            "CompDebugBreak",
            "CompCyclesAtEndOfInlining",
            "CompCycles",
        };

        foreach (var name in debugDataFields)
        {
            Assert.That(
                typeof(Compiler.DebugData).GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                name);
        }

        Assert.That(
            typeof(Compiler.DebugData).GetField(
                "s_didComponentUnitTests",
                BindingFlags.Static | BindingFlags.NonPublic),
            Is.Not.Null);
        Assert.That(
            typeof(Compiler.DebugData.CompStressData).GetField(
                "ModeNames",
                BindingFlags.Static | BindingFlags.NonPublic),
            Is.Not.Null);
        Assert.That(
            typeof(Compiler.DebugData.CompStressData).GetField(
                "AreaHashes",
                BindingFlags.Static | BindingFlags.NonPublic),
            Is.Not.Null);
        Assert.That(
            typeof(Compiler.DebugData.JitTlsDebugData).GetField(
                "CompGenTreeID",
                BindingFlags.Instance | BindingFlags.NonPublic),
            Is.Not.Null);
    }

    [Test]
    public static void DebugRangeAndDisplayCounterStateIsGroupedInCompilerDebugData()
    {
        var rangeFields = new[]
        {
            "fJitStressRange",
            "fJitRange",
            "s_jitInstrumentIfOptimizingRange",
            "s_jitEnableOsrRange",
            "s_jitEnablePgoRange",
            "s_jitInlineMethodsWithEHRange",
            "s_jitOptRepeatRange",
        };

        foreach (var name in rangeFields)
        {
            Assert.That(
                typeof(Compiler).GetField(
                    name,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null,
                name);
            Assert.That(
                typeof(Compiler.DebugData.MethodRangeData).GetField(
                    name,
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null,
                name);
        }

        Assert.That(
            typeof(Compiler).GetField(
                "jitNestingLevel",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            Is.Null);
        Assert.That(
            typeof(Compiler.DebugData.FunctionTraceData).GetField(
                "jitNestingLevel",
                BindingFlags.Static | BindingFlags.NonPublic),
            Is.Not.Null);

        Assert.That(
            typeof(Compiler).GetField(
                "s_compMethodsCount",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            Is.Null);
        Assert.That(
            typeof(Compiler.DebugData.DebuggerDisplaySequences).GetField(
                "s_compMethodsCount",
                BindingFlags.Static | BindingFlags.NonPublic),
            Is.Not.Null);
    }

    [Test]
    public static void DebugOptionStateRemainsAttachedToItsCompilerAcrossNestedCompilers()
    {
        Assume.That(TestConfiguration.IsDebug);

        using var jitTls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        try
        {
            JitTls.Compiler = compiler;
            compiler.opts.compMinOptsIsSet = true;
            compiler.opts.compMinOpts = true;
            compiler.compAllowStress = false;
            compiler.compGcChecks = true;
            compiler.compEnablePCRelAddr = true;
            compiler.compProcedureSplittingEH = true;
            compiler.dspCode = true;
            compiler.dspEHTable = true;
            compiler.dspDebugInfo = true;
            compiler.dspInstrs = true;
            compiler.dspLines = true;
            compiler.varNames = true;
            compiler.disAsmSpilled = true;
            compiler.disasmWithGC = true;
            compiler.disAddr = true;
            compiler.disAsm2 = true;
            compiler.dspOrder = true;
            compiler.dspUnwind = true;
            compiler.compLongAddress = true;
            compiler.dspGCtbls = true;
            compiler.dspMetrics = true;
            compiler.compJitAlignLoopForJcc = true;

            Assert.That(compiler.MinOpts, Is.True);

            var compilerData = Compiler.DebugData.Get(compiler);
            Assert.Multiple(() => {
                Assert.That(compilerData.CompMinOptsIsUsed, Is.True);
                Assert.That(compilerData.CompAllowStress, Is.False);
                Assert.That(compilerData.CompGcChecks, Is.True);
                Assert.That(compilerData.CompEnablePCRelAddr, Is.True);
                Assert.That(compilerData.CompProcedureSplittingEH, Is.True);
                Assert.That(compilerData.DspCode, Is.True);
                Assert.That(compilerData.DspEHTable, Is.True);
                Assert.That(compilerData.DspDebugInfo, Is.True);
                Assert.That(compilerData.DspInstrs, Is.True);
                Assert.That(compilerData.DspLines, Is.True);
                Assert.That(compilerData.VarNames, Is.True);
                Assert.That(compilerData.DisAsmSpilled, Is.True);
                Assert.That(compilerData.DisasmWithGC, Is.True);
                Assert.That(compilerData.DisAddr, Is.True);
                Assert.That(compilerData.DisAsm2, Is.True);
                Assert.That(compilerData.DspOrder, Is.True);
                Assert.That(compilerData.DspUnwind, Is.True);
                Assert.That(compilerData.CompLongAddress, Is.True);
                Assert.That(compilerData.DspGCtbls, Is.True);
                Assert.That(compilerData.DspMetrics, Is.True);
                Assert.That(compilerData.CompJitAlignLoopForJcc, Is.True);
            });

            compilerData.CompMinOptsIsUsed = false;
            inlinee.opts.compMinOptsIsSet = true;
            inlinee.opts.compMinOpts = false;
            JitTls.Compiler = inlinee;

            Assert.That(inlinee.dspCode, Is.False);
            Assert.That(inlinee.compAllowStress, Is.True);
            Assert.That(inlinee.compGcChecks, Is.False);
            Assert.That(Compiler.DebugData.Get(inlinee).CompMinOptsIsUsed, Is.False);
            Assert.That(compiler.MinOpts, Is.True);
            Assert.That(compilerData.CompMinOptsIsUsed, Is.True);
            Assert.That(Compiler.DebugData.Get(inlinee).CompMinOptsIsUsed, Is.False);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
