// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterPrefixTests
{
    [Test]
    public static void AddressModeAndRegisterUnionPreserveNativeBitWidths()
    {
        Assert.That(Unsafe.SizeOf<Emitter.emitAddrMode>(), Is.EqualTo(4));
        Assert.That(Unsafe.SizeOf<Emitter.instrDesc.idAddrUnion>(), Is.EqualTo(8));

        var descriptor = CreateDescriptor(INS_add, IF_RWR_RRD_RRD, EA_8BYTE);
        descriptor.idReg3(REG_R16);
        descriptor.idReg4(REG_R31);
        Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R16));
        Assert.That(descriptor.idReg4(), Is.EqualTo(REG_R31));

        ref var address = ref descriptor.idAddr().iiaAddrMode;
        address.amBaseReg = REG_R18;
        address.amIndxReg = REG_R19;
        address.amScale = 3;
        address.amDisp = -17;
        Assert.That(address.amBaseReg, Is.EqualTo(REG_R18));
        Assert.That(address.amIndxReg, Is.EqualTo(REG_R19));
        Assert.That(address.amScale, Is.EqualTo(3u));
        Assert.That(address.amDisp, Is.EqualTo(-17));
        Assert.That(descriptor.idReg3(), Is.EqualTo((regNumber)((uint)REG_R18 & 0x7F)));
    }

    [Test]
    public static void ApxLegacyNddAndNoPromotionSelectEvexOrRex2()
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseRex2Encodings = true;
        emitter.UsePromotedEvexEncodings = true;
        var descriptor = CreateDescriptor(INS_add, IF_RWR_RRD, EA_8BYTE);
        descriptor.idReg1(REG_R16);

        Assert.That(TakesEvex(emitter, descriptor), Is.False);
        Assert.That(TakesRex2(emitter, descriptor), Is.True);

        descriptor.idSetEvexNdContext();
        Assert.That(TakesEvex(emitter, descriptor), Is.True);
        Assert.That(TakesRex2(emitter, descriptor), Is.False);

        var noPromotion = CreateDescriptor(INS_add, IF_RWR_RRD, EA_8BYTE);
        noPromotion.idReg1(REG_R16);
        noPromotion.idSetEvexNdContext();
        noPromotion.idSetNoApxEvexPromotion();
        Assert.That(TakesEvex(emitter, noPromotion), Is.False);
        Assert.That(TakesRex2(emitter, noPromotion), Is.True);
    }

    [Test]
    public static void EvexContextsAndHighSimdRegistersRequireEvex()
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
        var high = CreateDescriptor(INS_addps, IF_RWR_RRD, EA_16BYTE);
        high.idReg1(REG_XMM16);
        Assert.That(TakesEvex(emitter, high), Is.True);

        var embeddedMask = CreateDescriptor(INS_addps, IF_RWR_RRD, EA_16BYTE);
        embeddedMask.idSetEvexAaaContext(4);
        Assert.That(TakesEvex(emitter, embeddedMask), Is.True);

        var broadcast = CreateDescriptor(INS_addps, IF_RWR_RRD, EA_16BYTE);
        broadcast.idSetEvexBroadcastBit();
        Assert.That(TakesEvex(emitter, broadcast), Is.True);
    }

    [TestCase(INS_movsx, EA_1BYTE, true)]
    [TestCase(INS_push, EA_8BYTE, false)]
    [TestCase(INS_movzx, EA_8BYTE, false)]
    [TestCase(INS_add, EA_8BYTE, true)]
    [TestCase(INS_add, EA_4BYTE, false)]
    [TestCase(INS_pdep, EA_8BYTE, true)]
    [TestCase(INS_pdep, EA_4BYTE, false)]
    public static void RexWUsesOpcodeClassAndOperandSize(instruction ins, emitAttr size, bool expected)
    {
        var emitter = CreateEmitter();
        var descriptor = CreateDescriptor(ins, IF_RWR_RRD, size);
        Assert.That(TakesRexW(emitter, descriptor), Is.EqualTo(expected));
    }

    [Test]
    public static void ApxOnlyInstructionsRequirePromotedEvexMode()
    {
        var emitter = CreateEmitter();
        var descriptor = CreateDescriptor(INS_push2, IF_RRD, EA_8BYTE);
        Assert.That(TakesEvex(emitter, descriptor), Is.False);
        emitter.UsePromotedEvexEncodings = true;
        Assert.That(TakesEvex(emitter, descriptor), Is.True);
    }

    [TestCase(INS_addps, IF_RWR_RRD, EA_16BYTE, 0x6200000000000000UL, 4u)]
    [TestCase(INS_addps, IF_RWR_RRD, EA_16BYTE, 0xC4000000000000UL, 2u)]
    [TestCase(INS_pshufb, IF_RWR_RRD, EA_16BYTE, 0xC4000000000000UL, 3u)]
    [TestCase(INS_sarx, IF_RWR_RRD, EA_4BYTE, 0xC4000000000000UL, 3u)]
    [TestCase(INS_add, IF_RWR_RRD, EA_8BYTE, 0xD50000000000UL, 2u)]
    [TestCase(INS_add, IF_RWR_RRD, EA_8BYTE, 0x4800000000UL, 1u)]
    [TestCase(INS_add, IF_RWR_RRD, EA_8BYTE, 0UL, 0u)]
    public static void EncodedPrefixUsesNativeByteCount(
        instruction ins, Emitter.insFormat format, emitAttr size, ulong code, uint expected)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
        emitter.UseRex2Encodings = true;
        var descriptor = CreateDescriptor(ins, format, size);

        Assert.That(GetPrefixSize(emitter, descriptor, code, true), Is.EqualTo(expected));
        if (code == 0x4800000000UL)
        {
            Assert.That(GetPrefixSize(emitter, descriptor, code, false), Is.Zero);
        }
    }

    [TestCase(REG_XMM0, REG_XMM8, 3u)]
    [TestCase(REG_XMM8, REG_XMM0, 2u)]
    public static void VexUsesRMRegisterForRexB(regNumber destination, regNumber source, uint expected)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        var descriptor = CreateDescriptor(INS_addps, IF_RWR_RRD, EA_16BYTE);
        descriptor.idReg1(destination);
        descriptor.idReg2(source);
        Assert.That(GetVexPrefixSize(emitter, descriptor), Is.EqualTo(expected));
    }

    [Test]
    public static void VexSibIndexRequiresThreeBytePrefix()
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        var descriptor = CreateDescriptor(INS_addps, IF_RWR_ARD, EA_16BYTE);
        descriptor.idAddr().iiaAddrMode.amIndxReg = REG_R8;
        Assert.That(GetVexPrefixSize(emitter, descriptor), Is.EqualTo(3u));
    }

    [Test]
    public static void BroadcastAndCompressedDisplacementShareEvexContext()
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
        var descriptor = CreateDescriptor(INS_addps, IF_RWR_ARD, EA_16BYTE);

        Assert.That(HasBroadcast(emitter, descriptor), Is.False);
        descriptor.idSetEvexBroadcastBit();
        Assert.That(HasBroadcast(emitter, descriptor), Is.True);
        SetCompressedDisplacement(emitter, descriptor);
        Assert.That(HasBroadcast(emitter, descriptor), Is.True);
        Assert.That(descriptor.idGetEvexbContext(), Is.EqualTo(3u));
    }

    [TestCase(REG_R8, REG_XMM0, 3u)]
    [TestCase(REG_RAX, REG_XMM8, 2u)]
    public static void VexMovdUsesGeneralPurposeRegisterForRexB(regNumber destination, regNumber source, uint expected)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        var descriptor = CreateDescriptor(INS_movd32, IF_RWR_RRD, EA_4BYTE);
        descriptor.idReg1(destination);
        descriptor.idReg2(source);
        Assert.That(GetVexPrefixSize(emitter, descriptor), Is.EqualTo(expected));
    }

    [TestCase(-8192)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(8191)]
    public static void AddressModeDisplacementIsSignedFourteenBits(int displacement)
    {
        var address = new Emitter.emitAddrMode { amDisp = displacement };
        Assert.That(address.amDisp, Is.EqualTo(displacement));
    }

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        return emitter;
    }

    private static Emitter.instrDesc CreateDescriptor(instruction instruction, Emitter.insFormat format, emitAttr size)
    {
        var type = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing basic instruction descriptor.");
        var descriptor = (Emitter.instrDesc)(Activator.CreateInstance(type, nonPublic: true)
            ?? throw new AssertionException("Could not create a basic instruction descriptor."));
        descriptor.idIns(instruction);
        descriptor.idInsFmt(format);
        descriptor.idOpSize(size);
        return descriptor;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TakesEvexPrefix")]
    private static extern bool TakesEvex(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TakesRex2Prefix")]
    private static extern bool TakesRex2(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TakesRexWPrefix")]
    private static extern bool TakesRexW(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetPrefixSize")]
    private static extern uint GetPrefixSize(Emitter emitter, Emitter.instrDesc descriptor, ulong code, bool includeRexPrefixSize);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetVexPrefixSize")]
    private static extern uint GetVexPrefixSize(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "HasEmbeddedBroadcast")]
    private static extern bool HasBroadcast(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SetEvexCompressedDisplacement")]
    private static extern void SetCompressedDisplacement(Emitter emitter, Emitter.instrDesc descriptor);
}
