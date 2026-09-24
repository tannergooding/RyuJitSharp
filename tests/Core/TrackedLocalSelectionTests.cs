// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class TrackedLocalSelectionTests
{
    private readonly struct LatePolicy : ILivenessPolicy
    {
    }

    private readonly struct EarlyPolicy : ILivenessPolicy
    {
        public static bool IsEarly => true;
    }

    private readonly struct AsyncPolicy : ILivenessPolicy
    {
        public static bool TrackAddressExposedLocals => true;
    }

    [Test]
    public static void BlendedOrderingPreservesTypeWeightCountGcAndLocalNumberPriorities()
    {
        WithCompiler(10, compiler => {
            SetLocal(compiler, 0, TYP_INT, 1, 10);
            SetLocal(compiler, 1, TYP_DOUBLE, 9, 1000);
            SetLocal(compiler, 2, TYP_INT, 1, 10);
            compiler.lvaTable[2].lvIsRegArg = true;
            SetLocal(compiler, 3, TYP_REF, 1, 10);
            SetLocal(compiler, 4, TYP_INT, 2, 10);
            SetLocal(compiler, 5, TYP_SIMD16, 99, 2000);
            SetLocal(compiler, 6, TYP_INT, 1, 0);
            SetLocal(compiler, 7, TYP_DOUBLE, 1, 0);
            SetLocal(compiler, 8, TYP_INT, 1, 10);
            SetLocal(compiler, 9, TYP_INT, 1, 0.005);
            compiler.lvaTable[9].lvIsRegArg = true;

            new Liveness<LatePolicy>(compiler).SelectTrackedLocals();

            AssertOrder(compiler, [2, 4, 3, 0, 8, 5, 1, 6, 7, 9]);
            Assert.That(compiler.lvaTable[2].lvRefCntWtd(), Is.EqualTo(10));
            Assert.That(compiler.lvaTable[9].lvRefCntWtd(), Is.EqualTo(0.005));
        });
    }

    [Test]
    public static void SmallCodeOrderingUsesCountsThenExactWeightsBeforeArgumentAndGcTies()
    {
        WithCompiler(8, compiler => {
            SetLocal(compiler, 0, TYP_INT, 2, 10);
            SetLocal(compiler, 1, TYP_INT, 3, 1);
            SetLocal(compiler, 2, TYP_INT, 2, 10);
            compiler.lvaTable[2].lvIsRegArg = true;
            SetLocal(compiler, 3, TYP_REF, 2, 10);
            SetLocal(compiler, 4, TYP_INT, 2, 10.005);
            SetLocal(compiler, 5, TYP_DOUBLE, 100, 10000);
            SetLocal(compiler, 6, TYP_INT, 2, 10);
            SetLocal(compiler, 7, TYP_INT, 1, 100000);

            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                local.lvTracked = true;
            }
            int[] candidates = [.. Enumerable.Range(0, 8)];

            // compCodeOpt is pinned to BLENDED_CODE in both native and managed
            // builds, so exercise the retained small-code comparator directly.
            Liveness<LatePolicy>.SortTrackedCandidates(candidates, new Liveness<LatePolicy>.SmallCodeLess(compiler));

            int[] expected = [1, 4, 2, 3, 0, 6, 7, 5];
            Assert.That(candidates, Is.EqualTo(expected));
        });
    }

    [TestCase(false, 4)]
    [TestCase(true, 4)]
    [TestCase(true, 2)]
    [TestCase(false, 0)]
    [TestCase(false, -1)]
    public static void EarlyPolicySkipsSortingOnlyWhenEveryCandidateFits(bool early, int limit)
    {
        WithCompiler(4, compiler => {
            MaxLocalsToTrack(ref JitConfig) = limit;
            compiler.lvaRefCountState = RCS_EARLY;
            for (var index = 0; index < 4; index++)
            {
                SetLocal(compiler, index, TYP_INT, 1, (index + 1) * BB_UNITY_WEIGHT);
            }

            if (early)
            {
                new Liveness<EarlyPolicy>(compiler).SelectTrackedLocals();
            }
            else
            {
                new Liveness<LatePolicy>(compiler).SelectTrackedLocals();
            }

            int[] expected = early && (limit == 4) ? [0, 1, 2, 3] : [3, 2, 1, 0];
            var trackedCount = limit < 0 ? 4 : limit;
            AssertOrder(compiler, expected[..trackedCount]);
            Assert.That(compiler.lvaTrackedToVarNum, Is.EqualTo(expected));
            for (var index = trackedCount; index < 4; index++)
            {
                Assert.That(compiler.lvaTable[expected[index]].lvTracked, Is.False);
            }
            Assert.That(compiler.lvaRefCountState, Is.EqualTo(RCS_EARLY));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FilteringPreservesImplicitUsesAndOnlyAsyncTracksExposedLocals(bool async)
    {
        WithCompiler(6, compiler => {
            for (var index = 0; index < 6; index++)
            {
                SetLocal(compiler, index, TYP_INT, (ushort)(6 - index), (6 - index) * BB_UNITY_WEIGHT);
#if DEBUG
                compiler.lvaTable[index].lvTrackedWithoutIndex = true;
#endif
            }
            compiler.lvaTable[0].setLvRefCnt(0);
            compiler.lvaTable[1].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            compiler.lvaTable[2].Type = TYP_STRUCT;
            compiler.lvaTable[2].lvPromoted = true;
            compiler.lvaTable[3].Type = TYP_REF;
            compiler.lvaTable[3].lvPinned = true;
            compiler.lvaTable[4].lvDoNotEnregister = true;
            compiler.lvaTable[5].setLvRefCnt(0);
            compiler.lvaTable[5].setLvRefCntWtd(0);
            compiler.lvaTable[5].lvImplicitlyReferenced = true;

            if (async)
            {
                new Liveness<AsyncPolicy>(compiler).SelectTrackedLocals();
            }
            else
            {
                new Liveness<LatePolicy>(compiler).SelectTrackedLocals();
            }

            AssertOrder(compiler, async ? [1, 4, 5] : [4, 5]);
            Assert.That(compiler.lvaTable[0].lvRefCntWtd(), Is.Zero);
            Assert.That(compiler.lvaTable[1].lvTracked, Is.EqualTo(async));
            Assert.That(compiler.lvaTable[2].lvTracked, Is.False);
            Assert.That(compiler.lvaTable[3].lvTracked, Is.False);
            Assert.That(compiler.lvaTable[4].lvDoNotEnregister, Is.True);
#if DEBUG
            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                Assert.That(local.lvTrackedWithoutIndex, Is.False);
            }
#endif
        });
    }

    [TestCase(1)]
    [TestCase(64)]
    [TestCase(65)]
    [TestCase(129)]
    public static void SelectionUpdatesEpochWordCountAndCompleteDebugTrackedSet(int count)
    {
        WithCompiler(count, compiler => {
            compiler.lvaCurEpoch = int.MaxValue;
            for (var index = 0; index < count; index++)
            {
                SetLocal(compiler, index, TYP_INT, 1, 1);
            }
            var reverseMap = new int[count + 1];
            reverseMap[^1] = 173;
            compiler.lvaTrackedToVarNum = reverseMap;

            new Liveness<LatePolicy>(compiler).SelectTrackedLocals();

            AssertOrder(compiler, [.. Enumerable.Range(0, count)]);
            Assert.That(compiler.lvaTrackedToVarNum, Is.SameAs(reverseMap));
            Assert.That(reverseMap[^1], Is.EqualTo(173));
            Assert.That(compiler.lvaCurEpoch, Is.EqualTo(int.MinValue));
            Assert.That(compiler.lvaTrackedCountInSizeTUnits, Is.EqualTo((count + 63) / 64));
            Assert.That(TrackedVarBitSetTraits.GetArrSize(compiler), Is.EqualTo((count + 63) / 64));
            Assert.That(TrackedVarBitSetTraits.GetSize(compiler), Is.EqualTo(count));
            var full = BitSetOps<Compiler, TrackedVarBitSetTraits>.MakeFull(compiler);
            Assert.That(BitSetOps<Compiler, TrackedVarBitSetTraits>.Count(compiler, full), Is.EqualTo((nint)count));
            Assert.That(BitSetOps<Compiler, TrackedVarBitSetTraits>.IsMember(compiler, full, count - 1), Is.True);
#if DEBUG
            Assert.That(compiler.lvaTrackedVars.Length, Is.EqualTo((count + 63) / 64));
            for (var index = 0; index < count; index++)
            {
                Assert.That((compiler.lvaTrackedVars[index / 64] & ((nint)1 << (index % 64))) != 0, Is.True);
            }
            if ((count % 64) != 0)
            {
                Assert.That(compiler.lvaTrackedVars[^1] >>> (count % 64), Is.EqualTo((nint)0));
            }
#endif
        });
    }

    [Test]
    public static void EmptyTableResetsTrackedStateWithoutChangingEpochOrMapping()
    {
        WithCompiler(0, compiler => {
            compiler.lvaCurEpoch = 21;
            compiler.lvaTrackedCount = 8;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            int[] previousMapping = [42];
            compiler.lvaTrackedToVarNum = previousMapping;

            new Liveness<LatePolicy>(compiler).SelectTrackedLocals();

            Assert.That(compiler.lvaTrackedCount, Is.Zero);
            Assert.That(compiler.lvaTrackedCountInSizeTUnits, Is.Zero);
            Assert.That(compiler.lvaCurEpoch, Is.EqualTo(21));
            Assert.That(compiler.lvaTrackedToVarNum, Is.SameAs(previousMapping));
#if DEBUG
            Assert.That(compiler.lvaTrackedVars, Is.Empty);
#endif
        });
    }

    [Test]
    public static void ReverseMapGrowsOnlyWhenRequiredAndReselectionClearsLostCandidates()
    {
        WithCompiler(3, compiler => {
            for (var index = 0; index < 3; index++)
            {
                SetLocal(compiler, index, TYP_INT, 1, index + 1);
            }
            int[] oldMapping = [17];
            compiler.lvaTrackedToVarNum = oldMapping;
            var liveness = new Liveness<LatePolicy>(compiler);

            liveness.SelectTrackedLocals();

            AssertOrder(compiler, [2, 1, 0]);
            var newMapping = compiler.lvaTrackedToVarNum;
            Assert.That(newMapping, Is.Not.SameAs(oldMapping));
            Assert.That(newMapping, Has.Length.EqualTo(3));
            compiler.lvaTable[2].setLvRefCnt(0);
            compiler.lvaTable[1].lvPinned = true;

            liveness.SelectTrackedLocals();

            AssertOrder(compiler, [0]);
            Assert.That(compiler.lvaTrackedToVarNum, Is.SameAs(newMapping));
            Assert.That(compiler.lvaTable[1].lvTracked, Is.False);
            Assert.That(compiler.lvaTable[2].lvTracked, Is.False);
            Assert.That(compiler.lvaTable[2].lvRefCntWtd(), Is.Zero);
            Assert.That(compiler.lvaCurEpoch, Is.EqualTo(2));
        });
    }

    private static void AssertOrder(Compiler compiler, int[] expected)
    {
        Assert.That(compiler.lvaTrackedCount, Is.EqualTo(expected.Length));
        Assert.That(compiler.lvaTrackedToVarNum.AsSpan(0, expected.Length).ToArray(), Is.EqualTo(expected));
        for (var index = 0; index < expected.Length; index++)
        {
            ref var local = ref compiler.lvaTable[expected[index]];
            Assert.That(local.lvTracked, Is.True);
            Assert.That(local._varIndex, Is.EqualTo(index));
        }
    }

    private static void SetLocal(Compiler compiler, int index, var_types type, ushort count, double weight)
    {
        ref var local = ref compiler.lvaTable[index];
        local.Type = type;
        local.setLvRefCnt(count, compiler.lvaRefCountState);
        local.setLvRefCntWtd(weight, compiler.lvaRefCountState);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocalsToTrack(ref JitConfigValues config);

    private static void WithCompiler(int count, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaRefCountState = RCS_NORMAL;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
        MaxLocalsToTrack(ref JitConfig) = 1024;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
