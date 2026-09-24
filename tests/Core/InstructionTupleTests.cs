// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insTupleType;

namespace RyuJitSharp.UnitTests;

internal static class InstructionTupleTests
{
    [TestCase(INS_add, INS_TT_NONE)]
    [TestCase(INS_addps, INS_TT_FULL)]
    [TestCase(INS_pmovsxbw, INS_TT_HALF_MEM)]
    [TestCase(INS_pmovsxbd, INS_TT_QUARTER_MEM)]
    [TestCase(INS_pmovsxbq, INS_TT_EIGHTH_MEM)]
    [TestCase(INS_pslld, INS_TT_FULL | INS_TT_MEM128)]
    [TestCase(INS_psllw, INS_TT_FULL_MEM | INS_TT_MEM128)]
    public static void TupleMetadataPreservesNativeInstructionIdentity(instruction ins, insTupleType expected)
    {
        Assert.That(Emitter.insTupleTypeInfo(ins), Is.EqualTo(expected));
    }

    [Test]
    public static void TupleMetadataCoversEveryInstruction()
    {
        for (instruction ins = 0; ins < INS_count; ins++)
        {
            _ = Emitter.insTupleTypeInfo(ins);
        }
    }
}
