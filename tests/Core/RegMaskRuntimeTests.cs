// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class RegMaskRuntimeTests
{
    [Test]
    public static void ScalarOperationsRespectTheConfiguredMaskWidth()
    {
        var allBits = new regMaskTP(unchecked((regMask)ulong.MaxValue));
        var highBit = new regMaskTP(unchecked((regMask)0x100000000UL));

        Assert.That(PopCount(allBits), Is.EqualTo(Configuration.REGMASK_BITS_32 ? 32U : 64U));
        Assert.That(BitScanForward(allBits), Is.Zero);
        Assert.That(PopCount(highBit), Is.EqualTo(Configuration.REGMASK_BITS_32 ? 0U : 1U));
        Assert.That(genFindLowestBit(highBit).Lower,
            Is.EqualTo(Configuration.REGMASK_BITS_32 ? SRBM_NONE : unchecked((regMask)0x100000000UL)));
        Assert.That(genMaxOneBit(highBit), Is.True);
        Assert.That(genExactlyOneBit(highBit), Is.EqualTo(!Configuration.TARGET_X86));

        var shifted = allBits >> 1;
        var expectedShifted = Configuration.REGMASK_BITS_32 ? 0x7FFFFFFFUL : 0x7FFFFFFFFFFFFFFFUL;
        Assert.That(shifted.Lower, Is.EqualTo(unchecked((regMask)expectedShifted)));

        if (Configuration.TARGET_X86)
        {
            var truncated = new regMaskTP(unchecked((regMask)0x100000001UL));
            Assert.That(truncated.Lower, Is.EqualTo((regMask)1));
        }
    }

    [Test]
    public static void RegisterBanksAndMaskSetsFollowTheConfiguredTarget()
    {
        var mask = new regMaskTP((regMask)1, (regMask)2);
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.That(mask.Lower, Is.EqualTo((regMask)1));
            Assert.That(mask.Upper, Is.EqualTo((regMask)2));

            var lowerBit = genFindLowestBit(new regMaskTP((regMask)0x10, (regMask)0x40));
            Assert.That(lowerBit, Is.EqualTo(new regMaskTP((regMask)0x10)));
            Assert.That(genCountBits(new regMaskTP((regMask)1, (regMask)1)), Is.EqualTo(2U));
            Assert.That(genMaxOneBit(new regMaskTP((regMask)1, (regMask)0)), Is.True);
            Assert.That(genMaxOneBit(new regMaskTP((regMask)1, (regMask)1)), Is.False);
        }
        else
        {
            Assert.That(mask.Lower, Is.EqualTo((regMask)1));
            Assert.That(mask.Upper, Is.EqualTo(SRBM_NONE));
        }

        var typedMask = default(regMaskTP);
        regMaskTP.AddRegsetForType(ref typedMask, (regMask)1, TYP_MASK);
        if (Configuration.HAS_MORE_THAN_64_REGISTERS && varTypeIsMask(TYP_MASK))
        {
            Assert.That(typedMask, Is.EqualTo(new regMaskTP(SRBM_NONE, (regMask)1)));
        }
        else
        {
            Assert.That(typedMask, Is.EqualTo(new regMaskTP((regMask)1)));
        }
    }

    [Test]
    public static void Arm32DoubleRegisterMasksKeepTheirDistinctConstructionRules()
    {
        if (!Configuration.TARGET_ARM)
        {
            Assert.Ignore("ARM32-specific register behavior.");
        }

        var first = REG_ARM32_F0;
        var second = REG_ARM32_F1;
        var pair = first.SingleTypeMask | second.SingleTypeMask;

        Assert.That(genSingleTypeRegMask(first, TYP_DOUBLE), Is.EqualTo(pair));
        Assert.That(getSingleTypeRegMask(second, TYP_DOUBLE), Is.EqualTo(pair));
        Assert.That(genRegMask(first, TYP_DOUBLE), Is.EqualTo(new regMaskTP(pair)));
        Assert.That(genSingleTypeRegMask(second, TYP_FLOAT), Is.EqualTo(second.SingleTypeMask));
    }

    [Test]
    public static void TypedFixedRegisterMasksRemainExplicitlyUnsupportedOnWasm()
    {
        if (!Configuration.TARGET_WASM)
        {
            Assert.Ignore("Wasm-specific fixed-register behavior.");
        }

        Assert.Throws<System.PlatformNotSupportedException>(() => genSingleTypeRegMask(REG_WASM_STK, TYP_INT));
    }
}
