// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterDescriptorPayloadTests
{
#if TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
    [Test]
    public static void TargetOptionEnumRetainsTheNativeNamesOrdinalsAndUnsignedWidth()
    {
        string[] expectedNames = [
            nameof(insOpts.INS_OPTS_NONE),
#if TARGET_ARM
            nameof(insOpts.INS_OPTS_LDST_PRE_DEC),
            nameof(insOpts.INS_OPTS_LDST_POST_INC),
            nameof(insOpts.INS_OPTS_RRX),
            nameof(insOpts.INS_OPTS_LSL),
            nameof(insOpts.INS_OPTS_LSR),
            nameof(insOpts.INS_OPTS_ASR),
            nameof(insOpts.INS_OPTS_ROR),
#elif TARGET_LOONGARCH64
            nameof(insOpts.INS_OPTS_RC),
            nameof(insOpts.INS_OPTS_RL),
            nameof(insOpts.INS_OPTS_JIRL),
            nameof(insOpts.INS_OPTS_J),
            nameof(insOpts.INS_OPTS_J_cond),
            nameof(insOpts.INS_OPTS_I),
            nameof(insOpts.INS_OPTS_C),
            nameof(insOpts.INS_OPTS_RELOC),
#elif TARGET_RISCV64
            nameof(insOpts.INS_OPTS_RC),
            nameof(insOpts.INS_OPTS_RL),
            nameof(insOpts.INS_OPTS_JUMP),
            nameof(insOpts.INS_OPTS_I),
            nameof(insOpts.INS_OPTS_C),
            nameof(insOpts.INS_OPTS_RELOC),
#endif
        ];

        Assert.That(Enum.GetUnderlyingType(typeof(insOpts)), Is.EqualTo(typeof(uint)));
        Assert.That(Enum.GetNames<insOpts>(), Is.EqualTo(expectedNames));
        var options = Enum.GetValues<insOpts>();
        Assert.That(options, Has.Length.EqualTo(expectedNames.Length));
        for (var ordinal = 0; ordinal < options.Length; ordinal++)
        {
            Assert.That((uint)options[ordinal], Is.EqualTo((uint)ordinal));
        }
    }
#endif

#if !TARGET_XARCH
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(6)]
    [TestCase(7)]
#if !TARGET_ARMARCH
    [TestCase(14)]
    [TestCase(15)]
#endif
    public static void NonXarchBoundFlagAliasesCustomBitZeroWithoutChangingOtherBits(int value)
    {
        var descriptor = new Descriptor();
        CustomBits(descriptor) = (byte)value;

        Assert.That(descriptor.idIsBound(), Is.EqualTo((value & 1) != 0));

        descriptor.idSetIsBound();

        Assert.That(CustomBits(descriptor), Is.EqualTo((byte)(value | 1)));
        Assert.That(descriptor.idIsBound(), Is.True);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_idCustomBits")]
    private static extern ref byte CustomBits(Emitter.instrDesc descriptor);
#endif

#if TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
    [Test]
    public static void CallRegisterPointerAliasesCustomBitThreeWithoutChangingBoundOrNoGc()
    {
        var descriptor = new Descriptor();
        descriptor.idSetIsBound();
        descriptor.idSetIsNoGC(true);

        Assert.That(descriptor.idIsCallRegPtr(), Is.False);

        descriptor.idSetIsCallRegPtr();

        Assert.That(CustomBits(descriptor), Is.EqualTo((byte)13));
        Assert.That(descriptor.idIsCallRegPtr(), Is.True);
        Assert.That(descriptor.idIsBound(), Is.True);
        Assert.That(descriptor.idIsNoGC(), Is.True);

        CustomBits(descriptor) &= unchecked((byte)~8);

        Assert.That(descriptor.idIsCallRegPtr(), Is.False);
        Assert.That(descriptor.idIsBound(), Is.True);
        Assert.That(descriptor.idIsNoGC(), Is.True);
    }
#endif

#if TARGET_WASM
    [Test]
    public static void WasmCodeSizeRetainsTheTerminatingUnportedBoundary()
    {
        var descriptor = new Descriptor();

        var exception = Assert.Throws<FatalJitException>(() => descriptor.idCodeSize());

        Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
    }
#endif

#if TARGET_XARCH
    [TestCase(EA_4BYTE, false, false)]
    [TestCase(EA_4BYTE | EA_CNS_RELOC_FLG, true, false)]
    [TestCase(EA_4BYTE | EA_DSP_RELOC_FLG, false, true)]
    [TestCase(EA_4BYTE | EA_CNS_RELOC_FLG | EA_DSP_RELOC_FLG, true, true)]
    public static void RelocationFlagAccessorsReplaceBothFlags(emitAttr attr, bool constant, bool displacement)
    {
        var descriptor = new Descriptor();
        descriptor.idSetIsCnsReloc();
        descriptor.idSetIsDspReloc(false);
        descriptor.idSetIsDspReloc();

        Assert.That(descriptor.idIsDspReloc(), Is.True);

        descriptor.idSetRelocFlags(attr);

        Assert.That(descriptor.idIsCnsReloc(), Is.EqualTo(constant));
        Assert.That(descriptor.idIsDspReloc(), Is.EqualTo(displacement));
        Assert.That(descriptor.idIsReloc(), Is.EqualTo(constant || displacement));
    }

    [Test]
    public static void AddressAccessorReturnsMutableDescriptorStorage()
    {
        var descriptor = new Descriptor();
        ref var address = ref descriptor.idAddr();
        address.iiaSecRel = true;

        Assert.That(descriptor.idAddr().iiaSecRel, Is.True);
    }

    [Test]
    public static void SmallDisplacementFlagClearsLargeDisplacementFlag()
    {
        var descriptor = new Descriptor();
        descriptor.idSetIsLargeDsp();

        Assert.That(descriptor.idIsLargeDsp(), Is.True);

        descriptor.idSetIsSmallDsp();

        Assert.That(descriptor.idIsLargeDsp(), Is.False);
    }

    [Test]
    public static void EvexDescriptorContextAccessorsPreserveEncodingFields()
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_addps);
        descriptor.idSetEvexbContext(3u);
        descriptor.idSetEvexAaaContext(7u << 2);
        descriptor.idSetEvexZContext();

        Assert.That(descriptor.idIsEvexbContextSet(), Is.True);
        Assert.That(descriptor.idGetEvexbContext(), Is.EqualTo(3u));
        Assert.That(descriptor.idIsEvexAaaContextSet(), Is.True);
        Assert.That(descriptor.idGetEvexAaaContext(), Is.EqualTo(7u));
        Assert.That(descriptor.idIsEvexZContextSet(), Is.True);
        Assert.That(descriptor.idGetEvexDFV(), Is.EqualTo(15u));

        var compressed = new Descriptor();
        compressed.idSetEvexCompressedDisplacementBit();

        Assert.That(compressed.idIsEvexbContextSet(), Is.True);
        Assert.That(compressed.idGetEvexbContext(), Is.EqualTo(2u));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BoundFlagUsesTheSharedCustomBitWithoutChangingOtherPayloads(bool bound)
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_nop);
        descriptor.idSetTlsGD();
        descriptor.idSetIsNoGC(true);
        descriptor.idSetIsCallRegPtr();
        descriptor.idSetIsLargeCall();
        descriptor.idSetIsLargeDsp();
        descriptor.idSmallCns(-1);
        descriptor.idCodeSize(15);

        if (bound)
        {
            descriptor.idSetIsBound();
        }

        Assert.That(descriptor.idIsBound(), Is.EqualTo(bound));
        Assert.That(descriptor.idGetEvexDFV(), Is.EqualTo(bound ? 15u : 14u));
        Assert.That(descriptor.idIsTlsGD(), Is.True);
        Assert.That(descriptor.idIsNoGC(), Is.True);
        Assert.That(descriptor.idIsCallRegPtr(), Is.True);
        Assert.That(descriptor.idIsCall(), Is.True);
        Assert.That(descriptor.idIsLargeCall(), Is.True);
        Assert.That(descriptor.idIsLargeCns(), Is.False);
        Assert.That(descriptor.idIsLargeDsp(), Is.True);
        Assert.That(descriptor.idSmallCns(), Is.EqualTo(-1));
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(15u));
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(14u)]
    [TestCase(15u)]
    public static void CustomPayloadWritesUpdateTheBoundFlagThroughTheNativeAlias(uint value)
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_nop);
        descriptor.idSetEvexDFV(value << 8);

        Assert.That(descriptor.idIsBound(), Is.EqualTo((value & 1) != 0));
        Assert.That(descriptor.idIsTlsGD(), Is.EqualTo((value & 2) != 0));
        Assert.That(descriptor.idIsNoGC(), Is.EqualTo((value & 4) != 0));
        Assert.That(descriptor.idIsCallRegPtr(), Is.EqualTo((value & 8) != 0));

        descriptor.idSetIsBound();

        Assert.That(descriptor.idGetEvexDFV(), Is.EqualTo(value | 1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BoundAndEvexMaskViewsShareTheSameBitInBothDirections(bool setThroughBound)
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_nop);
        descriptor.idSetIsCallRegPtr();
        if (setThroughBound)
        {
            descriptor.idSetIsBound();
            descriptor.idIns(INS_addps);
            Assert.That(descriptor.idGetEvexAaaContext(), Is.EqualTo(1u));
        }
        else
        {
            descriptor.idIns(INS_addps);
            descriptor.idSetEvexAaaContext(1u << 2);
            descriptor.idIns(INS_nop);
            Assert.That(descriptor.idIsBound(), Is.True);
        }

        descriptor.idIns(INS_nop);
        Assert.That(descriptor.idIsCallRegPtr(), Is.True);
        Assert.That(descriptor.idGetEvexDFV(), Is.EqualTo(9u));
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(15u)]
    public static void XarchCodeAndOperandSizesPreserveIndependentDescriptorState(uint size)
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_nop);
        descriptor.idReg1(REG_RAX);
        descriptor.idReg2(REG_XMM0);
        descriptor.idSetIsBound();
        descriptor.idSetIsLargeDsp();
        descriptor.idSmallCns(-1);
        descriptor.idCodeSize(size);

        foreach (var operandSize in new[] { EA_1BYTE, EA_2BYTE, EA_4BYTE, EA_8BYTE, EA_16BYTE, EA_32BYTE, EA_64BYTE })
        {
            descriptor.idOpSize(operandSize);
            for (uint gc = 0; gc < 3; gc++)
            {
                descriptor.idGCref((GCInfo.GCtype)gc);

                Assert.That(descriptor.idCodeSize(), Is.EqualTo(size));
                Assert.That(descriptor.idOpSize(), Is.EqualTo(operandSize));
                Assert.That(descriptor.idGCref(), Is.EqualTo((GCInfo.GCtype)gc));
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_RAX));
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_XMM0));
                Assert.That(descriptor.idIsBound(), Is.True);
                Assert.That(descriptor.idIsLargeDsp(), Is.True);
                Assert.That(descriptor.idSmallCns(), Is.EqualTo(-1));
            }
        }
    }

    [TestCase(REG_RAX)]
    [TestCase(REG_XMM0)]
    [TestCase(REG_K7)]
#if TARGET_AMD64
    [TestCase(REG_R31)]
    [TestCase(REG_XMM31)]
#endif
    public static void XarchRegistersPreserveTheirBankAndTheOtherOperand(regNumber reg)
    {
        var descriptor = new Descriptor();
        descriptor.idReg1(REG_RAX);
        descriptor.idReg2(REG_XMM0);
        descriptor.idReg1(reg);

        Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
        Assert.That(descriptor.idReg2(), Is.EqualTo(REG_XMM0));

        descriptor.idReg2(reg);

        Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
        Assert.That(descriptor.idReg2(), Is.EqualTo(reg));
    }

#if !DEBUG
    [TestCase(16u)]
    [TestCase(uint.MaxValue)]
    public static void XarchWritesRetainNativeFieldTruncationInRelease(uint value)
    {
        var descriptor = new Descriptor();
        descriptor.idCodeSize(value);
        descriptor.idGCref((GCInfo.GCtype)value);
        descriptor.idReg1(unchecked((regNumber)value));
        descriptor.idReg2(unchecked((regNumber)value));
#if TARGET_AMD64
        const uint registerMask = 127;
#else
        const uint registerMask = 63;
#endif
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(value & 15));
        Assert.That(descriptor.idGCref(), Is.EqualTo((GCInfo.GCtype)(value & 3)));
        Assert.That(descriptor.idReg1(), Is.EqualTo((regNumber)(value & registerMask)));
        Assert.That(descriptor.idReg2(), Is.EqualTo((regNumber)(value & registerMask)));
    }
#else
    [TestCase(false)]
    [TestCase(true)]
    public static void BoundAliasRetainsTheNativeAssertionBeforeReadingOrWriting(bool write)
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_addps);
        descriptor.idSetEvexDFV(write ? 0u : 1u << 8);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordPayloadAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var previousDescriptor = s_assertionDescriptor;
        s_assertionDescriptor = descriptor;
        try
        {
            if (write)
            {
                descriptor.idSetIsBound();
            }
            else
            {
                Assert.That(descriptor.idIsBound(), Is.True);
            }

            Assert.That(context.Assertions, Is.EqualTo(1));
            Assert.That(context.ObservedPayload, Is.EqualTo(write ? 0u : 1u));
            Assert.That(descriptor.idGetEvexDFV(), Is.EqualTo(1u));
        }
        finally
        {
            s_assertionDescriptor = previousDescriptor;
        }
    }

    private static Descriptor? s_assertionDescriptor;

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
        public uint ObservedPayload;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordPayloadAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (AssertionContext*)self;
        context->Assertions++;
        if (s_assertionDescriptor is Descriptor descriptor)
        {
            context->ObservedPayload = descriptor.idGetEvexDFV();
        }

        return 0;
    }
#endif
#endif

#if TARGET_ARM
    [Test]
    public static void ArmInstructionSizeEnumRetainsTheNativeNamesOrdinalsAndUnsignedWidth()
    {
        Assert.That(Enum.GetUnderlyingType(typeof(Emitter.insSize)), Is.EqualTo(typeof(uint)));
        Assert.That(Enum.GetNames<Emitter.insSize>(), Is.EqualTo(new[]
        {
            nameof(Emitter.insSize.ISZ_16BIT),
            nameof(Emitter.insSize.ISZ_32BIT),
            nameof(Emitter.insSize.ISZ_48BIT),
        }));
        Assert.That((uint)Emitter.insSize.ISZ_16BIT, Is.Zero);
        Assert.That((uint)Emitter.insSize.ISZ_32BIT, Is.EqualTo(1u));
        Assert.That((uint)Emitter.insSize.ISZ_48BIT, Is.EqualTo(2u));
    }

    [TestCase(Emitter.insSize.ISZ_16BIT, 2u)]
    [TestCase(Emitter.insSize.ISZ_32BIT, 4u)]
    [TestCase(Emitter.insSize.ISZ_48BIT, 6u)]
    public static void ArmInstructionSizeRetainsThePseudoInstructionWidth(Emitter.insSize size, uint codeSize)
    {
        var descriptor = new Descriptor();
        descriptor.idInsSize(size);
        descriptor.idInsFlags(insFlags.INS_FLAGS_SET);
        descriptor.idSmallCns(-1);

        Assert.That(descriptor.idInsSize(), Is.EqualTo(size));
        Assert.That(descriptor.idInstrIsT1(), Is.EqualTo(size == Emitter.insSize.ISZ_16BIT));
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(codeSize));
        Assert.That(descriptor.idInsFlags(), Is.EqualTo(insFlags.INS_FLAGS_SET));
        Assert.That(descriptor.idSmallCns(), Is.EqualTo(-1));
    }

#if !DEBUG
    [TestCase(3u, 1u, 6u)]
    [TestCase(4u, 2u, 2u)]
    [TestCase(uint.MaxValue, uint.MaxValue, 6u)]
    public static void ArmMetadataWritesRetainTheNativeTwoAndOneBitWidths(uint size, uint flags, uint codeSize)
    {
        var descriptor = new Descriptor();
        descriptor.idInsSize((Emitter.insSize)size);
        descriptor.idInsFlags((insFlags)flags);

        Assert.That(descriptor.idInsSize(), Is.EqualTo((Emitter.insSize)(size & 3)));
        Assert.That(descriptor.idInsFlags(), Is.EqualTo((insFlags)(flags & 1)));
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(codeSize));
    }
#else
    [TestCase(false)]
    [TestCase(true)]
    public static void ArmMetadataAssertionsObserveTheAlreadyTruncatedField(bool flags)
    {
        var descriptor = new Descriptor();
        descriptor.idInsSize(Emitter.insSize.ISZ_32BIT);
        descriptor.idInsFlags(insFlags.INS_FLAGS_SET);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordArmMetadataAssertion;
        var context = new MetadataAssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var previousDescriptor = s_metadataDescriptor;
        var previousFlags = s_observeFlags;
        s_metadataDescriptor = descriptor;
        s_observeFlags = flags;
        try
        {
            if (flags)
            {
                descriptor.idInsFlags((insFlags)2);
            }
            else
            {
                descriptor.idInsSize((Emitter.insSize)4);
            }

            Assert.That(context.Assertions, Is.EqualTo(1));
            Assert.That(context.ObservedPayload, Is.Zero);
        }
        finally
        {
            s_metadataDescriptor = previousDescriptor;
            s_observeFlags = previousFlags;
        }
    }

    private static Descriptor? s_metadataDescriptor;
    private static bool s_observeFlags;

    private struct MetadataAssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
        public uint ObservedPayload;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordArmMetadataAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (MetadataAssertionContext*)self;
        context->Assertions++;
        if (s_metadataDescriptor is Descriptor descriptor)
        {
            context->ObservedPayload = s_observeFlags ? (uint)descriptor.idInsFlags() : (uint)descriptor.idInsSize();
        }

        return 0;
    }
#endif
#endif

#if TARGET_LOONGARCH64 || TARGET_RISCV64
    [Test]
    public static void RegisterUnionViewsPreserveTheOtherWordAndUnusedBits()
    {
        var descriptor = new Descriptor();
        ref var address = ref descriptor.idAddr();
        var words = MemoryMarshal.Cast<byte, uint>(
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref address, 1)));
        words[0] = 0xA5A5A5A5;
        words[1] = 0x5A5A5A5A;
#if TARGET_LOONGARCH64
        const int registerWord = 1;
#else
        const int registerWord = 0;
#endif
        var registerBits = words[registerWord];
        var otherWord = words[1 - registerWord];

        descriptor.idReg3((regNumber)1);
        descriptor.idReg4((regNumber)62);

        Assert.That(Marshal.OffsetOf<Emitter.instrDesc.idAddrUnion>(
            nameof(Emitter.instrDesc.idAddrUnion.iiaRegisterBits)).ToInt32(), Is.EqualTo(registerWord * 4));
        Assert.That(descriptor.idReg3(), Is.EqualTo((regNumber)1));
        Assert.That(descriptor.idReg4(), Is.EqualTo((regNumber)62));
        Assert.That(words[registerWord], Is.EqualTo((registerBits & ~0xFFFu) | 1u | (62u << 6)));
        Assert.That(words[1 - registerWord], Is.EqualTo(otherWord));
    }

    [TestCase(0, 0u)]
    [TestCase(4660, 1383u)]
    [TestCase(32767, 32767u)]
    public static void LocalAddressAndRegisterViewsAliasTheNativeWordInBothDirections(int variable, uint offset)
    {
        var descriptor = new Descriptor();
        ref var address = ref descriptor.idAddr();
        var words = MemoryMarshal.Cast<byte, uint>(
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref address, 1)));
#if TARGET_LOONGARCH64
        const int registerWord = 1;
#else
        const int registerWord = 0;
#endif
        words[1 - registerWord] = 0xA5A5A5A5;
        address.iiaLclVar.initLclVarAddr(variable, offset);
        var registerBits = (uint)variable | (offset << 15);

        Assert.That(Marshal.OffsetOf<Emitter.instrDesc.idAddrUnion>(
            nameof(Emitter.instrDesc.idAddrUnion.iiaLclVar)).ToInt32(), Is.EqualTo(registerWord * 4));
        Assert.That(words[registerWord], Is.EqualTo(registerBits));
        Assert.That(descriptor.idReg3(), Is.EqualTo((regNumber)(registerBits & 63)));
        Assert.That(descriptor.idReg4(), Is.EqualTo((regNumber)((registerBits >> 6) & 63)));

        descriptor.idReg3((regNumber)1);
        descriptor.idReg4((regNumber)62);

        Assert.That(address.iiaLclVar.lvaVarNum(), Is.EqualTo((variable & ~0xFFF) | 1 | (62 << 6)));
        Assert.That(address.iiaLclVar.lvaOffset(), Is.EqualTo(offset));
        Assert.That(words[registerWord], Is.EqualTo((registerBits & ~0xFFFu) | 1u | (62u << 6)));
        Assert.That(words[1 - registerWord], Is.EqualTo(0xA5A5A5A5u));
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(4u)]
    [TestCase(16u)]
#if TARGET_RISCV64
    [TestCase(32u)]
#endif
    public static void MultiInstructionDescriptorsRetainTheNativeCodeSize(uint size)
    {
        var descriptor = new Descriptor();
        descriptor.idCodeSize(size);
        descriptor.idReg1((regNumber)1);
        descriptor.idReg2((regNumber)2);
        descriptor.idReg3((regNumber)3);
        descriptor.idReg4((regNumber)4);
        descriptor.idSmallCns(-1);
        descriptor.idInsOpt((insOpts)63);

        Assert.That(descriptor.idCodeSize(), Is.EqualTo(size));
        Assert.That(descriptor.idReg1(), Is.EqualTo((regNumber)1));
        Assert.That(descriptor.idReg2(), Is.EqualTo((regNumber)2));
        Assert.That(descriptor.idReg3(), Is.EqualTo((regNumber)3));
        Assert.That(descriptor.idReg4(), Is.EqualTo((regNumber)4));
        Assert.That(descriptor.idInsOpt(), Is.EqualTo((insOpts)63));
        Assert.That(descriptor.idSmallCns(), Is.EqualTo(-1));
    }

#if !DEBUG
    [TestCase(32u)]
    [TestCase(64u)]
    [TestCase(uint.MaxValue)]
    public static void MultiInstructionSizesKeepTheNativeStorageWidthWithoutAnExtraPostcondition(uint size)
    {
        var descriptor = new Descriptor();
        descriptor.idCodeSize(size);

#if TARGET_LOONGARCH64
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(size & 31));
#else
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(size & 63));
#endif
    }

    [TestCase(64u)]
    [TestCase(uint.MaxValue)]
    public static void RegisterAndOptionWritesTruncateTheirOwnFieldsOnly(uint value)
    {
        var descriptor = new Descriptor();
        descriptor.idReg3((regNumber)3);
        descriptor.idReg4((regNumber)4);
        descriptor.idInsOpt((insOpts)value);
        descriptor.idReg3(unchecked((regNumber)value));
        var storedRegister = (regNumber)(value & 63);

        Assert.That(descriptor.idReg3(), Is.EqualTo(storedRegister));
        Assert.That(descriptor.idReg4(), Is.EqualTo((regNumber)4));
        Assert.That(descriptor.idInsOpt(), Is.EqualTo((insOpts)(value & 63)));

        descriptor.idReg4(unchecked((regNumber)value));

        Assert.That(descriptor.idReg3(), Is.EqualTo(storedRegister));
        Assert.That(descriptor.idReg4(), Is.EqualTo(storedRegister));
    }
#else
    [Test]
    public static void MultiInstructionSizeAssertionPrecedesTheNativeWidthTruncation()
    {
        var descriptor = new Descriptor();
        descriptor.idCodeSize(4);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordCodeSizeAssertion;
        var context = new CodeSizeAssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var previousDescriptor = s_codeSizeDescriptor;
        s_codeSizeDescriptor = descriptor;
        try
        {
            descriptor.idCodeSize(uint.MaxValue);

            Assert.That(context.Assertions, Is.EqualTo(1));
            Assert.That(context.ObservedPayload, Is.EqualTo(4u));
#if TARGET_LOONGARCH64
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(31u));
#else
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(63u));
#endif
        }
        finally
        {
            s_codeSizeDescriptor = previousDescriptor;
        }
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(2u)]
    public static void RegisterAndOptionAssertionsObserveTheirTruncatedField(uint operand)
    {
        var descriptor = new Descriptor();
        descriptor.idInsOpt((insOpts)2);
        descriptor.idReg3((regNumber)3);
        descriptor.idReg4((regNumber)4);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordRegisterOptionAssertion;
        var context = new CodeSizeAssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var previousDescriptor = s_codeSizeDescriptor;
        var previousOperand = s_observeOperand;
        s_codeSizeDescriptor = descriptor;
        s_observeOperand = operand;
        try
        {
            switch (operand)
            {
                case 0:
                {
                    descriptor.idInsOpt((insOpts)64);
                    break;
                }

                case 1:
                {
                    descriptor.idReg3((regNumber)64);
                    break;
                }

                case 2:
                {
                    descriptor.idReg4((regNumber)64);
                    break;
                }
            }

            Assert.That(context.Assertions, Is.EqualTo(1));
            Assert.That(context.ObservedPayload, Is.Zero);
            Assert.That(descriptor.idInsOpt(), Is.EqualTo((insOpts)(operand == 0 ? 0 : 2)));
            Assert.That(descriptor.idReg3(), Is.EqualTo((regNumber)(operand == 1 ? 0 : 3)));
            Assert.That(descriptor.idReg4(), Is.EqualTo((regNumber)(operand == 2 ? 0 : 4)));
        }
        finally
        {
            s_codeSizeDescriptor = previousDescriptor;
            s_observeOperand = previousOperand;
        }
    }

    private static Descriptor? s_codeSizeDescriptor;
    private static uint s_observeOperand;

    private struct CodeSizeAssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
        public uint ObservedPayload;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordCodeSizeAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (CodeSizeAssertionContext*)self;
        context->Assertions++;
        if (s_codeSizeDescriptor is Descriptor descriptor)
        {
            context->ObservedPayload = descriptor.idCodeSize();
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordRegisterOptionAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (CodeSizeAssertionContext*)self;
        context->Assertions++;
        if (s_codeSizeDescriptor is Descriptor descriptor)
        {
            context->ObservedPayload = s_observeOperand switch
            {
                0 => (uint)descriptor.idInsOpt(),
                1 => (uint)descriptor.idReg3(),
                _ => (uint)descriptor.idReg4(),
            };
        }

        return 0;
    }
#endif
#endif

    private sealed class Descriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize => throw new NotSupportedException();
    }
}
