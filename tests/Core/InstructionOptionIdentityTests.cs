using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class InstructionOptionIdentityTests
{
    [Test]
    public static void InstructionOptionsRetainTargetNativeValues()
    {
        Assert.That(Enum.GetUnderlyingType(typeof(insOpts)), Is.EqualTo(typeof(uint)));
        AssertOption(insOpts.INS_OPTS_NONE, 0);

        if (Configuration.TARGET_XARCH)
        {
            AssertOption(insOpts.INS_OPTS_EVEX_b_MASK, 0x03);
            AssertOption(insOpts.INS_OPTS_EVEX_em_k7, 7 << 2);
            AssertOption(insOpts.INS_OPTS_EVEX_dfv_MASK, 0xF00);
            AssertOption(insOpts.INS_OPTS_EVEX_NoApxPromotion, 1 << 12);
            AssertOption(insOpts.INS_OPTS_EVEX_zu, 1 << 14);
        }
        else if (Configuration.TARGET_ARM)
        {
            AssertOption(insOpts.INS_OPTS_LDST_PRE_DEC, 1);
            AssertOption(insOpts.INS_OPTS_LDST_POST_INC, 2);
            AssertOption(insOpts.INS_OPTS_RRX, 3);
            AssertOption(insOpts.INS_OPTS_LSL, 4);
            AssertOption(insOpts.INS_OPTS_ROR, 7);
        }
        else if (Configuration.TARGET_ARM64)
        {
            AssertOption(insOpts.INS_OPTS_PRE_INDEX, 1);
            AssertOption(insOpts.INS_OPTS_LSL12, 3);
            AssertOption(insOpts.INS_OPTS_LSL, 4);
            AssertOption(insOpts.INS_OPTS_UXTB, 8);
            AssertOption(insOpts.INS_OPTS_8B, 16);
            AssertOption(insOpts.INS_OPTS_SCALABLE_B, 24);
            AssertOption(insOpts.INS_OPTS_D_TO_H, 51);
#if FEATURE_LOOP_ALIGN
            AssertOption(insOpts.INS_OPTS_ALIGN, 52);
#endif
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            AssertOption(insOpts.INS_OPTS_RC, 1);
            AssertOption(insOpts.INS_OPTS_RL, 2);
            AssertOption(insOpts.INS_OPTS_JIRL, 3);
            AssertOption(insOpts.INS_OPTS_J, 4);
            AssertOption(insOpts.INS_OPTS_J_cond, 5);
            AssertOption(insOpts.INS_OPTS_I_LOONGARCH64, 6);
            AssertOption(insOpts.INS_OPTS_C_LOONGARCH64, 7);
            AssertOption(insOpts.INS_OPTS_RELOC_LOONGARCH64, 8);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            AssertOption(insOpts.INS_OPTS_RC, 1);
            AssertOption(insOpts.INS_OPTS_RL, 2);
            AssertOption(insOpts.INS_OPTS_JUMP, 3);
            AssertOption(insOpts.INS_OPTS_I_RISCV64, 4);
            AssertOption(insOpts.INS_OPTS_C_RISCV64, 5);
            AssertOption(insOpts.INS_OPTS_RELOC_RISCV64, 6);
        }
        else if (!Configuration.TARGET_WASM)
        {
            Assert.Fail("The active target architecture is not recognized.");
        }
    }

    private static void AssertOption(insOpts option, int expected)
    {
        Assert.That((uint)option, Is.EqualTo((uint)expected));
    }
}
