// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64InstructionOutputTests
{
    [TestCase(-5, false, 16u)]
    [TestCase(0, false, 16u)]
    [TestCase(7, false, 16u)]
    [TestCase(0, true, 8u)]
    public static void DefaultDispatchUsesSveAndReturnsDescriptorSize(int alias, bool small, uint descriptorSize)
    {
        var emitter = NewEmitter();
        emitter.writeableOffset = alias;
        var id = OutputEmitter.Descriptor(INS_sve_setffr, IF_SVE_DQ_0A, EA_SCALABLE);
        if (small)
        {
            id.idSetIsSmallDsc();
        }
        var buffer = stackalloc byte[32];
        foreach (var usePublicEntry in new[] { false, true })
        {
            new Span<byte>(buffer, 32).Fill(0xA5);
            var cursor = buffer + 12;
            var result = Output(emitter, new insGroup(), id, &cursor, usePublicEntry);
            Assert.That(result, Is.EqualTo((nuint)descriptorSize));
            Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 16)));
            Assert.That(Unsafe.ReadUnaligned<uint>(unchecked(buffer + 12 + alias)), Is.EqualTo(0x252C9000u));
            for (var i = 0; i < 32; i++)
            {
                if (i < 12 + alias || i >= 16 + alias)
                {
                    Assert.That(buffer[i], Is.EqualTo(0xA5));
                }
            }
        }
    }

    [TestCase(INS_ret, IF_BR_1A, 0xD65F0000u, true)]
    [TestCase(INS_nop, IF_SN_0A, 0xD503201Fu, false)]
    [TestCase(INS_add, IF_DI_2A, 0x91000020u, false)]
    [TestCase(INS_str, IF_LS_2A, 0xF9000020u, false)]
    public static void OrdinaryOpcodeRecordsItsNativeEncoding(
        instruction ins, Emitter.insFormat format, uint expected, bool isSmallDescriptor)
    {
        var emitter = NewEmitter();
#if DEBUG
        emitter.emitIssuing = true;
#endif
        var id = OutputEmitter.Descriptor(ins, format, EA_8BYTE);
        if (isSmallDescriptor)
        {
            id.idSetIsSmallDsc();
        }
        var buffer = stackalloc byte[12];
        foreach (var usePublicEntry in new[] { false, true })
        {
            new Span<byte>(buffer, 12).Fill(0xA5);
            var cursor = buffer + 4;
            var slot = (nint)(&cursor);
            var size = Output(emitter, new insGroup(), id, (byte**)slot, usePublicEntry);
            Assert.That(size, Is.EqualTo((nuint)id.NativeLogicalSize));
            Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 8)));
            Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 4), Is.EqualTo(expected));
            Assert.That(buffer[3], Is.EqualTo(0xA5));
            Assert.That(buffer[8], Is.EqualTo(0xA5));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BoundJumpDescriptorsReachArm64LongJumpOutput(bool usePublicEntry)
    {
        var emitter = NewEmitter();
        var buffer = stackalloc byte[64];
        new Span<byte>(buffer, 64).Fill(0xA5);
        emitter.emitCodeBlock = buffer;
        emitter.emitTotalHotCodeSize = 128;
        var id = OutputEmitter.Jump(INS_cbz, IF_LARGEJMP, EA_8BYTE, 48);
        var cursor = buffer + 16;
        var slot = (nint)(&cursor);

        var size = Output(emitter, new insGroup(), id, (byte**)slot, usePublicEntry);

        Assert.That(size, Is.EqualTo((nuint)id.NativeLogicalSize));
        Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 24)));
        Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 16), Is.EqualTo(0xB5000043u));
        Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 20), Is.EqualTo(0x14000007u));
        Assert.That(buffer[15], Is.EqualTo(0xA5));
        Assert.That(buffer[24], Is.EqualTo(0xA5));
    }

#if FEATURE_LOOP_ALIGN
    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyAlignmentSkipsEncodingAndCommitsAnUnchangedCursor(bool hasAlign)
    {
        var emitter = NewEmitter();
        var id = OutputEmitter.EmptyAlignment();
        var group = new insGroup { igFlags = hasAlign ? InsGroupFlags.HasAlign : 0 };
        var buffer = stackalloc byte[12];
        new Span<byte>(buffer, 12).Fill(0xA5);
        var cursor = buffer + 4;
#if DEBUG
        nuint result = 0;
        var slot = (nint)(&cursor);
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = emitter.emitOutputInstr(group, id, (byte**)slot));
        Assert.That(assertions, Is.Empty);
        Assert.That(result, Is.EqualTo((nuint)48));
#else
        var result = emitter.emitOutputInstr(group, id, &cursor);
        Assert.That(result, Is.EqualTo((nuint)40));
#endif
        Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 4)));
        Assert.That(new ReadOnlySpan<byte>(buffer, 12).ToArray(), Is.All.EqualTo(0xA5));
    }
#endif

    [TestCase(0x79800000u, EA_4BYTE, 0x00400000u)]
    [TestCase(0x79800000u, EA_8BYTE, 0u)]
    [TestCase(0xB9800000u, EA_4BYTE, 0u)]
    [TestCase(0xB9800000u, EA_8BYTE, 0u)]
    [TestCase(0xB9400000u, EA_4BYTE, 0u)]
    [TestCase(0xB9400000u, EA_8BYTE, 0x40000000u)]
    public static void ScalarLoadStoreWidthRetainsSignExtensionSelection(uint code, emitAttr size, uint expected)
    {
        Assert.That(LoadStoreSize(null, code, size), Is.EqualTo(expected));
    }

    [TestCase(0x1C000000u, EA_4BYTE, 0x04000000u)]
    [TestCase(0x1C000000u, EA_8BYTE, 0x44000000u)]
    [TestCase(0x1C000000u, EA_16BYTE, 0x84000000u)]
    [TestCase(0x3D400000u, EA_1BYTE, 0x04000000u)]
    [TestCase(0x3D400000u, EA_2BYTE, 0x44000000u)]
    [TestCase(0x3D400000u, EA_4BYTE, 0x84000000u)]
    [TestCase(0x3D400000u, EA_8BYTE, 0xC4000000u)]
    [TestCase(0x3D400000u, EA_16BYTE, 0x04800000u)]
    public static void VectorLoadStoreWidthDistinguishesLiteralAndNonLiteral(uint code, emitAttr size, uint expected)
    {
        Assert.That(VectorLoadStoreSize(null, code, size), Is.EqualTo(expected));
    }

    [TestCase(EA_1BYTE, 15, 0x40001C00u)]
    [TestCase(EA_2BYTE, 7, 0x40005800u)]
    [TestCase(EA_4BYTE, 3, 0x40009000u)]
    [TestCase(EA_8BYTE, 1, 0x40008400u)]
    public static void VectorMemoryIndexSplitsLaneBits(emitAttr size, int index, uint expected)
    {
        Assert.That(MemoryIndex(null, size, index), Is.EqualTo(expected));
    }

    [TestCase(EA_1BYTE, 15, 0x001F0000u)]
    [TestCase(EA_2BYTE, 7, 0x001E0000u)]
    [TestCase(EA_4BYTE, 3, 0x001C0000u)]
    [TestCase(EA_8BYTE, 1, 0x00180000u)]
    public static void VectorIndexCombinesSizeMarkerAndLane(emitAttr size, int index, uint expected)
    {
        Assert.That(VectorIndex(null, size, index), Is.EqualTo(expected));
    }

    [TestCase(EA_2BYTE, 7, 0x00300800u)]
    [TestCase(EA_4BYTE, 3, 0x00200800u)]
    public static void IndexedMultiplySplitsLMHBits(emitAttr size, int index, uint expected)
    {
        Assert.That(MultiplyIndex(null, size, index), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_NONE, 0u)]
    [TestCase(INS_OPTS_POST_INDEX, 0x400u)]
    [TestCase(INS_OPTS_PRE_INDEX, 0xC00u)]
    public static void MemoryWritebackEncodesNativePreAndPostIndex(insOpts opt, uint expected)
    {
        Assert.That(Indexed(null, opt), Is.EqualTo(expected));
    }

    [TestCase(INS_ldp, INS_OPTS_NONE, 0x01000000u)]
    [TestCase(INS_stp, INS_OPTS_POST_INDEX, 0x00800000u)]
    [TestCase(INS_stp, INS_OPTS_PRE_INDEX, 0x01800000u)]
    [TestCase(INS_ldnp, INS_OPTS_NONE, 0u)]
    [TestCase(INS_stnp, INS_OPTS_NONE, 0u)]
    public static void PairWritebackRetainsNonTemporalOpcodeDifference(instruction ins, insOpts opt, uint expected)
    {
        Assert.That(PairIndexed(null, ins, opt), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_H_TO_S, IF_DV_2J, 0x00C00000u)]
    [TestCase(INS_OPTS_D_TO_H, IF_DV_2J, 0x00418000u)]
    [TestCase(INS_OPTS_H_TO_8BYTE, IF_DV_2H, 0x80C00000u)]
    [TestCase(INS_OPTS_8BYTE_TO_D, IF_DV_2I, 0x80400000u)]
    public static void ConversionKeepsScalarWidthAndFtypeBits(insOpts opt, Emitter.insFormat format, uint expected)
    {
        Assert.That(Convert(null, format, opt), Is.EqualTo(expected));
    }

    [TestCase(INS_sve_setffr, IF_SVE_DQ_0A, false)]
    [TestCase(INS_sve_clasta, IF_SVE_CO_3A, true)]
    [TestCase(INS_add, IF_DI_2A, true)]
    [TestCase(INS_fmov, IF_DV_2H, true)]
    [TestCase(INS_fmov, IF_DV_2I, false)]
    [TestCase(INS_str, IF_LS_2A, false)]
    [TestCase(INS_ldr, IF_LS_2A, true)]
    [TestCase(INS_ldadd, IF_LS_3E, true)]
    public static void GcDestinationClassificationRetainsRegisterAndMemoryBanks(
        instruction ins, Emitter.insFormat format, bool expected)
    {
        Assert.That(MayWriteGc(NewEmitter(), OutputEmitter.Descriptor(ins, format, EA_8BYTE)), Is.EqualTo(expected));
    }

#if DEBUG
    [Test]
    public static void InvalidConvertOptionAssertsAndRetainsNativeZeroContinuation()
    {
        var result = uint.MaxValue;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() => result = Convert(null, IF_DV_2J, INS_OPTS_NONE));
        Assert.That(assertions, Is.EqualTo<string[]>(["!\"Invalid 'conversion' value\""]));
        Assert.That(result, Is.Zero);
    }

    [Test]
    public static void NonTemporalPairAssertsInvalidWritebackBeforeReturningNativeZeroBits()
    {
        var result = uint.MaxValue;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = PairIndexed(null, INS_stnp, INS_OPTS_PRE_INDEX));
        Assert.That(assertions, Is.EqualTo<string[]>(["insOptsNone(opt)"]));
        Assert.That(result, Is.Zero);
    }

    [Test]
    public static void WrongConversionFormatAssertsBeforeReturningTheNativeSelectedEncoding()
    {
        uint result = 0;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = Convert(null, IF_DV_2I, INS_OPTS_H_TO_S));
        Assert.That(assertions, Is.EqualTo<string[]>(["fmt == IF_DV_2J"]));
        Assert.That(result, Is.EqualTo(0x00C00000u));
    }

    [Test]
    public static void InvalidLaneAssertsWithoutMaskingItIntoAValidLane()
    {
        uint result = 0;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = VectorIndex(null, EA_1BYTE, 16));
        Assert.That(assertions, Is.EqualTo<string[]>(["(bits >= 1) && (bits <= 0x1F)"]));
        Assert.That(result, Is.EqualTo(0x00210000u));
    }

    [Test]
    public static void UnboundJumpDescriptorTripsItsBoundAssertion()
    {
        var emitter = NewEmitter();
        var id = OutputEmitter.Jump(INS_cbz, IF_LARGEJMP, EA_8BYTE, 48, isBound: false);
        var buffer = stackalloc byte[64];
        new Span<byte>(buffer, 64).Fill(0xA5);
        emitter.emitCodeBlock = buffer;
        emitter.emitTotalHotCodeSize = 128;
        foreach (var usePublicEntry in new[] { false, true })
        {
            var cursor = buffer + 16;
            var slot = (nint)(&cursor);
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
                () => _ = Output(emitter, new insGroup(), id, (byte**)slot, usePublicEntry));
            Assert.That(assertions, Is.EqualTo<string[]>(["id.idIsBound()"]));
            Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 24)));
            Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 16), Is.EqualTo(0xB5000043u));
            Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 20), Is.EqualTo(0x14000007u));
        }
    }
#endif

    private static nuint Output(Emitter emitter, insGroup group, Emitter.instrDesc id, byte** cursor,
        bool usePublicEntry)
    {
        return usePublicEntry ? emitter.emitOutputInstr(group, id, cursor) : Driver(emitter, group, id, cursor);
    }

    private static OutputEmitter NewEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;

        return new OutputEmitter(codeGen, compiler);
    }

    private sealed class OutputEmitter : Emitter
    {
        internal OutputEmitter(CodeGen codeGen, Compiler compiler) : base(codeGen)
        {
            _compiler = compiler;
        }

        internal static instrDesc Descriptor(instruction ins, Emitter.insFormat format, emitAttr size)
        {
            var id = new instrDescBasic();
            id.idIns(ins);
            id.idInsFmt(format);
            id.idOpSize(size);
            id.idInsOpt(INS_OPTS_NONE);
            id.idReg1(REG_R0);
            id.idReg2(REG_R1);
            id.idReg3(REG_R2);
            id.idSmallCns(0);

            return id;
        }

        internal static instrDescJmp Jump(instruction ins, Emitter.insFormat format, emitAttr size, uint target,
            bool isBound = true)
        {
            var id = new instrDescJmp();
            id.idIns(ins);
            id.idInsFmt(format);
            id.idOpSize(size);
            id.idInsOpt(INS_OPTS_NONE);
            id.idReg1(REG_R3);
            if (isBound)
            {
                id.idSetIsBound();
            }
            id.idjTargetIG = new insGroup { igOffs = target };
#if DEBUG
            id.idDebugOnlyInfo(new instrDescDebugInfo());
#endif

            return id;
        }

#if FEATURE_LOOP_ALIGN
        internal static instrDesc EmptyAlignment()
        {
            var id = new instrDescAlign();
            id.idIns(INS_align);
            id.idInsFmt(IF_SN_0A);
            id.idInsOpt(INS_OPTS_NONE);

            return id;
        }
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitOutputInstrArm64")]
    private static extern nuint Driver(Emitter emitter, insGroup ig, Emitter.instrDesc id, byte** cursor);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeDatasizeLS")]
    private static extern uint LoadStoreSize(Emitter? emitter, uint code, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeDatasizeVLS")]
    private static extern uint VectorLoadStoreSize(Emitter? emitter, uint code, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeVLSIndex")]
    private static extern uint MemoryIndex(Emitter? emitter, emitAttr size, nint index);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeVectorIndex")]
    private static extern uint VectorIndex(Emitter? emitter, emitAttr size, nint index);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeVectorIndexLMH")]
    private static extern uint MultiplyIndex(Emitter? emitter, emitAttr size, nint index);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeIndexedOpt")]
    private static extern uint Indexed(Emitter? emitter, insOpts opt);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodePairIndexedOpt")]
    private static extern uint PairIndexed(Emitter? emitter, instruction ins, insOpts opt);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeConvertOpt")]
    private static extern uint Convert(Emitter? emitter, Emitter.insFormat format, insOpts opt);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsMayWriteToGCRegArm64")]
    private static extern bool MayWriteGc(Emitter emitter, Emitter.instrDesc id);
}
#endif
