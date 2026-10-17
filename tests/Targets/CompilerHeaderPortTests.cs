// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerHeaderPortTests
{
    [TestCaseSource(nameof(InstructionAliasCases))]
    public static void InstructionAliasesPreservePinnedTargetOpcodes(instruction alias, instruction opcode)
    {
        Assume.That(Configuration.TARGET_XARCH || Configuration.TARGET_ARM || Configuration.TARGET_ARM64 ||
            Configuration.TARGET_LOONGARCH64);
        Assert.That(alias, Is.EqualTo(opcode));
    }

    private static TestCaseData[] InstructionAliasCases()
    {
        if (Configuration.TARGET_XARCH)
        {
            return
            [
                new(INS_SHIFT_LEFT_LOGICAL, instruction.INS_shl),
                new(INS_SHIFT_RIGHT_LOGICAL, instruction.INS_shr),
                new(INS_SHIFT_RIGHT_ARITHM, instruction.INS_sar),
                new(INS_AND, instruction.INS_and),
                new(INS_OR, instruction.INS_or),
                new(INS_XOR, instruction.INS_xor),
                new(INS_NEG, instruction.INS_neg),
                new(INS_TEST, instruction.INS_test),
                new(INS_MUL, instruction.INS_imul),
                new(INS_SIGNED_DIVIDE, instruction.INS_idiv),
                new(INS_UNSIGNED_DIVIDE, instruction.INS_div),
                new(INS_ADDC, instruction.INS_adc),
                new(INS_SUBC, instruction.INS_sbb),
                new(INS_NOT, instruction.INS_not),
            ];
        }

        if (Configuration.TARGET_ARM)
        {
            return
            [
                new(INS_SHIFT_LEFT_LOGICAL, instruction.INS_lsl),
                new(INS_SHIFT_RIGHT_LOGICAL, instruction.INS_lsr),
                new(INS_SHIFT_RIGHT_ARITHM, instruction.INS_asr),
                new(INS_AND, instruction.INS_and),
                new(INS_OR, instruction.INS_orr),
                new(INS_XOR, instruction.INS_eor),
                new(INS_NEG, instruction.INS_rsb),
                new(INS_TEST, instruction.INS_tst),
                new(INS_MUL, instruction.INS_mul),
                new(INS_MULADD, instruction.INS_mla),
                new(INS_SIGNED_DIVIDE, instruction.INS_sdiv),
                new(INS_UNSIGNED_DIVIDE, instruction.INS_udiv),
                new(INS_ADDC, instruction.INS_adc),
                new(INS_SUBC, instruction.INS_sbc),
                new(INS_NOT, instruction.INS_mvn),
                new(INS_ABS, instruction.INS_vabs),
                new(INS_SQRT, instruction.INS_vsqrt),
            ];
        }

        if (Configuration.TARGET_ARM64)
        {
            return
            [
                new(INS_MULADD, instruction.INS_madd),
                new(INS_ABS, instruction.INS_fabs),
                new(INS_SQRT, instruction.INS_fsqrt),
            ];
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return
            [
                new(INS_MULADD, instruction.INS_fmadd_d),
                new(INS_ABS, instruction.INS_fabs_d),
                new(INS_SQRT, instruction.INS_fsqrt_d),
            ];
        }

        return [];
    }

    [TestCase(false, -1)]
    [TestCase(false, 17)]
    [TestCase(true, -1)]
    [TestCase(true, 17)]
    public static unsafe void ShadowParameterPrintPreservesPointerAndSignedLocalFormat(bool hasGroup, int shadowCopy)
    {
        Assume.That(TestConfiguration.IsDebug);

        var info = new Compiler.ShadowParamVarInfo {
            AssignGroup = hasGroup ? [1, 2] : null,
            ShadowCopy = shadowCopy,
        };
        var originalOutput = s_jitstdout;
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);

        try
        {
            s_jitstdout = writer;
            info.Print();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = originalOutput;
        }

        var text = Encoding.UTF8.GetString(stream.ToArray()).Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        var pointerDigits = FMT_PTR(null).Length;
        Assert.That(text, Does.Match(
            $"\\AassignGroup \\[[0-9A-F]{{{pointerDigits}}}\\]; shadowCopy: \\[{shadowCopy}\\];\\n\\z"));
        Assert.That(text.StartsWith($"assignGroup [{new string('0', pointerDigits)}]", StringComparison.Ordinal),
            Is.EqualTo(!hasGroup));
        Assert.That(info.ShadowCopy, Is.EqualTo(shadowCopy));
        Assert.That(info.AssignGroup, hasGroup ? Is.EqualTo(new nint[] { 1, 2 }) : Is.Null);
    }
}
