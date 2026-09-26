// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ObjectAllocatorGraphTests
{
    [TestCase(false, 3)]
    [TestCase(true, 4)]
    public static void PreparationTracksTypesAndPreservesIndexWidths(bool trackFields, int trackedCount)
    {
        WithAllocator([TYP_REF, TYP_INT, TYP_BYREF, TYP_STRUCT, TYP_REF], trackFields, false, false,
            (compiler, allocator) => {
                var reverseMap = new int[10];
                compiler.lvaTrackedToVarNum = reverseMap;
                Call(allocator, "PrepareAnalysis");

                Assert.That(Get<int>(allocator, "_nextLocalIndex"), Is.EqualTo(trackedCount));
                Assert.That(Get<int>(allocator, "_firstPseudoIndex"), Is.EqualTo(trackedCount));
                Assert.That(Get<int>(allocator, "_unknownSourceIndex"), Is.EqualTo(trackedCount));
                Assert.That(Get<int>(allocator, "_bvCount"), Is.EqualTo(trackedCount + 1));
                Assert.That(compiler.lvaTrackedToVarNum, Is.SameAs(reverseMap));
                Assert.That(compiler.lvaTable[1].lvTracked, Is.False);
                Assert.That(compiler.lvaTable[1]._varIndex, Is.Zero);
                Assert.That(compiler.lvaTable[3].lvTracked, Is.EqualTo(trackFields));

                for (var index = 0; index < trackedCount; index++)
                {
                    var local = compiler.lvaTrackedToVarNum[index];
                    Assert.That(compiler.lvaTable[local]._varIndex, Is.EqualTo(index));
                    Assert.That(Call<int>(allocator, "IndexToLocal", index), Is.EqualTo(local));
                    Assert.That(Call<int>(allocator, "LocalToIndex", local), Is.EqualTo(index));
                }

                Assert.That(Call<int>(allocator, "IndexToLocal", trackedCount), Is.EqualTo(BAD_VAR_NUM));
                Assert.That(Call<bool>(allocator, "CanLclVarEscape", 1), Is.True);
                Assert.That(Call<bool>(allocator, "IsLclVarUsed", 1), Is.True);
            });
    }

    [TestCase(false, false, 2)]
    [TestCase(true, false, 0)]
    [TestCase(false, true, 0)]
    public static void PreparationReservesConditionalIndicesOnlyWhenEnabledAndNotOsr(
        bool osr, bool disabledByConfig, int expectedPseudos)
    {
        WithAllocator([TYP_REF, TYP_BYREF, TYP_INT], false, !disabledByConfig, osr,
            (compiler, allocator) => {
                var map = compiler.ImpEnumeratorGdvLocalMap;
                map.Add(compiler.gtNewIconNode(TYP_INT, 1), 0);
                map.Add(compiler.gtNewIconNode(TYP_INT, 2), 1);

                Call(allocator, "PrepareAnalysis");

                Assert.That(Get<int>(allocator, "_maxPseudos"), Is.EqualTo(expectedPseudos));
                Assert.That(Get<int>(allocator, "_firstPseudoIndex"), Is.EqualTo(2 + expectedPseudos));
                Assert.That(Get<int>(allocator, "_unknownSourceIndex"), Is.EqualTo(2 + (2 * expectedPseudos)));
                Assert.That(Get<int>(allocator, "_bvCount"), Is.EqualTo(3 + (2 * expectedPseudos)));
                Assert.That(compiler.lvaTrackedToVarNum, Has.Length.GreaterThanOrEqualTo(3 + expectedPseudos));
                Assert.That(Call<int>(allocator, "IndexToLocal", 2 + expectedPseudos), Is.EqualTo(BAD_VAR_NUM));
            });
    }

    [Test]
    public static void PreparationUsesUshortIndicesAndClosureCrossesBitsetWords()
    {
        var types = new var_types[70];
        Array.Fill(types, TYP_REF);

        WithAllocator(types, false, false, false, (_, allocator) => {
            Call(allocator, "PrepareAnalysis");
            InitializeGraph(allocator);
            Assert.That(Call<int>(allocator, "LocalToIndex", 69), Is.EqualTo(69));

            Call(allocator, "AddConnGraphEdge", 0, 65);
            Call(allocator, "AddConnGraphEdge", 65, 66);
            Call(allocator, "AddConnGraphEdge", 66, 65);
            Call(allocator, "AddConnGraphEdge", 66, 69);
            Call(allocator, "MarkIndexAsUsed", 0);

            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var used = Get<nint[]>(allocator, "_definitelyUsedPointers");
            Call(allocator, "ComputeConnGraphClosure", traits, used, "used");

            ReadOnlySpan<int> reached = [0, 65, 66, 69];
            foreach (var index in reached)
            {
                Assert.That(Call<bool>(allocator, "IsIndexUsed", index), Is.True, $"index {index}");
            }

            Assert.That(Call<bool>(allocator, "IsIndexUsed", 64), Is.False);
            Assert.That(Call<bool>(allocator, "IsIndexUsed", 70), Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void PreparationHonorsConditionalEscapeAndFieldTrackingRanges()
    {
        var conditionalField = typeof(ObjectAllocator).GetField(
            "s_jitObjectStackAllocationConditionalEscapeRange", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing conditional escape range.");
        var fieldsField = typeof(ObjectAllocator).GetField(
            "s_jitObjectStackAllocationTrackFieldsRange", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing field tracking range.");
        var previousConditional = conditionalField.GetValue(null);
        var previousFields = fieldsField.GetValue(null);

        var excludedHash = "ffffffff\0"u8.ToArray();
        fixed (byte* text = excludedHash)
        {
            var range = default(ConfigMethodRange);
            range.EnsureInit(text);
            conditionalField.SetValue(null, range);
            fieldsField.SetValue(null, range);
        }

        try
        {
            WithAllocator([TYP_STRUCT, TYP_REF], true, true, false, (compiler, allocator) => {
                var map = compiler.ImpEnumeratorGdvLocalMap;
                map.Add(compiler.gtNewIconNode(TYP_INT, 1), 1);

                Call(allocator, "PrepareAnalysis");

                // Native range gating follows the initial tracked-local scan.
                Assert.That(compiler.lvaTable[0].lvTracked, Is.True);
                Assert.That(Get<bool>(allocator, "_trackFields"), Is.False);
                Assert.That(Get<int>(allocator, "_maxPseudos"), Is.Zero);
                Assert.That(Get<int>(allocator, "_firstPseudoIndex"), Is.EqualTo(2));
                Assert.That(Get<int>(allocator, "_unknownSourceIndex"), Is.EqualTo(2));
            });
        }
        finally
        {
            conditionalField.SetValue(null, previousConditional);
            fieldsField.SetValue(null, previousFields);
        }
    }
#endif

    [Test]
    public static void PreparationRetainsUnknownSourceWithoutTrackedLocals()
    {
        WithAllocator([TYP_INT], false, false, false, (_, allocator) => {
            Call(allocator, "PrepareAnalysis");
            Assert.That(Get<int>(allocator, "_nextLocalIndex"), Is.Zero);
            Assert.That(Get<int>(allocator, "_firstPseudoIndex"), Is.Zero);
            Assert.That(Get<int>(allocator, "_unknownSourceIndex"), Is.Zero);
            Assert.That(Get<int>(allocator, "_bvCount"), Is.EqualTo(1));
        });
    }

    [Test]
    public static void StackPointersPropagateInIndexOrderAndSubtractUnknownSource()
    {
        WithAllocator([TYP_REF, TYP_REF, TYP_REF, TYP_REF, TYP_REF, TYP_REF, TYP_INT],
            false, false, false, (_, allocator) => {
                Call(allocator, "PrepareAnalysis");
                InitializeGraph(allocator);

                for (var index = 0; index < 5; index++)
                {
                    Call(allocator, "AddConnGraphEdge", index, index + 1);
                }

                var unknownSource = Get<int>(allocator, "_unknownSourceIndex");
                Call(allocator, "AddConnGraphEdgeIndex", 2, unknownSource);
                Call(allocator, "MarkLclVarAsPossiblyStackPointing", 5);
                Call(allocator, "MarkLclVarAsDefinitelyStackPointing", 5);
                Set(allocator, "_analysisDone", true);

                var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
                Call(allocator, "ComputeStackObjectPointers", traits);

                for (var index = 0; index < 6; index++)
                {
                    Assert.That(Call<bool>(allocator, "MayIndexPointToStack", index), Is.True, $"possibly {index}");
                    Assert.That(Call<bool>(allocator, "DoesIndexPointToStack", index),
                        Is.EqualTo(index >= 3), $"definitely {index}");
                }

                Assert.That(Call<bool>(allocator, "MayLclVarPointToStack", 6), Is.False);
                Assert.That(Call<bool>(allocator, "DoesLclVarPointToStack", 6), Is.False);
                Assert.That(Call<bool>(allocator, "MayIndexPointToStack", unknownSource), Is.False);
            });
    }

    [Test]
    public static void MarkHelpersPreserveLocalAndIndexMembership()
    {
        WithAllocator([TYP_REF, TYP_INT, TYP_BYREF], false, false, false, (_, allocator) => {
            Call(allocator, "PrepareAnalysis");
            InitializeGraph(allocator);

            Call(allocator, "MarkLclVarAsEscaping", 2);
            Call(allocator, "MarkIndexAsUsed", 0);
            Call(allocator, "MarkLclVarAsPossiblyStackPointing", 2);
            Call(allocator, "MarkLclVarAsDefinitelyStackPointing", 2);
            Set(allocator, "_analysisDone", true);

            Assert.That(Call<bool>(allocator, "CanLclVarEscape", 0), Is.False);
            Assert.That(Call<bool>(allocator, "CanLclVarEscape", 1), Is.True);
            Assert.That(Call<bool>(allocator, "CanLclVarEscape", 2), Is.True);
            Assert.That(Call<bool>(allocator, "IsLclVarUsed", 0), Is.True);
            Assert.That(Call<bool>(allocator, "IsLclVarUsed", 2), Is.False);
            Assert.That(Call<bool>(allocator, "MayLclVarPointToStack", 2), Is.True);
            Assert.That(Call<bool>(allocator, "DoesLclVarPointToStack", 2), Is.True);
        });
    }

    private static void InitializeGraph(ObjectAllocator allocator)
    {
        var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
        var count = Get<int>(allocator, "_bvCount");
        var adjacency = new nint[count][];

        for (var index = 0; index < count; index++)
        {
            adjacency[index] = BitVecOps.MakeEmpty(traits);
        }

        Set(allocator, "_connGraphAdjacencyMatrix", adjacency);
        Set(allocator, "_escapingPointers", BitVecOps.MakeEmpty(traits));
        Set(allocator, "_definitelyUsedPointers", BitVecOps.MakeEmpty(traits));
        Set(allocator, "_possiblyStackPointingPointers", BitVecOps.MakeEmpty(traits));
        Set(allocator, "_definitelyStackPointingPointers", BitVecOps.MakeEmpty(traits));
    }

    private static void WithAllocator(var_types[] types, bool trackFields, bool conditionalEscape, bool osr,
        Action<Compiler, ObjectAllocator> action)
    {
        var previousConfig = JitConfig;
        object config = previousConfig;
        SetConfig(ref config, "_jitObjectStackAllocationTrackFields", trackFields ? 1 : 0);
        SetConfig(ref config, "_jitObjectStackAllocationConditionalEscape", conditionalEscape ? 1 : 0);
        JitConfig = (JitConfigValues)config;

#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = "ObjectAllocatorGraphTests";
#endif
        compiler.lvaTable = new LclVarDsc[types.Length];
        compiler.lvaCount = types.Length;

        for (var index = 0; index < types.Length; index++)
        {
            compiler.lvaTable[index].Type = types[index];
        }

        JitFlags flags = default;
        if (osr)
        {
            flags.Set(JitFlags.JIT_FLAG_OSR);
        }
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            action(compiler, new ObjectAllocator(compiler));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }

    private static void SetConfig(ref object config, string name, int value)
    {
        var field = typeof(JitConfigValues).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {name}");
        field.SetValue(config, value);
    }

    private static T Get<T>(ObjectAllocator allocator, string fieldName)
    {
        var field = typeof(ObjectAllocator).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {fieldName}");
        return (T)(field.GetValue(allocator) ?? throw new InvalidOperationException($"Uninitialized {fieldName}"));
    }

    private static void Set(ObjectAllocator allocator, string fieldName, object value)
    {
        var field = typeof(ObjectAllocator).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {fieldName}");
        field.SetValue(allocator, value);
    }

    private static void Call(ObjectAllocator allocator, string method, params object[] args)
    {
        _ = Invoke(allocator, method, args);
    }

    private static T Call<T>(ObjectAllocator allocator, string method, params object[] args)
    {
        return (T)(Invoke(allocator, method, args) ?? throw new InvalidOperationException($"No result from {method}"));
    }

    private static object? Invoke(ObjectAllocator allocator, string method, object[] args)
    {
        var member = typeof(ObjectAllocator).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {method}");

        try
        {
            return member.Invoke(allocator, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
