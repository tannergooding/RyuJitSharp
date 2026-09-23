// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class ExtendedDefaultPolicyTests
{
    private static JitConfigValues s_previousConfig;
    private static Compiler? s_previousCompiler;
#if DEBUG
    private static JitTls? s_jitTls;

    [TestCase(0, 123)]
    [TestCase(456, 456)]
    public static unsafe void CallResultPreservesExplicitCalleeAndAsyncGroup(int explicitCallee, int expectedCallee)
    {
        object config = Globals.JitConfig;
        SetField(typeof(JitConfigValues), config, "_jitStressAsyncInlining", 1);
        Globals.JitConfig = (JitConfigValues)config;

        var compiler = CreateCompiler();
        var call = new GenTreeCall(var_types.TYP_INT) {
            _callType = gtCallTypes.CT_USER_FUNC,
            _callMethHnd = (CORINFO_METHOD_STRUCT_*)123,
        };
        call.SetIsAsync(default);
        call.SingleInlineCandidateInfo = new InlineCandidateInfo { asyncStressIndex = 2 };

        var result = new InlineResult(compiler, call, null, "async candidate", doNotReport: true,
            callee: (CORINFO_METHOD_STRUCT_*)explicitCallee);
        result.Policy.NoteBool(InlineObservation.CALLEE_IS_ASYNC, true);
        var calleeField = typeof(InlineResult).GetField("_callee", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing callee field.");
        var callee = calleeField.GetValue(result) ?? throw new InvalidOperationException("Missing boxed callee.");
        Assert.Multiple(() => {
            Assert.That((nuint)System.Reflection.Pointer.Unbox(callee), Is.EqualTo((nuint)expectedCallee));
            Assert.That(result.Policy, Is.TypeOf<AsyncStressPolicy>());
            Assert.That(result.Policy.BudgetCheck(), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public static void AsyncGroupingUsesNativeShuffleAndExcludesNonCandidates(int count)
    {
        object config = Globals.JitConfig;
        SetField(typeof(JitConfigValues), config, "_jitStressAsyncInlining", 1);
        Globals.JitConfig = (JitConfigValues)config;

        var compiler = CreateCompiler();
        var random = new CLRRandom(1);
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_random", random);
        compiler._inlineStrategy = strategy;
        var block = new BasicBlock(null, null);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;

        var excludedCall = new GenTreeCall(var_types.TYP_INT);
        var excludedInfo = new InlineCandidateInfo();
        excludedCall.SingleInlineCandidateInfo = excludedInfo;
        var first = new Statement(excludedCall, 1);
        SetField(typeof(BasicBlock), block, "_stmtList", first);
        var previous = first;
        var candidates = new InlineCandidateInfo[count];
        for (var i = 0; i < count; i++)
        {
            var call = new GenTreeCall(var_types.TYP_INT);
            call.SetIsAsync(default);
            candidates[i] = new InlineCandidateInfo();
            call.SingleInlineCandidateInfo = candidates[i];
            var stmt = new Statement(call, i + 2);
            previous.NextStmt = stmt;
            stmt.PrevStmt = previous;
            previous = stmt;
        }
        first.PrevStmt = previous;
        compiler.fgAsyncStressPrepare(1);

        int[] expected = count switch {
            0 => [],
            1 => [0],
            _ => [2, 0, 1],
        };
        var expectedStream = new CLRRandom(1);
        for (var i = count - 1; i > 0; i--)
        {
            _ = expectedStream.Next(i + 1);
        }
        Assert.Multiple(() => {
            for (var i = 0; i < count; i++)
            {
                Assert.That(candidates[i].asyncStressIndex, Is.EqualTo(expected[i]));
            }
            Assert.That(excludedInfo.asyncStressIndex, Is.EqualTo(-1));
            Assert.That(random.NextDouble(), Is.EqualTo(expectedStream.NextDouble()));
        });
    }

#endif

    [SetUp]
    public static unsafe void SetUp()
    {
        s_previousConfig = Globals.JitConfig;
        s_previousCompiler = JitTls.Compiler;
#if DEBUG
        s_jitTls = new JitTls(null);
#endif
        JitTls.Compiler = CreateCompiler();
        object config = new JitConfigValues();
        SetConfig("_jitExtDefaultPolicyMaxIL", 128);
        SetConfig("_jitExtDefaultPolicyMaxILRoot", 256);
        SetConfig("_jitExtDefaultPolicyMaxILProf", 1024);
        SetConfig("_jitExtDefaultPolicyMaxBB", 7);
        SetConfig("_jitExtDefaultPolicyProfTrust", 7);
        SetConfig("_jitExtDefaultPolicyProfScale", 42);
        SetConfig("_jitMaxLocalsToTrack", 1024);
        Globals.JitConfig = (JitConfigValues)config;

        void SetConfig(string name, int value)
        {
            var field = typeof(JitConfigValues).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing config field {name}.");
            field.SetValue(config, value);
        }
    }

    [TearDown]
    public static void TearDown()
    {
        Globals.JitConfig = s_previousConfig;
        JitTls.Compiler = s_previousCompiler;
#if DEBUG
        s_jitTls?.Dispose();
        s_jitTls = null;
#endif
    }

    [TestCase(100, 0, 0, 100)]
    [TestCase(100, 1, 0, 65)]
    [TestCase(100, 0, 1, 30)]
    [TestCase(100, 3, 0, 30)]
    [TestCase(19, 1, 1, 5)]
    public static void EstimatedSizeAccountsForFolding(int size, int branches, int switches, int expected)
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, true);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, size);
        for (var i = 0; i < branches; i++)
        {
            policy.NoteBool(InlineObservation.CALLSITE_FOLDABLE_BRANCH, true);
        }
        for (var i = 0; i < switches; i++)
        {
            policy.NoteBool(InlineObservation.CALLSITE_FOLDABLE_SWITCH, true);
        }

        Assert.Multiple(() => {
            Assert.That(policy.RequiresPreciseScan, Is.True);
            Assert.That(policy.EstimatedTotalILSize, Is.EqualTo(expected));
        });
    }

    [TestCase(16, false, false, InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE)]
    [TestCase(17, false, false, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(128, false, false, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(129, false, false, InlineObservation.CALLEE_TOO_MUCH_IL)]
    [TestCase(8, true, false, InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE)]
    [TestCase(9, true, false, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(10, true, false, InlineObservation.CALLEE_TOO_MUCH_IL)]
    [TestCase(1024, false, true, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(1025, false, true, InlineObservation.CALLEE_TOO_MUCH_IL)]
    public static void CandidateSizeLimitsMatchNative(int size, bool inThrow, bool trustedProfile, InlineObservation expected)
    {
        var compiler = CreateCompiler();
        compiler.fgPgoHaveWeights = trustedProfile;
        compiler.fgPgoSource = ICorJitInfo.PgoSource.Dynamic;
        var policy = new ProbePolicy(compiler);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteBool(InlineObservation.CALLSITE_INSIDE_THROW_BLOCK, inThrow);
        policy.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, trustedProfile);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, size);

        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

    [TestCase(false, 9, false)]
    [TestCase(false, 10, true)]
    [TestCase(true, 14, false)]
    [TestCase(true, 15, true)]
    public static void FoldableBranchesRaiseBlockLimit(bool isPrejitRoot, int blocks, bool rejected)
    {
        var policy = new ProbePolicy(CreateCompiler(), isPrejitRoot);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 100);
        policy.NoteBool(InlineObservation.CALLSITE_FOLDABLE_BRANCH, true);
        policy.NoteBool(InlineObservation.CALLSITE_FOLDABLE_BRANCH, true);
        policy.NoteInt(InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS, blocks);

        Assert.That(policy.Observation, Is.EqualTo(rejected
            ? InlineObservation.CALLEE_TOO_MANY_BASIC_BLOCKS
            : InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE));
    }

    [TestCase(ICorJitInfo.PgoSource.Dynamic, false, true)]
    [TestCase(ICorJitInfo.PgoSource.Blend, false, true)]
    [TestCase(ICorJitInfo.PgoSource.Text, false, true)]
    [TestCase(ICorJitInfo.PgoSource.Static, false, false)]
    [TestCase(ICorJitInfo.PgoSource.Synthesis, false, false)]
    [TestCase(ICorJitInfo.PgoSource.Synthesis, true, true)]
    public static void ProfileTrustDistinguishesStaticAndSynthesis(ICorJitInfo.PgoSource source, bool singleEdge, bool expected)
    {
        var compiler = CreateCompiler();
        compiler.fgPgoSource = source;
        compiler.fgPgoSingleEdge = singleEdge;
        Assert.That(compiler.fgHaveTrustedProfileWeights, Is.False);
        compiler.fgPgoHaveWeights = true;
        Assert.That(compiler.fgHaveTrustedProfileWeights, Is.EqualTo(expected));
    }

    [TestCase(InlineCallsiteFrequency.BORING, false, 5.3)]
    [TestCase(InlineCallsiteFrequency.RARE, false, 1.3)]
    [TestCase(InlineCallsiteFrequency.HOT, false, 7.0)]
    [TestCase(InlineCallsiteFrequency.HOT, true, 1.0)]
    public static void FrequencyAndNoReturnOverridesPreserveOrdering(InlineCallsiteFrequency frequency, bool noReturn, double expected)
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.NoteBool(InlineObservation.CALLSITE_FOLDABLE_BRANCH, true);
        policy.NoteBool(InlineObservation.CALLSITE_IN_NORETURN_REGION, noReturn);
        policy.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)frequency);

        Assert.That(policy.Multiplier, Is.EqualTo(expected).Within(1e-12));
    }

    [TestCase(false, false, 0.0, 0, 0.0)]
    [TestCase(true, false, 0.0, 0, 0.9)]
    [TestCase(true, true, 0.0, 0, 3.0)]
    [TestCase(true, false, 0.5, 512, 3.6)]
    [TestCase(true, false, 2.0, 1024, 0.0)]
    public static void ProfileAndAllocatedLocalCapacityScaleBenefit(bool trusted, bool intrinsicType, double frequency, int capacity, double expected)
    {
        var compiler = CreateCompiler();
        compiler.fgPgoHaveWeights = trusted;
        compiler.fgPgoSource = ICorJitInfo.PgoSource.Dynamic;
        compiler.lvaTable = new LclVarDsc[capacity];
        compiler.lvaCount = 0;
        var policy = new ProbePolicy(compiler);
        policy.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, true);
        policy.NoteBool(InlineObservation.CALLEE_IS_INTRINSIC_TYPE, intrinsicType);
        policy.NoteDouble(InlineObservation.CALLSITE_PROFILE_FREQUENCY, frequency);
        policy.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)InlineCallsiteFrequency.HOT);

        Assert.That(policy.Multiplier, Is.EqualTo(expected).Within(1e-12));
    }

    [Test]
    public static void ProfileFrequencyRetainsNativeNanSemantics()
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, true);
        policy.NoteDouble(InlineObservation.CALLSITE_PROFILE_FREQUENCY, double.NaN);
        policy.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)InlineCallsiteFrequency.HOT);

        Assert.That(double.IsNaN(policy.Multiplier), Is.True);
    }

    [Test]
    public static void CallStoragePreservesManagedAsyncDebugInfoWhenCopied()
    {
        var context = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
        var info = new AsyncCallInfo { CallAsyncDebugInfo = new DebugInfo(context, default) };
        var call = new GenTreeCall(var_types.TYP_INT);
        call.SetIsAsync(info);
        var copy = new GenTreeCall(var_types.TYP_INT) { _anonymous1 = call._anonymous1 };

        Assert.That(copy._asyncInfo.CallAsyncDebugInfo.InlineContext, Is.SameAs(context));
    }

    [TestCase(1)]
    [TestCase(int.MinValue)]
    public static void InlineRandomPreservesNativeSeedAndNegativeRangeRounding(int seed)
    {
        Assert.Multiple(() => {
            Assert.That(new CLRRandom(seed).Next(), Is.EqualTo(534011718));
            Assert.That(new CLRRandom(seed).Next(-13, 17), Is.EqualTo(-5));
        });
    }

#if DEBUG
    [TestCase(false, false, -1, InlineObservation.CALLEE_TOO_MUCH_IL)]
    [TestCase(true, false, -1, InlineObservation.CALLEE_TOO_MUCH_IL)]
    [TestCase(true, true, -1, InlineObservation.CALLEE_IS_PROFITABLE_INLINE)]
    public static void AsyncStressDefersSizeRejectionUntilSelection(bool isAsync, bool isPrejitRoot, int index, InlineObservation expected)
    {
        var policy = new AsyncStressPolicy(CreateCompiler(), isPrejitRoot);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 200);
        Assert.That(policy.Observation, Is.EqualTo(InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE));
        policy.NoteBool(InlineObservation.CALLEE_IS_ASYNC, isAsync);
        policy.NoteInt(InlineObservation.CALLSITE_ASYNC_STRESS_INDEX, index);
        policy.DetermineProfitability(default);

        Assert.That(policy.Observation, Is.EqualTo(expected));
        if (isAsync && isPrejitRoot)
        {
            Assert.That(policy.BudgetCheck(), Is.False);
        }
    }

    [TestCase(65535, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(65536, InlineObservation.CALLEE_TOO_MUCH_IL)]
    [TestCase(-1, InlineObservation.CALLEE_TOO_MUCH_IL)]
    public static void AsyncStressRetainsImplementationSizeLimit(int size, InlineObservation expected)
    {
        var policy = new AsyncStressPolicy(CreateCompiler(), isPrejitRoot: true);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, size);

        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

    [TestCase(1, 0, true)]
    [TestCase(1, 1, true)]
    [TestCase(1, 2, false)]
    [TestCase(2, 0, true)]
    [TestCase(2, 1, false)]
    [TestCase(3, 0, false)]
    public static void AsyncStressProbabilityUsesDepthAndGroupIndex(int depth, int index, bool accepted)
    {
        object config = Globals.JitConfig;
        SetField(typeof(JitConfigValues), config, "_jitStressAsyncInliningMaxDepth", 2);
        SetField(typeof(JitConfigValues), config, "_jitStressAsyncInliningPct", 50);
        Globals.JitConfig = (JitConfigValues)config;

        var compiler = CreateCompiler();
        var random = new CLRRandom(1);
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_random", random);
        SetField(typeof(InlineStrategy), strategy, "_maxInlineDepth", 20);
        compiler._inlineStrategy = strategy;
        var policy = new AsyncStressPolicy(compiler, isPrejitRoot: false);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 200);
        policy.NoteBool(InlineObservation.CALLEE_IS_ASYNC, true);
        policy.NoteInt(InlineObservation.CALLSITE_DEPTH, depth);
        policy.NoteInt(InlineObservation.CALLSITE_ASYNC_STRESS_INDEX, index);
        policy.DetermineProfitability(default);

        var expectedStream = new CLRRandom(1);
        if (depth <= 2)
        {
            _ = expectedStream.NextDouble();
        }
        Assert.Multiple(() => {
            Assert.That(policy.Observation, Is.EqualTo(accepted
                ? InlineObservation.CALLSITE_RANDOM_ACCEPT : InlineObservation.CALLSITE_RANDOM_REJECT));
            Assert.That(policy.BudgetCheck(), Is.False);
            Assert.That(random.NextDouble(), Is.EqualTo(expectedStream.NextDouble()));
        });
    }

    [Test]
    public static void XmlUsesNativeAttributeNamesAndInvariantNumbers()
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, true);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 100);
        policy.NoteBool(InlineObservation.CALLSITE_FOLDABLE_BRANCH, true);
        policy.NoteDouble(InlineObservation.CALLSITE_PROFILE_FREQUENCY, 0.25);
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, leaveOpen: true);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            policy.OnDumpXml(writer);
            writer.Flush();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        Assert.That(reader.ReadToEnd(), Is.EqualTo(
            " m_CodeSize=\"100\" m_IsForceInline=\"True\" m_IsForceInlineKnown=\"True\"" +
            " m_ProfileFrequency=\"0.25\" m_FoldableBranch=\"1\""));
    }
#endif

    private static void SetField([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type, object target, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {name}.");
        field.SetValue(target, value);
    }

    private static Compiler CreateCompiler()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTable = [];
        return compiler;
    }

    private sealed class ProbePolicy(Compiler compiler, bool isPrejitRoot = false) : ExtendedDefaultPolicy(compiler, isPrejitRoot)
    {
        public double Multiplier => DetermineMultiplier();
    }
}
