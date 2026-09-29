// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if EMITTER_STATS
using System.Runtime.CompilerServices;
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
}
#endif
