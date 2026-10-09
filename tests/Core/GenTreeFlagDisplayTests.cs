// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using NUnit.Framework;
using static RyuJitSharp.GenTreeDebugFlags;
using static RyuJitSharp.GenTreeFlags;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class GenTreeFlagDisplayTests
{
    [TestCase(GTF_EMPTY, "----------")]
    [TestCase(GTF_ASG, "A---------")]
    [TestCase(GTF_CONTAINED, "c---------")]
    [TestCase(GTF_ASG | GTF_CONTAINED, "A---------")]
    [TestCase(GTF_CALL, "-C--------")]
    [TestCase(GTF_EXCEPT, "--X-------")]
    [TestCase(GTF_GLOB_REF, "---G------")]
    [TestCase(GTF_ORDER_SIDEEFF, "----O-----")]
    [TestCase(GTF_COLON_COND, "-----?----")]
    [TestCase(GTF_DONT_CSE, "------N---")]
    [TestCase(GTF_MAKE_CSE, "------H---")]
    [TestCase(GTF_DONT_CSE | GTF_MAKE_CSE, "------N---")]
    [TestCase(GTF_REVERSE_OPS, "-------R--")]
    [TestCase(GTF_UNSIGNED, "--------U-")]
    [TestCase(GTF_SPILL, "---------Z")]
    [TestCase(GTF_SPILLED, "---------z")]
    [TestCase(GTF_SPILL | GTF_SPILLED, "---------#")]
    [TestCase(GTF_ASG | GTF_CALL | GTF_EXCEPT | GTF_GLOB_REF | GTF_ORDER_SIDEEFF | GTF_COLON_COND |
        GTF_DONT_CSE | GTF_MAKE_CSE | GTF_REVERSE_OPS | GTF_UNSIGNED | GTF_SPILL | GTF_SPILLED, "ACXGO?NRU#")]
    public static void FlagDisplayMatchesNativeColumnsAndWidth(GenTreeFlags flags, string expected)
    {
        var displayed = 0;
        var output = CodeGenLifeTransitionTests.Capture(() => displayed = GenTree.gtDispFlags(flags));

        Assert.That(output, Is.EqualTo(expected));
        Assert.That(displayed, Is.EqualTo(10));
        Assert.That(output.Length, Is.EqualTo(displayed));
    }

    [Test]
    public static void NodeDebugMaskContainsExactlyTheRetainedNodeProperties()
    {
        var nodeFlags = GTF_DEBUG_NODE_SMALL | GTF_DEBUG_NODE_LARGE | GTF_DEBUG_NODE_CG_PRODUCED |
            GTF_DEBUG_NODE_CG_CONSUMED | GTF_DEBUG_NODE_LSRA_ADDED;

        Assert.That(GTF_DEBUG_NODE_MASK, Is.EqualTo(nodeFlags));
        Assert.That((ushort)GTF_DEBUG_NODE_MASK, Is.EqualTo(0x003E));
        Assert.That(GTF_DEBUG_NODE_MASK & GTF_DEBUG_CAST_DONT_FOLD, Is.EqualTo(GTF_DEBUG_NONE));
    }
}
#endif
