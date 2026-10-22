// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class FlowEdgeTests
{
    [Test]
    public static void LikelihoodTrackingPreservesItsDebugStateTransitions()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        using var tls = TestConfiguration.CreateJitTlsWithCompiler(compiler);

        var source = new BasicBlock(null, null);
        var destination = new BasicBlock(null, null);
        var edge = new FlowEdge(source, destination, null);

        AssertLikelihoodTracking(edge, expected: false);

        edge.Likelihood = 0.25;
        AssertLikelihoodTracking(edge, expected: true);

        edge.AddLikelihood(0.25);
        Assert.That(edge.Likelihood, Is.EqualTo(0.5));
        AssertLikelihoodTracking(edge, expected: true);
        edge.clearLikelihood();
        AssertLikelihoodTracking(edge, expected: false);
        if (!TestConfiguration.IsDebug)
        {
            Assert.That(
                typeof(FlowEdge).GetField(
                    "_likelihoodSet",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
                Is.Null);
        }

        static void AssertLikelihoodTracking(FlowEdge edge, bool expected)
        {
            if (TestConfiguration.IsDebug)
            {
                Assert.That(edge.hasLikelihood, Is.EqualTo(expected));
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => _ = edge.hasLikelihood);
            }
        }
    }
}
