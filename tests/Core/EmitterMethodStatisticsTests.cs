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
    public static void SmallDescriptorAllocationCountsNativeSmallDescriptors()
    {
        var previousSmallDescriptors = TotalSmallDescriptors(null);
        var previousInstructions = TotalInstructions(null);
        var previousMemory = TotalMemory(null);

        try
        {
            TotalSmallDescriptors(null) = 11;

            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new AllocationEmitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            Begin(emitter);

            _ = emitter.AllocateSmall(emitAttr.EA_4BYTE);

            Assert.That(TotalSmallDescriptors(null), Is.EqualTo(12u));
            Assert.That(TotalInstructions(null), Is.EqualTo(previousInstructions + 1));
        }
        finally
        {
            TotalSmallDescriptors(null) = previousSmallDescriptors;
            TotalInstructions(null) = previousInstructions;
            TotalMemory(null) = previousMemory;
        }
    }

    [Test]
    public static void ConstantAndDisplacementAllocationCountsNativeStatistics()
    {
        var smallCnsBuckets = SmallCnsBuckets(null);
        var previousBasicDescriptors = TotalBasicDescriptors(null);
        var previousConstantDescriptors = TotalConstantDescriptors(null);
        var previousDisplacementDescriptors = TotalDisplacementDescriptors(null);
        var previousConstantDisplacementDescriptors = TotalConstantDisplacementDescriptors(null);
        var previousSmallCnsBuckets = (uint[])smallCnsBuckets.Clone();
        var previousSmallConstants = TotalSmallConstants(null);
        var previousLargeConstants = TotalLargeConstants(null);
        var previousInt8Constants = Int8Constants(null);
        var previousInt16Constants = Int16Constants(null);
        var previousInt32Constants = Int32Constants(null);
        var previousNegativeConstants = NegativeConstants(null);
        var previousPowerOfTwoConstants = PowerOfTwoConstants(null);
        var previousSmallDisplacements = SmallDisplacements(null);
        var previousLargeDisplacements = LargeDisplacements(null);
        var previousSmallDescriptors = TotalSmallDescriptors(null);
        var previousInstructions = TotalInstructions(null);
        var previousMemory = TotalMemory(null);

        try
        {
            TotalBasicDescriptors(null) = 0;
            TotalConstantDescriptors(null) = 0;
            TotalDisplacementDescriptors(null) = 0;
            TotalConstantDisplacementDescriptors(null) = 0;
            Array.Clear(smallCnsBuckets, 0, smallCnsBuckets.Length);
            TotalSmallConstants(null) = 0;
            TotalLargeConstants(null) = 0;
            Int8Constants(null) = 0;
            Int16Constants(null) = 0;
            Int32Constants(null) = 0;
            NegativeConstants(null) = 0;
            PowerOfTwoConstants(null) = 0;
            SmallDisplacements(null) = 0;
            LargeDisplacements(null) = 0;

            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new AllocationEmitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            Begin(emitter);

            _ = emitter.AllocateSmallConstant(emitAttr.EA_4BYTE, -16);
            _ = emitter.AllocateSmallConstant(emitAttr.EA_4BYTE, 15);
            _ = emitter.AllocateSmallConstant(emitAttr.EA_4BYTE, 1);
            _ = emitter.AllocateSmallConstant(emitAttr.EA_4BYTE, 16);
            _ = emitter.AllocateConstant(emitAttr.EA_4BYTE, -1);
            _ = emitter.AllocateConstant(emitAttr.EA_4BYTE, 128);
            _ = emitter.AllocateConstant(emitAttr.EA_8BYTE, nint.MinValue);
            _ = emitter.AllocateDisplacement(emitAttr.EA_4BYTE, 0);
            _ = emitter.AllocateDisplacement(emitAttr.EA_4BYTE, 1);
            _ = emitter.AllocateDisplacementConstant(emitAttr.EA_4BYTE, -16, 0);
            _ = emitter.AllocateDisplacementConstant(emitAttr.EA_4BYTE, 32768, 0);
            _ = emitter.AllocateDisplacementConstant(emitAttr.EA_4BYTE, 1, 4);
            _ = emitter.AllocateDisplacementConstant(emitAttr.EA_4BYTE, 32768, 4);

            Assert.That(TotalSmallConstants(null), Is.EqualTo(6u));
            Assert.That(TotalLargeConstants(null), Is.EqualTo(5u));
            Assert.That(smallCnsBuckets[112], Is.EqualTo(2u));
            Assert.That(smallCnsBuckets[127], Is.EqualTo(1u));
            Assert.That(smallCnsBuckets[129], Is.EqualTo(2u));
            Assert.That(smallCnsBuckets[143], Is.EqualTo(1u));
            Assert.That(Int8Constants(null), Is.EqualTo(7u));
            Assert.That(Int16Constants(null), Is.EqualTo(1u));
            Assert.That(Int32Constants(null), Is.EqualTo(2u));
            Assert.That(NegativeConstants(null), Is.EqualTo(4u));
            Assert.That(PowerOfTwoConstants(null), Is.EqualTo(6u));
            Assert.That(SmallDisplacements(null), Is.EqualTo(3u));
            Assert.That(LargeDisplacements(null), Is.EqualTo(3u));
            Assert.That(TotalBasicDescriptors(null), Is.EqualTo(3u));
            Assert.That(TotalConstantDescriptors(null), Is.EqualTo(4u));
            Assert.That(TotalDisplacementDescriptors(null), Is.EqualTo(2u));
            Assert.That(TotalConstantDisplacementDescriptors(null), Is.EqualTo(1u));
            Assert.That(TotalSmallDescriptors(null), Is.EqualTo(previousSmallDescriptors + 3));
            Assert.That(TotalInstructions(null), Is.EqualTo(previousInstructions + 13));
        }
        finally
        {
            TotalBasicDescriptors(null) = previousBasicDescriptors;
            TotalConstantDescriptors(null) = previousConstantDescriptors;
            TotalDisplacementDescriptors(null) = previousDisplacementDescriptors;
            TotalConstantDisplacementDescriptors(null) = previousConstantDisplacementDescriptors;
            Array.Copy(previousSmallCnsBuckets, smallCnsBuckets, smallCnsBuckets.Length);
            TotalSmallConstants(null) = previousSmallConstants;
            TotalLargeConstants(null) = previousLargeConstants;
            Int8Constants(null) = previousInt8Constants;
            Int16Constants(null) = previousInt16Constants;
            Int32Constants(null) = previousInt32Constants;
            NegativeConstants(null) = previousNegativeConstants;
            PowerOfTwoConstants(null) = previousPowerOfTwoConstants;
            SmallDisplacements(null) = previousSmallDisplacements;
            LargeDisplacements(null) = previousLargeDisplacements;
            TotalSmallDescriptors(null) = previousSmallDescriptors;
            TotalInstructions(null) = previousInstructions;
            TotalMemory(null) = previousMemory;
        }
    }

    [Test]
    public static void DescriptorKindAllocationsCountNativeStatistics()
    {
        var previousBasicDescriptors = TotalBasicDescriptors(null);
        var previousJumpDescriptors = TotalJumpDescriptors(null);
#if !TARGET_WASM
        var previousCallDescriptors = TotalCallDescriptors(null);
#endif
        var previousInstructions = TotalInstructions(null);
        var previousMemory = TotalMemory(null);
#if TARGET_XARCH
        var previousAddressModeDescriptors = TotalAddressModeDescriptors(null);
        var previousConstantAddressModeDescriptors = TotalConstantAddressModeDescriptors(null);
#endif
#if FEATURE_LOOP_ALIGN
        var previousAlignmentDescriptors = TotalAlignmentDescriptors(null);
#endif

        try
        {
            TotalBasicDescriptors(null) = 0;
            TotalJumpDescriptors(null) = 0;
#if !TARGET_WASM
            TotalCallDescriptors(null) = 0;
#endif
            TotalInstructions(null) = 0;
#if TARGET_XARCH
            TotalAddressModeDescriptors(null) = 0;
            TotalConstantAddressModeDescriptors(null) = 0;
#endif
#if FEATURE_LOOP_ALIGN
            TotalAlignmentDescriptors(null) = 0;
#endif

            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new AllocationEmitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            Begin(emitter);

            _ = emitter.AllocateDescriptor(emitAttr.EA_4BYTE);
            _ = emitter.AllocateJumpDescriptor();
#if !TARGET_WASM
            _ = emitter.AllocateCallDescriptor(emitAttr.EA_4BYTE);
#endif
#if TARGET_XARCH
            _ = emitter.AllocateAddressModeDescriptor(emitAttr.EA_4BYTE);
            _ = emitter.AllocateConstantAddressModeDescriptor(emitAttr.EA_4BYTE);
#endif
#if FEATURE_LOOP_ALIGN
            _ = emitter.AllocateAlignmentDescriptor();
#endif

            Assert.That(TotalBasicDescriptors(null), Is.EqualTo(1u));
            Assert.That(TotalJumpDescriptors(null), Is.EqualTo(1u));
#if !TARGET_WASM
            Assert.That(TotalCallDescriptors(null), Is.EqualTo(1u));
#endif
            Assert.That(TotalInstructions(null), Is.EqualTo((uint)(
                2
#if !TARGET_WASM
                + 1
#endif
#if TARGET_XARCH
                + 2
#endif
#if FEATURE_LOOP_ALIGN
                + 1
#endif
            )));
#if TARGET_XARCH
            Assert.That(TotalAddressModeDescriptors(null), Is.EqualTo(1u));
            Assert.That(TotalConstantAddressModeDescriptors(null), Is.EqualTo(1u));
#endif
#if FEATURE_LOOP_ALIGN
            Assert.That(TotalAlignmentDescriptors(null), Is.EqualTo(1u));
#endif
        }
        finally
        {
            TotalBasicDescriptors(null) = previousBasicDescriptors;
            TotalJumpDescriptors(null) = previousJumpDescriptors;
#if !TARGET_WASM
            TotalCallDescriptors(null) = previousCallDescriptors;
#endif
            TotalInstructions(null) = previousInstructions;
            TotalMemory(null) = previousMemory;
#if TARGET_XARCH
            TotalAddressModeDescriptors(null) = previousAddressModeDescriptors;
            TotalConstantAddressModeDescriptors(null) = previousConstantAddressModeDescriptors;
#endif
#if FEATURE_LOOP_ALIGN
            TotalAlignmentDescriptors(null) = previousAlignmentDescriptors;
#endif
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

#if TARGET_XARCH
    [Test]
    public static void StatisticsReportFormatsFrequencyDescriptorAndExceptionalCounts()
    {
        var formats = InstructionFormats(null);
        var previousFormats = (uint[])formats.Clone();
        var previousMethods = TotalMethods(null);
        var previousGroups = TotalGroups(null);
        var previousGroupInstructions = TotalGroupInstructions(null);
        var previousGroupBytes = TotalGroupBytes(null);
        var previousInstructions = TotalInstructions(null);
        var previousBasicDescriptors = TotalBasicDescriptors(null);
        var previousAllocatedSize = TotalAllocatedSize(null);
        var previousActualSize = TotalActualSize(null);

        try
        {
            Array.Clear(formats, 0, formats.Length);
            formats[0] = 1;
            formats[1] = 1;
            TotalMethods(null) = 1;
            TotalGroups(null) = 1;
            TotalGroupInstructions(null) = 0;
            TotalGroupBytes(null) = 64;
            TotalInstructions(null) = 2;
            TotalBasicDescriptors(null) = 1;
            TotalAllocatedSize(null) = 0;
            TotalActualSize(null) = 0;

            using var stream = new MemoryStream();
            using var output = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);
            Emitter.emitterStats(output);
            output.Flush();

            var text = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(text, Does.Contain(
                $"          {Emitter.emitIfName((uint)0),-14}        1 (50.00%)\n"));
            Assert.That(text, Does.Contain(
                $"          {Emitter.emitIfName((uint)1),-14}        1 (50.00%)\n"));
            Assert.That(text, Does.Contain("Total shown"));
            Assert.That(Regex.IsMatch(text, @"(?m)^Total instrDesc:\s+1 \(50\.00%\)$"), Is.True);
            Assert.That(text, Does.Contain("Average of      inf bytes        per instrDesc\n"));
        }
        finally
        {
            Array.Copy(previousFormats, formats, formats.Length);
            TotalMethods(null) = previousMethods;
            TotalGroups(null) = previousGroups;
            TotalGroupInstructions(null) = previousGroupInstructions;
            TotalGroupBytes(null) = previousGroupBytes;
            TotalInstructions(null) = previousInstructions;
            TotalBasicDescriptors(null) = previousBasicDescriptors;
            TotalAllocatedSize(null) = previousAllocatedSize;
            TotalActualSize(null) = previousActualSize;
        }
    }
#endif

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

        public instrDesc AllocateSmall(emitAttr attr)
        {
            return NewSmall(this, attr);
        }

        public instrDesc AllocateConstant(emitAttr attr, nint constant)
        {
            return NewConstant(this, attr, constant);
        }

        public instrDesc AllocateSmallConstant(emitAttr attr, nint constant)
        {
            return NewSmallConstant(this, attr, constant);
        }

        public instrDesc AllocateDisplacement(emitAttr attr, nint displacement)
        {
            return NewDisplacement(this, attr, displacement);
        }

        public instrDesc AllocateDisplacementConstant(emitAttr attr, nint constant, int displacement)
        {
            return NewDisplacementConstant(this, attr, constant, displacement);
        }

        public instrDesc AllocateDescriptor(emitAttr attr)
        {
            return NewDescriptor(this, attr);
        }

        public instrDescJmp AllocateJumpDescriptor()
        {
            return NewJumpDescriptor(this);
        }

#if !TARGET_WASM
        public instrDesc AllocateCallDescriptor(emitAttr attr)
        {
            return NewCallDescriptor(this, attr);
        }
#endif

#if TARGET_XARCH
        public instrDesc AllocateAddressModeDescriptor(emitAttr attr)
        {
            return NewAddressModeDescriptor(this, attr);
        }

        public instrDesc AllocateConstantAddressModeDescriptor(emitAttr attr)
        {
            return NewConstantAddressModeDescriptor(this, attr);
        }
#endif

#if FEATURE_LOOP_ALIGN
        public instrDesc AllocateAlignmentDescriptor()
        {
            return NewAlignmentDescriptor(this);
        }
#endif

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocAnyInstr")]
        private static extern T Allocate<T>(Emitter emitter, nuint size, emitAttr attr)
            where T : instrDesc, new();

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrSmall")]
        private static extern instrDescBasic NewSmall(Emitter emitter, emitAttr attr);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrCns")]
        private static extern instrDesc NewConstant(Emitter emitter, emitAttr attr, nint constant);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrSC")]
        private static extern instrDesc NewSmallConstant(Emitter emitter, emitAttr attr, nint constant);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrDsp")]
        private static extern instrDesc NewDisplacement(Emitter emitter, emitAttr attr, nint displacement);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrCnsDsp")]
        private static extern instrDesc NewDisplacementConstant(Emitter emitter, emitAttr attr, nint constant, int displacement);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocInstr")]
        private static extern instrDescBasic NewDescriptor(Emitter emitter, emitAttr attr);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocInstrJmp")]
        private static extern instrDescJmp NewJumpDescriptor(Emitter emitter);

#if !TARGET_WASM
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocInstrCGCA")]
        private static extern instrDescCGCA NewCallDescriptor(Emitter emitter, emitAttr attr);
#endif

#if TARGET_XARCH
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocInstrAmd")]
        private static extern instrDescAmd NewAddressModeDescriptor(Emitter emitter, emitAttr attr);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocInstrCnsAmd")]
        private static extern instrDescCnsAmd NewConstantAddressModeDescriptor(Emitter emitter, emitAttr attr);
#endif

#if FEATURE_LOOP_ALIGN
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocInstrAlign")]
        private static extern instrDescAlign NewAlignmentDescriptor(Emitter emitter);
#endif
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

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGicnt")]
    private static extern ref uint TotalGroupInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescSmallCnt")]
    private static extern ref uint TotalSmallDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCnt")]
    private static extern ref uint TotalBasicDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitIFcounts")]
    private static extern ref uint[] InstructionFormats(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "totAllocdSize")]
    private static extern ref uint TotalAllocatedSize(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "totActualSize")]
    private static extern ref uint TotalActualSize(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCnsCnt")]
    private static extern ref uint TotalConstantDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescDspCnt")]
    private static extern ref uint TotalDisplacementDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCnsDspCnt")]
    private static extern ref uint TotalConstantDisplacementDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescJmpCnt")]
    private static extern ref uint TotalJumpDescriptors(Emitter? emitter);

#if !TARGET_WASM
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCGCACnt")]
    private static extern ref uint TotalCallDescriptors(Emitter? emitter);
#endif

#if TARGET_XARCH
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescAmdCnt")]
    private static extern ref uint TotalAddressModeDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCnsAmdCnt")]
    private static extern ref uint TotalConstantAddressModeDescriptors(Emitter? emitter);
#endif

#if FEATURE_LOOP_ALIGN
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescAlignCnt")]
    private static extern ref uint TotalAlignmentDescriptors(Emitter? emitter);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitSmallCns")]
    private static extern ref uint[] SmallCnsBuckets(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitSmallCnsCnt")]
    private static extern ref uint TotalSmallConstants(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitLargeCnsCnt")]
    private static extern ref uint TotalLargeConstants(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitInt8CnsCnt")]
    private static extern ref uint Int8Constants(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitInt16CnsCnt")]
    private static extern ref uint Int16Constants(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitInt32CnsCnt")]
    private static extern ref uint Int32Constants(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitNegCnsCnt")]
    private static extern ref uint NegativeConstants(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitPow2CnsCnt")]
    private static extern ref uint PowerOfTwoConstants(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitSmallDspCnt")]
    private static extern ref uint SmallDisplacements(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitLargeDspCnt")]
    private static extern ref uint LargeDisplacements(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int DebugPrefix(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitterStaticStats")]
    private static extern void StaticReport(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetMem")]
    private static extern byte[] AllocateBytes(Emitter emitter, nuint size);
}
#endif
