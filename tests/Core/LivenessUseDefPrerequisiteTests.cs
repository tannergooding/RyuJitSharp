// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using TrackedSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LivenessUseDefPrerequisiteTests
{
    [Test]
    public static void BlocksWithoutTrackedLocalsAcceptEmptyUseDefAssignments()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            var block = new BasicBlock(null, null);
            block.InitVarSets(compiler);
            var uses = TrackedSetOps.MakeEmpty(compiler);
            var definitions = TrackedSetOps.MakeEmpty(compiler);

            TrackedSetOps.Assign(compiler, ref block.bbVarUse, uses);
            TrackedSetOps.Assign(compiler, ref block.bbVarDef, definitions);

            Assert.That(block.bbVarUse, Is.Empty);
            Assert.That(block.bbVarDef, Is.Empty);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
