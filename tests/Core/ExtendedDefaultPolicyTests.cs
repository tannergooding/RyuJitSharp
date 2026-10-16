// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
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
    private static IDisposable? s_jitTls;

    [TestCaseSource(nameof(ExplicitCalleeCases))]
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

        using var result = new InlineResult(compiler, call, null, "async candidate", doNotReport: true,
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

    [Test]
    public static void InlinePolicyDataCollectionStateIsExplicitlyUnavailableInRelease()
    {
        var policy = new ProbePolicy(CreateCompiler());

        if (TestConfiguration.IsDebug)
        {
            Assert.That(policy.IsDataCollectionTarget, Is.False);
        }
        else
        {
            _ = Assert.Throws<InvalidOperationException>(() => _ = policy.IsDataCollectionTarget);
        }
    }

    [Test]
    public static void InlinePolicyDebugDiagnosticsAreConditional()
    {
        InlinePolicy policy = new ProbePolicy(CreateCompiler());
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, leaveOpen: true);
        var argumentEvaluated = false;

        StreamWriter GetWriter()
        {
            argumentEvaluated = true;
            return writer;
        }

        policy.DumpXml(GetWriter());
        policy.NotePriorFailure(InlineObservation.CALLSITE_NOT_CANDIDATE);
        writer.Flush();

        Assert.Multiple(() => {
            Assert.That(argumentEvaluated, Is.EqualTo(TestConfiguration.IsDebug));
            Assert.That(stream.Length > 0, Is.EqualTo(TestConfiguration.IsDebug));
            Assert.That(policy.Decision.IsFailure, Is.EqualTo(TestConfiguration.IsDebug));
        });
    }

    [Test]
    public static void InlineStrategyConfigurationIsBuildSpecific()
    {
        object config = Globals.JitConfig;
        SetField(typeof(JitConfigValues), config, "_jitInlineSize", 120);
        SetField(typeof(JitConfigValues), config, "_jitInlineDepth", 40);
        SetField(typeof(JitConfigValues), config, "_jitForceInlineDepth", 15);
        Globals.JitConfig = (JitConfigValues)config;

        var compiler = CreateCompiler();
        compiler.info = new Compiler.Info();
        var strategy = new InlineStrategy(compiler);

        Assert.Multiple(() => {
            Assert.That(GetField<int>(typeof(InlineStrategy), strategy, "_maxInlineSize"),
                Is.EqualTo(TestConfiguration.IsDebug ? 120 : Globals.DEFAULT_MAX_INLINE_SIZE));
            Assert.That(GetField<int>(typeof(InlineStrategy), strategy, "_maxInlineDepth"),
                Is.EqualTo(TestConfiguration.IsDebug ? 40 : Globals.DEFAULT_MAX_INLINE_DEPTH));
            Assert.That(GetField<int>(typeof(InlineStrategy), strategy, "_maxForceInlineDepth"),
                Is.EqualTo(TestConfiguration.IsDebug ? 15 : Globals.DEFAULT_MAX_FORCE_INLINE_DEPTH));
        });
    }

    [Test]
    public static void AdditionalInlineMultiplierIsBuildSpecific()
    {
        object config = Globals.JitConfig;
        SetField(typeof(JitConfigValues), config, "_jitInlineAdditionalMultiplier", 2);
        Globals.JitConfig = (JitConfigValues)config;

        var compiler = CreateCompiler();
        var defaultPolicy = new DefaultPolicyProbe(compiler);
        defaultPolicy.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)InlineCallsiteFrequency.HOT);
        var extendedPolicy = new ProbePolicy(compiler);
        extendedPolicy.NoteBool(InlineObservation.CALLSITE_FOLDABLE_BRANCH, true);
        extendedPolicy.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)InlineCallsiteFrequency.HOT);

        Assert.Multiple(() => {
            Assert.That(defaultPolicy.Multiplier, Is.EqualTo(TestConfiguration.IsDebug ? 5.0 : 3.0).Within(1e-12));
            Assert.That(extendedPolicy.Multiplier, Is.EqualTo(TestConfiguration.IsDebug ? 9.0 : 7.0).Within(1e-12));
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
        var preparationDepth = 0;
        compiler.fgAsyncStressPrepare(++preparationDepth);

        int[] expected = count switch {
            0 => [],
            1 => [0],
            _ => [2, 0, 1],
        };
        var expectedStream = new CLRRandom(1);

        for (var i = count - 1; i > 0; i--)
        {
            if (TestConfiguration.IsDebug)
            {
                _ = expectedStream.Next(i + 1);
            }
        }

        Assert.Multiple(() => {
            for (var i = 0; i < count; i++)
            {
                Assert.That(candidates[i].asyncStressIndex,
                    Is.EqualTo(TestConfiguration.IsDebug ? expected[i] : 0));
            }

            Assert.That(excludedInfo.asyncStressIndex, Is.EqualTo(TestConfiguration.IsDebug ? -1 : 0));
            Assert.That(random.NextDouble(), Is.EqualTo(expectedStream.NextDouble()));
            Assert.That(preparationDepth, Is.EqualTo(TestConfiguration.IsDebug ? 1 : 0));
        });
    }

    [Test]
    public static unsafe void AsyncStressApisAreExplicitlyUnavailableInRelease()
    {
        var compiler = CreateCompiler();
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;

        if (TestConfiguration.IsDebug)
        {
            Assert.Multiple(() => {
                Assert.That(compiler.compAsyncInliningStress(), Is.False);
                Assert.That(compiler.compAsyncInliningStressSeed(), Is.EqualTo(2));
                Assert.That(compiler.compInlineStress(), Is.False);
                Assert.That(compiler.compRandomInlineStress(), Is.False);
                Assert.That(Globals.JitStressLevel, Is.Zero);
                Assert.DoesNotThrow(() => new AsyncStressPolicy(compiler, isPrejitRoot: false));
            });
            return;
        }

        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_compiler", compiler);
        var prepareStress = typeof(Compiler).GetMethod(nameof(Compiler.fgAsyncStressPrepare), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Missing async stress preparation method.");

        Assert.Multiple(() => {
            _ = Assert.Throws<InvalidOperationException>(() => compiler.compAsyncInliningStress());
            _ = Assert.Throws<InvalidOperationException>(() => compiler.compAsyncInliningStressSeed());
            _ = Assert.Throws<InvalidOperationException>(() => compiler.compInlineStress());
            _ = Assert.Throws<InvalidOperationException>(() => compiler.compRandomInlineStress());
            _ = Assert.Throws<InvalidOperationException>(() => _ = Globals.JitStressLevel);
            _ = Assert.Throws<InvalidOperationException>(() => _ = Globals.JitConfig.JitStress);
            _ = Assert.Throws<InvalidOperationException>(() => _ = Globals.JitConfig.JitInlinePolicyRandom);
            _ = Assert.Throws<InvalidOperationException>(() => _ = Globals.JitConfig.JitStressAsyncInlining);
            _ = Assert.Throws<InvalidOperationException>(() => _ = Globals.JitConfig.JitStressAsyncInliningMaxDepth);
            _ = Assert.Throws<InvalidOperationException>(() => _ = Globals.JitConfig.JitStressAsyncInliningPct);
            _ = Assert.Throws<InvalidOperationException>(() => strategy.GetRandom());
            var exception = Assert.Throws<TargetInvocationException>(() => prepareStress.Invoke(compiler, [1]));
            Assert.That(exception?.InnerException, Is.TypeOf<InvalidOperationException>());
            _ = Assert.Throws<InvalidOperationException>(() => new AsyncStressPolicy(compiler, isPrejitRoot: false));
        });
    }

    [TestCaseSource(nameof(AsyncStressSeedCases))]
    public static void AsyncStressSeedUsesSpecificThenGlobalThenDefault(int asyncSeed, int globalStress, int expected)
    {
        object config = Globals.JitConfig;
        SetField(typeof(JitConfigValues), config, "_jitStressAsyncInlining", asyncSeed);
        SetField(typeof(JitConfigValues), config, "_jitStress", globalStress);
        Globals.JitConfig = (JitConfigValues)config;

        Assert.That(CreateCompiler().compAsyncInliningStressSeed(), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(InlineRandomSeedCases))]
    public static void InlineRandomUsesConfiguredSeedAndCachesState(int configuredSeed)
    {
        object config = Globals.JitConfig;
        SetField(typeof(JitConfigValues), config, "_jitInlinePolicyRandom", configuredSeed);
        Globals.JitConfig = (JitConfigValues)config;

        var compiler = CreateCompiler();
        compiler.info = new Compiler.Info { compFullName = "ExtendedDefaultPolicyTests.RandomSeed" };

        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_compiler", compiler);
        var random = strategy.GetRandom(17);
        var expectedSeed = ((configuredSeed != 0) ? configuredSeed : 17) ^ compiler.info.compMethodHash();
        var expected = new CLRRandom(expectedSeed);

        Assert.Multiple(() => {
            Assert.That(random.NextDouble(), Is.EqualTo(expected.NextDouble()));
            Assert.That(strategy.GetRandom(23), Is.SameAs(random));
            Assert.That(random.NextDouble(), Is.EqualTo(expected.NextDouble()));
        });
    }

    [SetUp]
    public static unsafe void SetUp()
    {
        s_previousConfig = Globals.JitConfig;
        s_previousCompiler = JitTls.Compiler;
        s_jitTls = TestConfiguration.CreateJitTls();
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
        s_jitTls?.Dispose();
        s_jitTls = null;
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

    [TestCaseSource(nameof(AsyncStressDefersSizeRejectionCases))]
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

    [TestCaseSource(nameof(AsyncStressSizeLimitCases))]
    public static void AsyncStressRetainsImplementationSizeLimit(int size, InlineObservation expected)
    {
        var policy = new AsyncStressPolicy(CreateCompiler(), isPrejitRoot: true);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, size);

        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(AsyncStressProbabilityCases))]
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
        if (!TestConfiguration.IsDebug)
        {
            Assert.Ignore("Inline XML diagnostics are available only in Debug builds.");
        }

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

    private static IEnumerable<TestCaseData> AsyncStressDefersSizeRejectionCases()
    {
        if (TestConfiguration.IsDebug)
        {
            yield return new TestCaseData(false, false, -1, InlineObservation.CALLEE_TOO_MUCH_IL);
            yield return new TestCaseData(true, false, -1, InlineObservation.CALLEE_TOO_MUCH_IL);
            yield return new TestCaseData(true, true, -1, InlineObservation.CALLEE_IS_PROFITABLE_INLINE);
        }
    }

    private static IEnumerable<TestCaseData> ExplicitCalleeCases()
    {
        if (TestConfiguration.IsDebug)
        {
            yield return new TestCaseData(0, 123);
            yield return new TestCaseData(456, 456);
        }
    }

    private static IEnumerable<TestCaseData> AsyncStressSizeLimitCases()
    {
        if (TestConfiguration.IsDebug)
        {
            yield return new TestCaseData(65535, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);
            yield return new TestCaseData(65536, InlineObservation.CALLEE_TOO_MUCH_IL);
            yield return new TestCaseData(-1, InlineObservation.CALLEE_TOO_MUCH_IL);
        }
    }

    private static IEnumerable<TestCaseData> AsyncStressProbabilityCases()
    {
        if (TestConfiguration.IsDebug)
        {
            yield return new TestCaseData(1, 0, true);
            yield return new TestCaseData(1, 1, true);
            yield return new TestCaseData(1, 2, false);
            yield return new TestCaseData(2, 0, true);
            yield return new TestCaseData(2, 1, false);
            yield return new TestCaseData(3, 0, false);
        }
    }

    private static IEnumerable<TestCaseData> AsyncStressSeedCases()
    {
        if (TestConfiguration.IsDebug)
        {
            yield return new TestCaseData(7, 11, 7);
            yield return new TestCaseData(0, 11, 11);
            yield return new TestCaseData(0, 0, 2);
        }
    }

    private static IEnumerable<TestCaseData> InlineRandomSeedCases()
    {
        if (TestConfiguration.IsDebug)
        {
            yield return new TestCaseData(0);
            yield return new TestCaseData(42);
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static unsafe void InlineContextsPreserveOutcomeBudgetAndDebugAncestry(bool force, bool success)
    {
        var compiler = CreateCompiler();
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_compiler", compiler);
        SetField(typeof(InlineStrategy), strategy, "_currentTimeEstimate", 100);
        SetField(typeof(InlineStrategy), strategy, "_currentTimeBudget", 100);
        SetField(typeof(InlineStrategy), strategy, "_currentSizeEstimate", 500);
        var root = new InlineContext(strategy) { _ilSize = 20 };
        if (TestConfiguration.IsDebug)
        {
            root._ilInstsSet = new System.Collections.BitArray(20, true);
        }
        SetField(typeof(InlineStrategy), strategy, "_rootContext", root);
        var call = new GenTreeCall(var_types.TYP_INT) {
            _inlineContext = root,
            _callMethHnd = (CORINFO_METHOD_STRUCT_*)123,
            SingleInlineCandidateInfo = new InlineCandidateInfo {
                methInfo = new CORINFO_METHOD_INFO { ILCode = (byte*)456, ILCodeSize = 20 },
                ilOffset = 9,
            },
        };
        call.SetIsAsync(default);
        var stmt = new Statement(call, 1);
        stmt.SetDebugInfo(new DebugInfo(root, new ILLocation(5, 0)));
        var context = strategy.NewContext(root, stmt, call);
        Assert.That(root.Child, Is.SameAs(context));
        Assert.That(context.Parent, Is.SameAs(root));
        Assert.That(context.ActualCallOffset, Is.EqualTo(9));
        Assert.That((nuint)context.Callee, Is.EqualTo((nuint)123));
        Assert.That((nuint)context.Code, Is.EqualTo((nuint)456));
        Assert.That(context.ILSize, Is.EqualTo(20));
        if (TestConfiguration.IsDebug)
        {
            Assert.That(context.IsAsyncCall, Is.True);
        }
        var debugInfo = new DebugInfo(context, new ILLocation(17, 0)).GetRoot();
        Assert.That(debugInfo.InlineContext, Is.SameAs(root));
        Assert.That(debugInfo.Location.Offset, Is.EqualTo(5));
        Assert.That(new DebugInfo().GetRoot().IsValid, Is.False);

        var observation = force ? InlineObservation.CALLEE_IS_FORCE_INLINE : InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE;
        var result = (InlineResult)RuntimeHelpers.GetUninitializedObject(typeof(InlineResult));
        SetField(typeof(InlineResult), result, "_policy", new OutcomePolicy(compiler, observation));
        result.ImportedILSize = 20;
        Assert.That(result.ReasonString, Is.EqualTo(observation.String));
        Assert.That(result.ResultString, Is.EqualTo(result.Policy.Decision.String));

        if (success)
        {
            context.SetSucceeded(new InlineInfo { inlineResult = result });
        }
        else
        {
            context.SetFailed(result);
        }

        Assert.That(context.IsSuccess, Is.EqualTo(success));
        Assert.That(context.Observation, Is.EqualTo(observation));
        Assert.That(context.ImportedILSize, Is.EqualTo(20));
        Assert.That(context.CodeSizeEstimate, Is.EqualTo(TestConfiguration.IsDebug ? 30 : 0));
        Assert.That(strategy.InlineCount, Is.EqualTo(success ? 1 : 0));
        Assert.That(context.Ordinal, Is.EqualTo(success ? 1 : 0));
        Assert.That(strategy.BudgetCheck(7), Is.EqualTo(success && !force));
        Assert.That(strategy.CurrentSizeEstimate, Is.EqualTo(TestConfiguration.IsDebug && success ? 530 : 500));
        var sibling = strategy.NewContext(root, stmt, call);
        Assert.That(root.Child, Is.SameAs(sibling));
        Assert.That(sibling.Sibling, Is.SameAs(context));
    }

    private sealed class OutcomePolicy : DefaultPolicy
    {
        public OutcomePolicy(Compiler compiler, InlineObservation observation) : base(compiler, isPrejitRoot: false)
        {
            _observation = observation;
        }

        public override int CodeSizeEstimate() => 30;
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static unsafe void NonInlineCandidateVisitorPreservesPriorFailure(bool priorFailure)
    {
        var compiler = CreateCompiler();
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_compiler", compiler);
        compiler._inlineStrategy = strategy;
        var root = new InlineContext(strategy);
        var call = new GenTreeCall(var_types.TYP_INT) {
            _callType = gtCallTypes.CT_USER_FUNC,
            _inlineContext = root,
            _inlineObservation = priorFailure ? InlineObservation.CALLEE_TOO_MUCH_IL : default,
        };
        var stmt = new Statement(call, 1);
        var visitor = new FindNonInlineCandidateVisitor(compiler, stmt);
        _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        var context = root.Child ?? throw new InvalidOperationException("Missing failed inline context.");
        Assert.That(context.IsSuccess, Is.False);
        Assert.That(context.Observation, Is.EqualTo(priorFailure
            ? InlineObservation.CALLEE_TOO_MUCH_IL
            : InlineObservation.CALLSITE_NOT_CANDIDATE));
        Assert.That(strategy.InlineCount, Is.Zero);
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public static unsafe void InlineDumpPreservesNativeAsyncAnnotations(bool asyncCaller, bool asyncCall, bool verbose)
    {
        var compiler = CreateCompiler();
        JitFlags flags = default;

        if (asyncCaller)
        {
            flags.Set(JitFlags.JIT_FLAG_ASYNC);
        }

        compiler.opts.jitFlags = &flags;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getMethodDefFromMethod = &GetMethodToken;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_compiler", compiler);
        var context = new InlineContext(strategy) {
            _parent = new InlineContext(strategy),
            _flags = asyncCall ? InlineContext.Flags.AsyncCall : InlineContext.Flags.None,
            _treeId = 12,
            _actualCallOffset = 9,
        };
        SetField(typeof(InlineContext), context, "_observation", InlineObservation.CALLEE_IS_NOINLINE);

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = Globals.s_jitstdout;

        try
        {
            Globals.s_jitstdout = writer;
            context.Dump(verbose, 2);
            writer.Flush();
        }
        finally
        {
            Globals.s_jitstdout = previous;
        }

        var asyncness = asyncCaller ? (asyncCall ? " ASYNC" : " SYNC") : "";
        var reason = InlineObservation.CALLEE_IS_NOINLINE.String;
        var expected = verbose
            ? $"  [{Globals.FMT_INL_CTX(0)} IL=0009 TR=000012 06000042] [FAILED: callee: {reason}{asyncness}] <unknown>\r\n"
            : $"  [FAILED: {reason}{asyncness}] <unknown>\r\n";
        Assert.That(System.Text.Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe int GetMethodToken(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method) => 0x06000042;
#endif

    private static void SetField([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type, object target, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {name}.");
        field.SetValue(target, value);
    }

    private static T GetField<T>([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type, object target, string name)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {name}.");
        return (T)(field.GetValue(target) ?? throw new InvalidOperationException($"Missing value for {name}."));
    }

    private static Compiler CreateCompiler()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTable = [];
        return compiler;
    }

    private sealed class DefaultPolicyProbe(Compiler compiler) : DefaultPolicy(compiler, isPrejitRoot: false)
    {
        public double Multiplier => DetermineMultiplier();
    }

    private sealed class ProbePolicy(Compiler compiler, bool isPrejitRoot = false) : ExtendedDefaultPolicy(compiler, isPrejitRoot)
    {
        public double Multiplier => DetermineMultiplier();
    }
}
