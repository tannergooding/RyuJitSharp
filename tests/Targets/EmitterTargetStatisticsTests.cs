// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if EMITTER_STATS && TARGET_ARM64
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterTargetStatisticsTests
{
    [Test]
    public static void LocalVariablePairAllocationAndStatisticsMatchNativeCounters()
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
        var previousInstructions = TotalInstructions(null);
        var previousDescriptors = TotalDescriptors(null);
        var previousConstantDescriptors = TotalConstantDescriptors(null);
        var previousLocalVariablePairDescriptors = TotalLocalVariablePairDescriptors(null);
        var previousLocalVariablePairConstantDescriptors = TotalLocalVariablePairConstantDescriptors(null);

        try
        {
            TotalInstructions(null) = 0;
            TotalDescriptors(null) = 0;
            TotalConstantDescriptors(null) = 0;
            TotalLocalVariablePairDescriptors(null) = 0;
            TotalLocalVariablePairConstantDescriptors(null) = 0;

            var emitter = CreateEmitter();
            _ = AllocateLocalVariablePair(emitter, emitAttr.EA_8BYTE, 1);
            _ = AllocateLocalVariablePair(emitter, emitAttr.EA_8BYTE, 0x12345678);

            Assert.That(TotalInstructions(null), Is.EqualTo(2u));
            Assert.That(TotalDescriptors(null), Is.EqualTo(2u));
            Assert.That(TotalConstantDescriptors(null), Is.EqualTo(2u));
            Assert.That(TotalLocalVariablePairDescriptors(null), Is.EqualTo(1u));
            Assert.That(TotalLocalVariablePairConstantDescriptors(null), Is.EqualTo(1u));

            var text = GetStatisticsReport();
            Assert.That(Regex.IsMatch(text, @"(?m)^Total instrDescLclVarPair:\s+1 \(50\.00%\)$"), Is.True);
            Assert.That(Regex.IsMatch(text, @"(?m)^Total instrDescLclVarPairCns:\s+1 \(50\.00%\)$"), Is.True);
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
            TotalInstructions(null) = previousInstructions;
            TotalDescriptors(null) = previousDescriptors;
            TotalConstantDescriptors(null) = previousConstantDescriptors;
            TotalLocalVariablePairDescriptors(null) = previousLocalVariablePairDescriptors;
            TotalLocalVariablePairConstantDescriptors(null) = previousLocalVariablePairConstantDescriptors;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrLclVarPair")]
    private static extern Emitter.instrDesc AllocateLocalVariablePair(Emitter emitter, emitAttr attr, nint constant);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescLclVarPairCnt")]
    private static extern ref uint TotalLocalVariablePairDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescLclVarPairCnsCnt")]
    private static extern ref uint TotalLocalVariablePairConstantDescriptors(Emitter? emitter);

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new Emitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , false
#endif
            );

        return emitter;
    }

    private static string GetStatisticsReport()
    {
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        Emitter.emitterStats(output);
        output.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
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

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalInsCnt")]
    private static extern ref uint TotalInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCnt")]
    private static extern ref uint TotalDescriptors(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCnsCnt")]
    private static extern ref uint TotalConstantDescriptors(Emitter? emitter);
}
#endif
