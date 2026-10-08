// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG || TARGET_XARCH || TARGET_ARM || TARGET_ARM64 || TARGET_LOONGARCH64
#if DEBUG
using System;
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
#if TARGET_XARCH || TARGET_ARM || TARGET_ARM64 || TARGET_LOONGARCH64
using static RyuJitSharp.instruction;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerHeaderPortTests
{
#if TARGET_XARCH
    [TestCase(INS_SHIFT_LEFT_LOGICAL, INS_shl)]
    [TestCase(INS_SHIFT_RIGHT_LOGICAL, INS_shr)]
    [TestCase(INS_SHIFT_RIGHT_ARITHM, INS_sar)]
    [TestCase(INS_AND, INS_and)]
    [TestCase(INS_OR, INS_or)]
    [TestCase(INS_XOR, INS_xor)]
    [TestCase(INS_NEG, INS_neg)]
    [TestCase(INS_TEST, INS_test)]
    [TestCase(INS_MUL, INS_imul)]
    [TestCase(INS_SIGNED_DIVIDE, INS_idiv)]
    [TestCase(INS_UNSIGNED_DIVIDE, INS_div)]
    [TestCase(INS_ADDC, INS_adc)]
    [TestCase(INS_SUBC, INS_sbb)]
    [TestCase(INS_NOT, INS_not)]
#elif TARGET_ARM
    [TestCase(INS_SHIFT_LEFT_LOGICAL, INS_lsl)]
    [TestCase(INS_SHIFT_RIGHT_LOGICAL, INS_lsr)]
    [TestCase(INS_SHIFT_RIGHT_ARITHM, INS_asr)]
    [TestCase(INS_AND, INS_and)]
    [TestCase(INS_OR, INS_orr)]
    [TestCase(INS_XOR, INS_eor)]
    [TestCase(INS_NEG, INS_rsb)]
    [TestCase(INS_TEST, INS_tst)]
    [TestCase(INS_MUL, INS_mul)]
    [TestCase(INS_MULADD, INS_mla)]
    [TestCase(INS_SIGNED_DIVIDE, INS_sdiv)]
    [TestCase(INS_UNSIGNED_DIVIDE, INS_udiv)]
    [TestCase(INS_ADDC, INS_adc)]
    [TestCase(INS_SUBC, INS_sbc)]
    [TestCase(INS_NOT, INS_mvn)]
    [TestCase(INS_ABS, INS_vabs)]
    [TestCase(INS_SQRT, INS_vsqrt)]
#elif TARGET_ARM64
    [TestCase(INS_MULADD, INS_madd)]
    [TestCase(INS_ABS, INS_fabs)]
    [TestCase(INS_SQRT, INS_fsqrt)]
#elif TARGET_LOONGARCH64
    [TestCase(INS_MULADD, INS_fmadd_d)]
    [TestCase(INS_ABS, INS_fabs_d)]
    [TestCase(INS_SQRT, INS_fsqrt_d)]
#endif
#if TARGET_XARCH || TARGET_ARM || TARGET_ARM64 || TARGET_LOONGARCH64
    public static void InstructionAliasesPreservePinnedTargetOpcodes(instruction alias, instruction opcode)
    {
        Assert.That(alias, Is.EqualTo(opcode));
    }
#endif

#if DEBUG
    [TestCase(false, -1)]
    [TestCase(false, 17)]
    [TestCase(true, -1)]
    [TestCase(true, 17)]
    public static unsafe void ShadowParameterPrintPreservesPointerAndSignedLocalFormat(bool hasGroup, int shadowCopy)
    {
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
#endif
}
#endif
