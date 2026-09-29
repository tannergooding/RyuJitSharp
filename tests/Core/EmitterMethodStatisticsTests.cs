// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if EMITTER_STATS
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterMethodStatisticsTests
{
    [Test]
    public static void MethodStartResetsLocalCountersAndAccountsForTwoNativeGroups()
    {
        var previousMethods = TotalMethods(null);
        var previousGroups = TotalGroups(null);
        var previousGroupBytes = TotalGroupBytes(null);
        var previousMemory = TotalMemory(null);
        var previousMethodBytes = MethodBytes(null);
        var previousPrologInstructions = PrologInstructions(null);
        var previousPrologBytes = PrologBytes(null);
        var previousMaxPrologInstructions = MaxPrologInstructions(null);
        var previousMaxPrologBytes = MaxPrologBytes(null);

        try
        {
            TotalMethods(null) = 9;
            TotalGroups(null) = 7;
            TotalGroupBytes(null) = 64;
            TotalMemory(null) = 80;
            MethodBytes(null) = 96;
            PrologInstructions(null) = 4;
            PrologBytes(null) = 48;
            MaxPrologInstructions(null) = 17;
            MaxPrologBytes(null) = 128;

            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new Emitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);
            emitter.Init();

#if DEBUG
            const nuint groupSize = 136;
#else
            const nuint groupSize = 56;
#endif

            Begin(emitter);
            var allocation = (2 * groupSize) + Capacity(emitter);

            Assert.That(TotalMethods(null), Is.EqualTo(10u));
            Assert.That(TotalGroups(null), Is.EqualTo(9u));
            Assert.That(TotalGroupBytes(null), Is.EqualTo(64 + (2 * groupSize)));
            Assert.That(TotalMemory(null), Is.EqualTo(80 + allocation));
            Assert.That(MethodBytes(null), Is.EqualTo(2 * groupSize));
            Assert.That(PrologInstructions(null), Is.Zero);
            Assert.That(PrologBytes(null), Is.EqualTo((nuint)0));
            Assert.That(MaxPrologInstructions(null), Is.EqualTo(17u));
            Assert.That(MaxPrologBytes(null), Is.EqualTo((nuint)128));

            emitter.Init();
            Begin(emitter);

            Assert.That(TotalMethods(null), Is.EqualTo(11u));
            Assert.That(TotalGroups(null), Is.EqualTo(11u));
            Assert.That(TotalGroupBytes(null), Is.EqualTo(64 + (4 * groupSize)));
            Assert.That(TotalMemory(null), Is.EqualTo(80 + (2 * allocation)));
            Assert.That(MethodBytes(null), Is.EqualTo(2 * groupSize));
        }
        finally
        {
            TotalMethods(null) = previousMethods;
            TotalGroups(null) = previousGroups;
            TotalGroupBytes(null) = previousGroupBytes;
            TotalMemory(null) = previousMemory;
            MethodBytes(null) = previousMethodBytes;
            PrologInstructions(null) = previousPrologInstructions;
            PrologBytes(null) = previousPrologBytes;
            MaxPrologInstructions(null) = previousMaxPrologInstructions;
            MaxPrologBytes(null) = previousMaxPrologBytes;
        }
    }

    [Test]
    public static void DescriptorAllocationCountsNativeDebugInfoAndInstructions()
    {
        var instructionCounter = typeof(Emitter).GetField("emitTotalInsCnt", BindingFlags.NonPublic | BindingFlags.Static);
        var previousInstructions = instructionCounter?.GetValue(null);
        var previousMemory = TotalMemory(null);

        try
        {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new AllocationEmitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            Begin(emitter);

            DebugPrefix(emitter) = 8;
            var before = TotalMemory(null);
            var descriptor = emitter.AllocateBasic();
            var debugInfo = descriptor.idDebugOnlyInfo() ?? throw new AssertionException("Missing descriptor debug info.");

            Assert.That(debugInfo.idSize, Is.EqualTo((nuint)16));
            Assert.That(TotalMemory(null), Is.EqualTo(before + 56));
            Assert.That(TotalInstructions(null), Is.EqualTo(((uint?)previousInstructions ?? 0u) + 1));
        }
        finally
        {
            instructionCounter?.SetValue(null, previousInstructions);
            TotalMemory(null) = previousMemory;
        }
    }

    [Test]
    public static void RawEmitterMemoryCountsTheRequestedNativeBytes()
    {
        var previousMemory = TotalMemory(null);

        try
        {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new Emitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);

            var allocation = AllocateBytes(emitter, 32);

            Assert.That(allocation, Has.Length.EqualTo(32));
            Assert.That(TotalMemory(null), Is.EqualTo(previousMemory + 32));
        }
        finally
        {
            TotalMemory(null) = previousMemory;
        }
    }

    [Test]
    public static void StaticLayoutReportUsesNativeGroupAndDescriptorSizes()
    {
        var previous = Globals.s_jitstdout;
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

        try
        {
            Globals.s_jitstdout = output;
            StaticReport(null);

#if DEBUG
            const int groupSize = 136;
            const int groupNumberOffset = 88;
            const int groupRegisterOffset = 112;
            const int groupDataOffset = 120;
            const int groupInstructionOffset = 132;
#else
            const int groupSize = 56;
            const int groupNumberOffset = 8;
            const int groupRegisterOffset = 32;
            const int groupDataOffset = 40;
            const int groupInstructionOffset = 52;
#endif
            var text = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(text, Does.Contain($"Size of insGroup                    = {groupSize}"));
            Assert.That(text, Does.Contain("Size of insPlaceholderGroupData     = 104"));
            Assert.That(text, Does.Contain("Size of GCInfo::regPtrDsc   = 40"));
            Assert.That(text, Does.Contain("Offset of <union>           = 24"));
            Assert.That(text, Does.Contain("Size of instrDescCGCA          = 72"));
            Assert.That(text, Does.Contain("igBuffSize                           = 912"));
            AssertField(text, "igNum", groupNumberOffset, 4);
            AssertField(text, "igGCregs", groupRegisterOffset, 8);
            AssertField(text, "igData", groupDataOffset, 8);
            AssertField(text, "igInsCnt", groupInstructionOffset, 1);
            AssertField(text, "igPhInitGCrefRegs", 24, 16);
            AssertField(text, "igPhPrevByrefRegs", 80, 16);
            AssertField(text, "igPhType", 96, 1);
        }
        finally
        {
            Globals.s_jitstdout = previous;
        }
    }

    private static void AssertField(string report, string name, int offset, int size)
    {
        var match = Regex.Match(report, $@"(?m)^Offset / size of {Regex.Escape(name)}\s+=\s*(\d+)\s*/\s*(\d+)\s*$");
        Assert.That(match.Success, Is.True, $"Missing native layout for {name}");
        Assert.That(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(offset), name);
        Assert.That(int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(size), name);
    }

    private sealed class AllocationEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public instrDesc AllocateBasic()
        {
            return Allocate<instrDescBasic>(this, 16, emitAttr.EA_4BYTE);
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocAnyInstr")]
        private static extern T Allocate<T>(Emitter emitter, nuint size, emitAttr attr)
            where T : instrDesc, new();
    }

    private static void Begin(Emitter emitter)
    {
        emitter.emitBegFN(false
#if DEBUG
            , false
#endif
            );
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGmcnt")]
    private static extern ref uint TotalMethods(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGcnt")]
    private static extern ref uint TotalGroups(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGsize")]
    private static extern ref nuint TotalGroupBytes(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotMemAlloc")]
    private static extern ref nuint TotalMemory(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitSizeMethod")]
    private static extern ref nuint MethodBytes(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitCurPrologInsCnt")]
    private static extern ref uint PrologInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitCurPrologIGSize")]
    private static extern ref nuint PrologBytes(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitMaxPrologInsCnt")]
    private static extern ref uint MaxPrologInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitMaxPrologIGSize")]
    private static extern ref nuint MaxPrologBytes(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalInsCnt")]
    private static extern ref uint TotalInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int DebugPrefix(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitterStaticStats")]
    private static extern void StaticReport(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetMem")]
    private static extern byte[] AllocateBytes(Emitter emitter, nuint size);
}
#endif
