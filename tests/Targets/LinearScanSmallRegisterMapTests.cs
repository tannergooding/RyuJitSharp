// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanSmallRegisterMapTests
{
    [TestCase(1, 4)]
    [TestCase(4, 4)]
    [TestCase(5, 8)]
    public static void VariableMapsUseOneBytePerRoundedEntryWithoutNarrowingRegisterIdentities(
        int trackedCount, int mapCount)
    {
        WithMaps(trackedCount, (_, allocator) =>
        {
            Assert.That(Unsafe.SizeOf<regNumber>(), Is.EqualTo(sizeof(uint)));
            Assert.That(RegMapCount(allocator), Is.EqualTo((uint)mapCount));
            var inMaps = InMaps(allocator) ?? throw new AssertionException("Missing incoming maps.");
            var outMaps = OutMaps(allocator) ?? throw new AssertionException("Missing outgoing maps.");
            for (var blockNumber = 0; blockNumber < inMaps.Length; blockNumber++)
            {
                var inMap = inMaps[blockNumber] ?? throw new AssertionException("Missing incoming map.");
                var outMap = outMaps[blockNumber] ?? throw new AssertionException("Missing outgoing map.");
                Assert.That(Buffer.ByteLength(inMap), Is.EqualTo(mapCount));
                Assert.That(Buffer.ByteLength(outMap), Is.EqualTo(mapCount));
                Assert.That(inMap, Is.All.EqualTo((byte)REG_STK));
                Assert.That(outMap, Is.All.EqualTo((byte)REG_STK));
            }

            var scratch = ScratchMap(allocator) ?? throw new AssertionException("Missing scratch map.");
            Assert.That(Buffer.ByteLength(scratch), Is.EqualTo(mapCount));
        });
    }

    [TestCase(0u)]
    [TestCase(127u)]
    [TestCase(128u)]
    [TestCase(254u)]
    public static void LocalNumberSettersStoreBytesAtTrackedIndices(uint value)
    {
        WithMaps(3, (compiler, allocator) =>
        {
            compiler.lvaTable[0]._varIndex = 2;
            var register = (regNumber)value;

            allocator.setInVarRegForBB(1, 0, register);
            allocator.setOutVarRegForBB(2, 0, register);

            var inMap = allocator.getInVarToRegMap(1) ?? throw new AssertionException("Missing incoming map.");
            var outMap = allocator.getOutVarToRegMap(2) ?? throw new AssertionException("Missing outgoing map.");
            Assert.That(inMap[2], Is.EqualTo((byte)value));
            Assert.That(outMap[2], Is.EqualTo((byte)value));
            Assert.That(allocator.getVarReg(inMap, 2), Is.EqualTo(register));
            Assert.That(allocator.getVarReg(outMap, 2), Is.EqualTo(register));
            Assert.That(inMap[0], Is.EqualTo((byte)REG_STK));
            Assert.That(outMap[0], Is.EqualTo((byte)REG_STK));
        });
    }

    [Test]
    public static void TrackedAccessorsZeroExtendEveryByteAndRoundTripTargetSentinels()
    {
        WithMaps(1, (_, allocator) =>
        {
            var map = allocator.getInVarToRegMap(1) ?? throw new AssertionException("Missing incoming map.");
            for (uint value = 0; value <= byte.MaxValue; value++)
            {
                allocator.setVarReg(map, 0, (regNumber)value);

                Assert.That(map[0], Is.EqualTo((byte)value));
                Assert.That((uint)allocator.getVarReg(map, 0), Is.EqualTo(value));
            }

            allocator.setVarReg(map, 0, REG_STK);
            Assert.That(allocator.getVarReg(map, 0), Is.EqualTo(REG_STK));
            allocator.setVarReg(map, 0, REG_NA);
            Assert.That(allocator.getVarReg(map, 0), Is.EqualTo(REG_NA));
        });
    }

    [TestCase(256u)]
    [TestCase(0x1_0000u)]
    [TestCase(0x2000_0000u)]
    [TestCase(uint.MaxValue)]
    public static void FullWidthValuesCannotBeTruncatedIntoVariableMaps(uint value)
    {
        Assume.That(TestConfiguration.IsDebug, "Debug map overflow validation is available only in Debug.");

        WithMaps(1, (unusedCompiler, allocator) =>
        {
            var map = allocator.getInVarToRegMap(1) ?? throw new AssertionException("Missing incoming map.");
            allocator.setVarReg(map, 0, REG_NA);

            _ = Assert.Throws<OverflowException>(() => allocator.setVarReg(map, 0, (regNumber)value));

            Assert.That(allocator.getVarReg(map, 0), Is.EqualTo(REG_NA));
        });
    }

    [Test]
    public static void MapCopyIncludesPaddingAndPreservesSplitBlockAliases()
    {
        WithMaps(1, (compiler, allocator) =>
        {
            var destination = allocator.getInVarToRegMap(1)
                ?? throw new AssertionException("Missing incoming map.");
            compiler.fgBBNumMax = 3;
            allocator.getSplitBBNumToTargetBBNumMap().Add(3,
                new LinearScan.SplitEdgeInfo { fromBBNum = 0, toBBNum = 1 });
            var alias = allocator.getInVarToRegMap(3);
            byte[] source = [0, 127, 128, 255, 42];

            var result = allocator.setInVarToRegMap(1, source);

            Assert.That(result, Is.SameAs(destination));
            Assert.That(allocator.getInVarToRegMap(3), Is.SameAs(alias).And.SameAs(destination));
            Assert.That(allocator.getOutVarToRegMap(3), Is.SameAs(destination));
            Assert.That(destination, Is.EqualTo(source[..4]));
            Assert.That(Buffer.ByteLength(destination), Is.EqualTo(4));
            source[0] = 1;
            Assert.That(destination[0], Is.Zero);
            Assert.That(source[4], Is.EqualTo(42));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VariableMapDumpPreservesRegisterNamesAndSkipsStackAndPadding(bool stackOnly)
    {
        Assume.That(TestConfiguration.IsDebug, "Variable-map diagnostics are available only in Debug.");

        WithMaps(3, (compiler, allocator) =>
        {
            compiler.lvaTrackedToVarNum = [12, 34, 7];
            var map = allocator.getInVarToRegMap(1) ?? throw new AssertionException("Missing incoming map.");
            if (!stackOnly)
            {
                allocator.setVarReg(map, 0, REG_INT_FIRST);
                allocator.setVarReg(map, 2, REG_FP_FIRST);
            }
            map[3] = byte.MaxValue;
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;

                DumpVarToRegMap(allocator, map);
                writer.Flush();

                var expected = stackOnly
                    ? $"none{Environment.NewLine}" : $"V12={REG_INT_FIRST.Name} V07={REG_FP_FIRST.Name} {Environment.NewLine}";
                Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
            }
            finally
            {
                s_jitstdout = previous;
            }
        });
    }

    [Test]
    public static void DebuggerMapsExposeFullWidthRegisterIdentitiesForLiveLocalsOnly()
    {
        Assume.That(TestConfiguration.IsDebug, "Debugger register maps are available only in Debug.");

        WithMaps(3, (compiler, allocator) =>
        {
            compiler.lvaTrackedCountInSizeTUnits = 1;
            var live = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, live, 0);
            SetOps.AddElemD(compiler, live, 2);
            compiler.fgFirstBB = new BasicBlock(null, null) { bbNum = 1, bbLiveIn = live };
            allocator.localVarIntervals = [new Interval(TYP_INT, default) { varNum = 12 }, null, null];
            var inMap = allocator.getInVarToRegMap(1) ?? throw new AssertionException("Missing incoming map.");
            var outMap = allocator.getOutVarToRegMap(1) ?? throw new AssertionException("Missing outgoing map.");
            allocator.setVarReg(inMap, 0, (regNumber)128);
            allocator.setVarReg(inMap, 2, REG_NA);
            allocator.setVarReg(outMap, 2, (regNumber)255);
            var proxy = new LinearScan.DebuggerProxy(allocator);

            var incoming = proxy.InVarToRegMaps;
            var outgoing = proxy.OutVarToRegMaps;

            Assert.That(incoming, Has.Length.EqualTo(1));
            Assert.That(outgoing, Has.Length.EqualTo(1));
            Assert.That(incoming[0].BlockNumber, Is.EqualTo(1));
            Assert.That(incoming[0].Variables, Has.Length.EqualTo(2));
            Assert.That(outgoing[0].Variables, Has.Length.EqualTo(2));
            Assert.That(incoming[0].Variables[0].VariableNumber, Is.EqualTo(12u));
            Assert.That(incoming[0].Variables[0].Register, Is.EqualTo((regNumber)128));
            Assert.That(incoming[0].Variables[1].VariableNumber, Is.EqualTo(2u));
            Assert.That(incoming[0].Variables[1].Register, Is.EqualTo(REG_NA));
            Assert.That(outgoing[0].Variables[0].Register, Is.EqualTo(REG_STK));
            Assert.That(outgoing[0].Variables[1].Register, Is.EqualTo((regNumber)255));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpVarToRegMap")]
    private static extern void DumpVarToRegMap(LinearScan allocator, byte[] map);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_compiler")]
    private static extern ref Compiler CompilerInstance(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocals(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regMapCount")]
    private static extern ref uint RegMapCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint MaxBlockBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_inVarToRegMaps")]
    private static extern ref byte[]?[]? InMaps(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_outVarToRegMaps")]
    private static extern ref byte[]?[]? OutMaps(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_sharedCriticalVarToRegMap")]
    private static extern ref byte[]? ScratchMap(LinearScan allocator);

    private static void WithMaps(int trackedCount, Action<Compiler, LinearScan> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaCount = trackedCount;
        compiler.lvaTrackedCount = trackedCount;
        compiler.lvaTrackedFixed = true;
        compiler.lvaTable = new LclVarDsc[trackedCount];
        compiler.fgBBNumMax = 2;
        for (var index = 0; index < trackedCount; index++)
        {
            compiler.lvaTable[index]._varIndex = checked((ushort)index);
        }

        // Map storage is independent of backend register selection and codegen initialization.
        var allocator = (LinearScan)RuntimeHelpers.GetUninitializedObject(typeof(LinearScan));
        CompilerInstance(allocator) = compiler;
        EnregisterLocals(allocator) = true;
        MaxBlockBeforeResolution(allocator) = 2;
        JitTls.Compiler = compiler;
        try
        {
            allocator.initVarRegMaps();
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
