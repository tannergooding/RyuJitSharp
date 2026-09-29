// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64EmitterRegisterDescriptorTests
{
    [TestCase(0u, 127u)]
    [TestCase(127u, 0u)]
    [TestCase(31u, 32u)]
    [TestCase(63u, 64u)]
    public static void RegistersPreserveLocalAddressAndAdjacentBits(uint third, uint fourth)
    {
        var descriptor = DescriptorEmitter.Basic();
        descriptor.idAddr().iiaLclVar.initLclVarAddr(12345, 456);
        var localBits = unchecked((uint)Raw(descriptor));
        const uint reservedBits = 0xBEE00000;
        Raw(descriptor) |= (ulong)reservedBits << 32;
        for (uint gc = 0; gc < 4; gc++)
        {
            foreach (var scaled in new[] { false, true })
            {
                descriptor.idGCrefReg2((GCInfo.GCtype)gc);
                descriptor.idReg3Scaled(scaled);
                descriptor.idReg3((regNumber)third);
                descriptor.idReg4((regNumber)fourth);

                Assert.That(descriptor.idReg3(), Is.EqualTo((regNumber)third));
                Assert.That(descriptor.idReg4(), Is.EqualTo((regNumber)fourth));
                Assert.That(descriptor.idGCrefReg2(), Is.EqualTo((GCInfo.GCtype)gc));
                Assert.That(descriptor.idReg3Scaled(), Is.EqualTo(scaled));
                Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(12345));
                Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(456u));
                Assert.That(unchecked((uint)Raw(descriptor)), Is.EqualTo(localBits));
                Assert.That((uint)(Raw(descriptor) >> 32),
                    Is.EqualTo(reservedBits | (scaled ? 1u : 0u) | (gc << 1) | (third << 3) | (fourth << 10)));
            }
        }
    }

    [Test]
    public static void OptionAliasesShareTheNativeBit()
    {
        var descriptor = DescriptorEmitter.Basic();
        descriptor.idReg3(REG_R19);
        descriptor.idReg4(REG_R28);
        descriptor.idReg3Scaled(true);
        Assert.That(descriptor.idPredicateReg2Merge(), Is.True);
        Assert.That(descriptor.idVectorLength4x(), Is.True);
        Assert.That(descriptor.idHasShift(), Is.True);

        descriptor.idPredicateReg2Merge(false);
        Assert.That(descriptor.idReg3Scaled(), Is.False);
        descriptor.idVectorLength4x(true);
        Assert.That(descriptor.idReg3Scaled(), Is.True);
        descriptor.idHasShift(false);
        Assert.That(descriptor.idVectorLength4x(), Is.False);
        Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R19));
        Assert.That(descriptor.idReg4(), Is.EqualTo(REG_R28));
    }

    [Test]
    public static void SmallDescriptorsDoNotReadOrWriteTheShiftBit()
    {
        var descriptor = DescriptorEmitter.Basic();
        descriptor.idSetIsSmallDsc();
        Assert.That(descriptor.idHasShift(), Is.False);
        descriptor.idHasShift(true);
        Assert.That(descriptor.idHasShift(), Is.False);
    }

    [Test]
    public static void SvePatternsOverlayOnlyTheLocalWord()
    {
        var descriptor = DescriptorEmitter.Basic();
        descriptor.idReg3(REG_R19);
        descriptor.idReg4(REG_R28);
        descriptor.idReg3Scaled(true);
        descriptor.idGCrefReg2(GCInfo.GCtype.GCT_BYREF);
        var registers = Raw(descriptor) >> 32;
        foreach (var pattern in Enum.GetValues<insSvePattern>())
        {
            descriptor.idSvePattern(pattern);
            Assert.That(descriptor.idSvePattern(), Is.EqualTo(pattern));
            Assert.That(unchecked((uint)Raw(descriptor)), Is.EqualTo((uint)pattern));
            Assert.That(Raw(descriptor) >> 32, Is.EqualTo(registers));
        }
    }

    [TestCase(0u)]
    [TestCase(15u)]
    [TestCase(31u)]
    [TestCase(127u)]
    [TestCase(128u)]
    [TestCase(uint.MaxValue)]
    public static void SvePrefetchOperationsAliasAndTruncateTheFourthRegister(uint operation)
    {
        var descriptor = DescriptorEmitter.Basic();
        descriptor.idReg3(REG_R19);
        descriptor.idGCrefReg2(GCInfo.GCtype.GCT_GCREF);
        descriptor.idSvePrfop((insSvePrfop)operation);
        Assert.That((uint)descriptor.idSvePrfop(), Is.EqualTo(operation & 127));
        Assert.That((uint)descriptor.idReg4(), Is.EqualTo(operation & 127));
        Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R19));
        Assert.That(descriptor.idGCrefReg2(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));

        descriptor.idReg4(REG_R28);
        Assert.That((uint)descriptor.idSvePrfop(), Is.EqualTo((uint)REG_R28));
    }

    private static ref ulong Raw(Emitter.instrDesc descriptor)
    {
        return ref Unsafe.As<Emitter.instrDesc.idAddrUnion, ulong>(ref descriptor.idAddr());
    }

    private abstract class DescriptorEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static instrDesc Basic()
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(INS_nop);
            return descriptor;
        }
    }
}
#endif
