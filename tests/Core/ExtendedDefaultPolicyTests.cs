// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
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

#if DEBUG
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
