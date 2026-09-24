// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp.UnitTests;

internal static class ConditionDescriptorTests
{
    [Test]
    public static void EveryValidConditionHasACompleteDescriptor()
    {
        Assert.That((int)EJ_COUNT, Is.EqualTo(18));
        for (var value = 2; value < 32; value++)
        {
            var desc = GenConditionDesc.Get(new GenCondition((GenCondition.CodeKind)value));
            Assert.That(desc.JumpKind1, Is.Not.EqualTo(EJ_NONE));
            Assert.That(desc.Oper is GT_NONE or GT_AND or GT_OR, Is.True);
            Assert.That(desc.Oper is GT_NONE, Is.EqualTo(desc.JumpKind2 is EJ_NONE));
        }
    }

    [TestCase(GenCondition.SLT, EJ_jl)]
    [TestCase(GenCondition.C, EJ_jb)]
    [TestCase(GenCondition.O, EJ_jo)]
    public static void IntegerAndFlagConditionsSelectTheirNativeJump(GenCondition.CodeKind code, emitJumpKind jump)
    {
        var desc = GenConditionDesc.Get(new GenCondition(code));
        Assert.That(desc.JumpKind1, Is.EqualTo(jump));
        Assert.That(desc.Oper, Is.EqualTo(GT_NONE));
        Assert.That(desc.JumpKind2, Is.EqualTo(EJ_NONE));
    }

    [Test]
    public static void FloatingJumpCombinationsImplementOrderedAndUnorderedTruthTables()
    {
        ReadOnlySpan<GenCondition.CodeKind> codes = [
            GenCondition.FEQ, GenCondition.FNE, GenCondition.FLT, GenCondition.FLE, GenCondition.FGE, GenCondition.FGT,
            GenCondition.FEQU, GenCondition.FNEU, GenCondition.FLTU, GenCondition.FLEU, GenCondition.FGEU, GenCondition.FGTU,
        ];
        // Result bits correspond to unordered, greater, less and equal.
        ReadOnlySpan<int> expected = [
            0b1000, 0b0110, 0b0100, 0b1100, 0b1010, 0b0010,
            0b1001, 0b0111, 0b0101, 0b1101, 0b1011, 0b0011,
        ];
        // ZF occupies bit 0, PF bit 1, and CF bit 2.
        ReadOnlySpan<int> flags = [0b111, 0b000, 0b100, 0b001];
        for (var index = 0; index < codes.Length; index++)
        {
            var condition = new GenCondition(codes[index]);
            var desc = GenConditionDesc.Get(condition);
            var actual = 0;
            for (var outcome = 0; outcome < flags.Length; outcome++)
            {
                var first = EvaluateJump(desc.JumpKind1, flags[outcome]);
                var result = desc.Oper switch {
                    GT_NONE => first,
                    GT_AND => first && EvaluateJump(desc.JumpKind2, flags[outcome]),
                    GT_OR => first || EvaluateJump(desc.JumpKind2, flags[outcome]),
                    _ => throw new InvalidOperationException(),
                };
                if (result)
                {
                    actual |= 1 << outcome;
                }
            }

            Assert.That(actual, Is.EqualTo(expected[index]), condition.Name);
        }
    }

    private static bool EvaluateJump(emitJumpKind jump, int flags)
    {
        return jump switch {
            EJ_je => (flags & 1) != 0,
            EJ_jne => (flags & 1) == 0,
            EJ_jb => (flags & 4) != 0,
            EJ_jbe => (flags & 5) != 0,
            EJ_jae => (flags & 4) == 0,
            EJ_ja => (flags & 5) == 0,
            EJ_jp => (flags & 2) != 0,
            EJ_jnp => (flags & 2) == 0,
            _ => throw new InvalidOperationException(),
        };
    }
}
