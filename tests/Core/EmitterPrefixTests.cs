// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterPrefixTests
{
    [TestCase(REG_RAX, EA_1BYTE, 0u)]
    [TestCase(REG_RBX, EA_1BYTE, 24u)]
    [TestCase(REG_RBP, EA_1BYTE, 40u)]
    [TestCase(REG_RDI, EA_1BYTE, 56u)]
    [TestCase(REG_RBP, EA_8BYTE, 40u)]
    [TestCase(REG_RDI, EA_8BYTE, 56u)]
    public static void UnextendedRegisterFieldsAllowOmittedOpcodeStorage(regNumber reg, emitAttr size, uint expected)
    {
        var emitter = CreateEmitter();
        var descriptor = CreateDescriptor(INS_mov, IF_RWR_RRD, size);

        Assert.That(EncodeRegisterField(emitter, descriptor, reg, size, null), Is.EqualTo(expected));
    }

    [TestCase(INS_psrldq, REG_RBX)]
    [TestCase(INS_pslldq, REG_RDI)]
    [TestCase(INS_psrlw, REG_RDX)]
    [TestCase(INS_psllq, REG_RSI)]
    [TestCase(INS_vpsraq, REG_RSP)]
    [TestCase(INS_vprolq, REG_RCX)]
    [TestCase(INS_vprord, REG_RAX)]
    public static void ShiftImmediateOpcodeSelectsNativeRegisterField(instruction ins, regNumber expected)
    {
        Assert.That(GetSseShiftRegNumber(CreateEmitter(), ins), Is.EqualTo(expected));
    }

    [TestCase(INS_addps, REG_XMM0, EA_16BYTE, false, true)]
    [TestCase(INS_addps, REG_XMM16, EA_16BYTE, true, false)]
    [TestCase(INS_mov, REG_RAX, EA_8BYTE, false, false)]
    public static void SimdPrefixHelperSelectsTheRequiredEncoding(
        instruction ins, regNumber reg, emitAttr size, bool expectEvex, bool expectVex)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
        var descriptor = CreateDescriptor(ins, IF_RWR_RRD, size);
        descriptor.idReg1(reg);

        const ulong code = 0x1234;
        var expected = (expectEvex, expectVex) switch
        {
            (false, false) => code,
            (false, true) => emitter.AddVexPrefix(ins, code, size),
            (true, false) => AddEvexPrefix(emitter, descriptor, code, size),
            _ => throw new AssertionException("A descriptor cannot require both VEX and EVEX."),
        };

        Assert.That(emitter.AddSimdPrefixIfNeeded(descriptor, code, size), Is.EqualTo(expected));
    }

#if DEBUG
    [Test]
    public static void InvalidShiftImmediateOpcodeAssertsAndReturnsNoRegister()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);

        Assert.That(GetSseShiftRegNumber(CreateEmitter(), INS_mov), Is.EqualTo(REG_NA));
        Assert.That(context.Assertions, Is.EqualTo(1));
    }
#endif

#if DEBUG
    [TestCase(false, false, 0x62FFFFFFFFFFFFFFUL, 1)]
    [TestCase(true, false, 0x62FFFFFFFFFFFFFFUL, 0)]
    [TestCase(false, true, 0x62FFFFFFFFFFFFFFUL, 0)]
    [TestCase(true, false, 0xC4FFFFFFFFFFFFUL, 1)]
    [TestCase(true, false, 0UL, 1)]
    public static void HighSimdRegisterFieldsRequireEnabledEvexPrefix(
        bool evex, bool promotedEvex, ulong code, int expectedAssertions)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = evex;
        emitter.UsePromotedEvexEncodings = promotedEvex;
        var descriptor = CreateDescriptor(INS_addps, IF_RWR_RRD, EA_16BYTE);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);

        var expectedCode = code & 0xFFEFFFFFFFFFFFFFUL;
        Assert.That(EncodeRegisterField(emitter, descriptor, REG_XMM16, EA_16BYTE, &code), Is.Zero);
        Assert.That(code, Is.EqualTo(expectedCode));
        Assert.That(context.Assertions, Is.EqualTo(expectedAssertions));
    }

    [TestCase(REG_XMM15, false)]
    [TestCase(REG_XMM16, true)]
    [TestCase(REG_XMM31, true)]
    public static void HighSimdRegisterClassifierMatchesExtendedRegisterRange(regNumber reg, bool expected)
    {
        Assert.That(Emitter.isHighSimdReg(reg), Is.EqualTo(expected));
    }

    [TestCase(REG_R15, false)]
    [TestCase(REG_R16, true)]
    [TestCase(REG_R31, true)]
    public static void HighGeneralPurposeRegisterClassifierMatchesApxRegisterRange(regNumber reg, bool expected)
    {
        Assert.That(IsHighGPReg(CreateEmitter(), reg), Is.EqualTo(expected));
    }

    [TestCase(0UL, false, false)]
    [TestCase(0x4800000000UL, true, false)]
    [TestCase(0xFF00000000UL, true, false)]
    [TestCase(0xD40000000000UL, false, false)]
    [TestCase(0xD50000000000UL, false, true)]
    [TestCase(0xD5FF00000000UL, true, true)]
    public static void RexAndRex2ClassifiersMatchEncodedPrefixBytes(ulong code, bool expectedRex, bool expectedRex2)
    {
        var emitter = CreateEmitter();
        Assert.That(IsRexPrefix(emitter, code), Is.EqualTo(expectedRex));
        Assert.That(IsRex2Prefix(emitter, code), Is.EqualTo(expectedRex2));
    }

    [TestCase(INS_pcmpgtb, true)]
    [TestCase(INS_vpgatherqq, true)]
    [TestCase(INS_kmovq_msk, true)]
    [TestCase(INS_add, false)]
    public static void KMaskDestinationClassifierMatchesEvexInstructionSet(instruction ins, bool expected)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;

        Assert.That(HasKMaskRegisterDest(emitter, ins), Is.EqualTo(expected));
    }

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        ((AssertionContext*)self)->Assertions++;
        return 0;
    }
#endif

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddEvexPrefix")]
    private static extern ulong AddEvexPrefix(Emitter emitter, Emitter.instrDesc descriptor, ulong code, emitAttr size);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insEncodeReg345")]
    private static extern uint EncodeRegisterField(
        Emitter emitter, Emitter.instrDesc descriptor, regNumber reg, emitAttr size, ulong* code);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "getSseShiftRegNumber")]
    private static extern regNumber GetSseShiftRegNumber(Emitter emitter, instruction ins);

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

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isHighGPReg")]
    private static extern bool IsHighGPReg(Emitter emitter, regNumber reg);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "hasRexPrefix")]
    private static extern bool IsRexPrefix(Emitter emitter, ulong code);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "hasRex2Prefix")]
    private static extern bool IsRex2Prefix(Emitter emitter, ulong code);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "HasKMaskRegisterDest")]
    private static extern bool HasKMaskRegisterDest(Emitter emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SetEvexCompressedDisplacement")]
    private static extern void SetCompressedDisplacement(Emitter emitter, Emitter.instrDesc descriptor);
}
