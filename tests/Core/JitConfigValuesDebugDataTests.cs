using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[TestFixture]
internal static class JitConfigValuesDebugDataTests
{
    [Test]
    public static void DebugDataWritesDetachCopiedConfig()
    {
        Assume.That(Configuration.DEBUG);
        Assert.That(
            typeof(Compiler.DebugData).GetNestedType("JitConfigValuesData", System.Reflection.BindingFlags.NonPublic),
            Is.Not.Null);

        var original = new JitConfigValues();
        TestConfiguration.GetJitConfigDebugDataForWrite(ref original)._jitSsaStress = 1;

        var copy = original;
        var field = TestConfiguration.GetJitConfigField("_jitSsaStress")
            ?? throw new InvalidOperationException("Missing debug configuration field.");
        Assert.That(field.GetValue(copy), Is.EqualTo(1));

        TestConfiguration.GetJitConfigDebugDataForWrite(ref copy)._jitSsaStress = 2;

        Assert.Multiple(() =>
        {
            Assert.That(field.GetValue(original), Is.EqualTo(1));
            Assert.That(field.GetValue(copy), Is.EqualTo(2));
        });
    }

    [Test]
    public static void ConfigurationSidecarWritesDetachCopiedConfig()
    {
        var fieldNames = new List<string>();
        foreach (var candidate in new[]
        {
            "_jitDisableSimdVN",
            "_enableAVX",
            "_enableArm64Sve",
            "_enableRiscV64Zba",
            "_jitWasmFunclets",
        })
        {
            if (TestConfiguration.GetJitConfigField(candidate) is not null)
            {
                fieldNames.Add(candidate);
            }
        }

        Assume.That(fieldNames.Count, Is.GreaterThan(0), "This target has no generated configuration sidecar field.");
        foreach (var fieldName in fieldNames)
        {
            var original = new JitConfigValues();
            TestConfiguration.SetJitConfigValue(ref original, fieldName, 1);

            var copy = original;
            TestConfiguration.SetJitConfigValue(ref copy, fieldName, 2);
            var field = TestConfiguration.GetJitConfigField(fieldName)
                ?? throw new InvalidOperationException($"Missing configuration sidecar field '{fieldName}'.");

            Assert.Multiple(() =>
            {
                Assert.That(field.GetValue(original), Is.EqualTo(1), fieldName);
                Assert.That(field.GetValue(copy), Is.EqualTo(2), fieldName);
            });
        }
    }

    [Test]
    public static void FeatureConfigurationSidecarIsInactiveWhenFeatureIsDisabled()
    {
        Assume.That(!Configuration.FEATURE_SIMD);

        Assert.That(TestConfiguration.GetJitConfigField("_jitDisableSimdVN"), Is.Null);
        Assert.That(new JitConfigValues().JitDisableSimdVN, Is.Zero);
    }

    [Test]
    public static void ReleaseLayoutKeepsOnlyOneSidecarReference()
    {
        Assume.That(!Configuration.DEBUG);
        Assume.That(Configuration.TARGET_64BIT);

        Assert.That(Unsafe.SizeOf<JitConfigValues>(), Is.EqualTo(488));
    }
}
