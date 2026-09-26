// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenConstantTests
{
    [TestCase(4, TYP_FLOAT)]
    [TestCase(8, TYP_SIMD8)]
    [TestCase(16, TYP_SIMD16)]
    [TestCase(32, TYP_SIMD32)]
    [TestCase(64, TYP_SIMD64)]
    public static void VectorDataCopiesOnlyTheRequestedBytesAndAlignment(int size, var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var value = Pattern(64, 64);
            var expected = value.AsSpan<byte>()[..size].ToArray();
            _ = codeGen.Emitter.emitSimdConst(in value, (emitAttr)size);
            value.u32[0] = 123;

            var section = Section(codeGen);
            Assert.That(section.dsSize, Is.EqualTo(size));
            Assert.That(section.dsAlignment, Is.EqualTo(size));
            Assert.That(section.dsDataType, Is.EqualTo(type));
            Assert.That(section.Data, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void FixedVectorAndMaskConstantsShareTheNativeDataPool()
    {
        WithCodeGen((_, codeGen) =>
        {
            var value = Pattern(16, 16);
            var first = codeGen.Emitter.emitSimd16Const(value.v128[0]);
            var prefix = codeGen.Emitter.emitSimd8Const(value.v64[0]);
            simdmask_t maskValue = default;
            maskValue.u64[0] = value.u64[0];
            var mask = codeGen.Emitter.emitSimdMaskConst(maskValue);

            Assert.That((nuint)prefix, Is.EqualTo((nuint)first));
            Assert.That((nuint)mask, Is.EqualTo((nuint)first));
            Assert.That(Section(codeGen).dsDataType, Is.EqualTo(TYP_SIMD16));
            Assert.That(codeGen.Emitter.emitConsDsc.dsdOffs, Is.EqualTo(16));
        });
    }

    [TestCase(8, 8, 8, true, INS_movsd_simd, 8, 8)]
    [TestCase(8, 8, 4, true, INS_movsd_simd, 8, 8)]
    [TestCase(16, 16, 16, true, INS_movups, 16, 16)]
    [TestCase(32, 32, 32, true, INS_movups, 32, 32)]
    [TestCase(64, 64, 64, true, INS_movups, 64, 64)]
    [TestCase(64, 64, 32, true, INS_vbroadcastf32x8, 32, 64)]
    [TestCase(64, 64, 16, true, INS_vbroadcastf32x4, 16, 64)]
    [TestCase(64, 64, 8, true, INS_vbroadcastsd, 8, 64)]
    [TestCase(64, 64, 4, true, INS_vbroadcastss, 4, 64)]
    [TestCase(32, 32, 16, true, INS_vbroadcastf32x4, 16, 32)]
    [TestCase(32, 32, 8, true, INS_vbroadcastsd, 8, 32)]
    [TestCase(32, 32, 4, true, INS_vbroadcastss, 4, 32)]
    [TestCase(16, 16, 8, true, INS_movddup, 8, 16)]
    [TestCase(16, 16, 4, true, INS_vbroadcastss, 4, 16)]
    [TestCase(16, 16, 4, false, INS_movddup, 8, 16)]
    [TestCase(64, 32, 32, true, INS_movups, 32, 32)]
    [TestCase(64, 32, 4, true, INS_movups, 32, 32)]
    [TestCase(64, 16, 16, true, INS_movups, 16, 16)]
    [TestCase(32, 16, 16, true, INS_movups, 16, 16)]
    [TestCase(16, 8, 8, true, INS_movsd_simd, 8, 8)]
    [TestCase(8, 4, 4, true, INS_movss, 4, 4)]
    [TestCase(64, 4, 4, true, INS_movss, 4, 4)]
    public static void CompressedLoadsPreserveBroadcastPriorityAndZeroExtensionWidths(
        int size, int populated, int period, bool avx, instruction expected, int dataSize, int instructionSize)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            if (!avx)
            {
                compiler.opts.compSupportsISA = default;
                compiler.opts.compSupportsISAExactly = default;
                compiler.opts.compSupportsISAReported = default;
                codeGen.Emitter.UseVexEncodings = false;
                codeGen.Emitter.UseEvexEncodings = false;
            }
            var value = Pattern(populated, period);

            codeGen.Emitter.emitSimdConstCompressedLoad(in value, (emitAttr)size, REG_XMM1);

            var descriptor = Last(codeGen);
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idOpSize(), Is.EqualTo((emitAttr)instructionSize));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(descriptor.idIsDspReloc(), Is.True);
            Assert.That(Section(codeGen).Data, Is.EqualTo(value.AsSpan<byte>()[..dataSize].ToArray()));
            Assert.That(Section(codeGen).dsAlignment, Is.EqualTo(dataSize));
            Assert.That(InstructionCount(codeGen.Emitter), Is.EqualTo(1));
        });
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void ZeroVectorsUseNarrowZeroingWithoutConstantData(var_types type)
    {
        WithCodeGen((_, codeGen) =>
        {
            simd64_t value = default;
            codeGen.genSetRegToConst(REG_XMM1, type, in value);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(INS_xorps));
            Assert.That(Last(codeGen).idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(codeGen.Emitter.emitConsDsc.dsdList, Is.Null);
        });
    }

    [TestCase(TYP_SIMD8, REG_XMM1, INS_pcmpeqd)]
    [TestCase(TYP_SIMD12, REG_XMM1, INS_pcmpeqd)]
    [TestCase(TYP_SIMD16, REG_XMM1, INS_pcmpeqd)]
    [TestCase(TYP_SIMD32, REG_XMM1, INS_pcmpeqd)]
    [TestCase(TYP_SIMD64, REG_XMM1, INS_vpternlogd)]
    [TestCase(TYP_SIMD8, REG_XMM16, INS_vpternlogd)]
    [TestCase(TYP_SIMD12, REG_XMM16, INS_vpternlogd)]
    [TestCase(TYP_SIMD16, REG_XMM16, INS_vpternlogd)]
    [TestCase(TYP_SIMD32, REG_XMM16, INS_vpternlogd)]
    public static void AllBitsSetVectorsSelectTheNativeRegisterAndIsaForms(
        var_types type, regNumber reg, instruction expected)
    {
        WithCodeGen((_, codeGen) =>
        {
            var value = simd64_t.AllBitsSet;
            codeGen.genSetRegToConst(reg, type, in value);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(expected));
            Assert.That(Last(codeGen).idReg1(), Is.EqualTo(reg));
            Assert.That(codeGen.Emitter.emitConsDsc.dsdList, Is.Null);
        });
    }

    [Test]
    public static void Simd32AllBitsSetFallsBackToABroadcastWithoutAvx2()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compSupportsISA = default;
            compiler.opts.compSupportsISAExactly = default;
            compiler.opts.compSupportsISAReported = default;
            Enable(compiler, InstructionSet_AVX);
            codeGen.Emitter.UseEvexEncodings = false;
            var value = simd64_t.AllBitsSet;

            codeGen.genSetRegToConst(REG_XMM1, TYP_SIMD32, in value);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(INS_vbroadcastss));
            Assert.That(Section(codeGen).Data, Is.EqualTo(new byte[] { 255, 255, 255, 255 }));
        });
    }

    [Test]
    public static void Simd12RetainsThePinnedOriginalValueCallsite()
    {
        WithCodeGen((_, codeGen) =>
        {
            var value = Pattern(16, 16);
            codeGen.genSetRegToConst(REG_XMM1, TYP_SIMD12, in value);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(INS_movups));
            Assert.That(Section(codeGen).Data, Is.EqualTo(value.AsSpan<byte>()[..16].ToArray()));
        });
    }

    [TestCase(0UL, INS_kxorq)]
    [TestCase(ulong.MaxValue, INS_kxnorq)]
    [TestCase(0x123456789ABCDEF0UL, INS_kmovq_msk)]
    public static void MaskNodesUseTheirAssignedRegisterAndExactBits(ulong bits, instruction expected)
    {
        WithCodeGen((_, codeGen) =>
        {
            simdmask_t value = default;
            value.u64[0] = bits;
            var tree = new GenTreeMskCon(value) { RegNum = REG_K2 };
            codeGen.genSetRegToConst(REG_K1, TYP_MASK, tree);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(expected));
            Assert.That(Last(codeGen).idReg1(), Is.EqualTo(REG_K2));
            if (expected == INS_kmovq_msk)
            {
                Assert.That(BitConverter.ToUInt64(Section(codeGen).Data), Is.EqualTo(bits));
                Assert.That(Section(codeGen).dsDataType, Is.EqualTo(TYP_MASK));
            }
            else
            {
                Assert.That(codeGen.Emitter.emitConsDsc.dsdList, Is.Null);
            }
        });
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void VectorNodesUseTheirAssignedRegister(var_types type)
    {
        WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeVecCon(type)
            {
                RegNum = REG_XMM2,
                SimdVal = Pattern(type.Size, type.Size),
            };
            codeGen.genSetRegToConst(REG_XMM1, type, tree);
            Assert.That(Last(codeGen).idReg1(), Is.EqualTo(REG_XMM2));
            Assert.That(Section(codeGen).Data, Is.EqualTo(tree.SimdVal.AsSpan<byte>()[..(int)Section(codeGen).dsSize].ToArray()));
        });
    }

    [TestCase(TYP_FLOAT, 0UL, REG_XMM1, INS_xorps)]
    [TestCase(TYP_DOUBLE, 0UL, REG_XMM1, INS_xorps)]
    [TestCase(TYP_FLOAT, 0x8000000000000000UL, REG_XMM1, INS_movss)]
    [TestCase(TYP_DOUBLE, 0x8000000000000000UL, REG_XMM1, INS_movsd_simd)]
    [TestCase(TYP_FLOAT, ulong.MaxValue, REG_XMM1, INS_pcmpeqd)]
    [TestCase(TYP_DOUBLE, ulong.MaxValue, REG_XMM1, INS_pcmpeqd)]
    [TestCase(TYP_DOUBLE, ulong.MaxValue, REG_XMM16, INS_vpternlogd)]
    [TestCase(TYP_DOUBLE, 0x3FF8000000000000UL, REG_XMM1, INS_movsd_simd)]
    public static void FloatingNodesDistinguishSignedZeroAndAllBitsSet(
        var_types type, ulong bits, regNumber reg, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewDconNode(type, BitConverter.UInt64BitsToDouble(bits));
            codeGen.genSetRegToConst(reg, type, tree);
            Assert.That(Last(codeGen).idIns(), Is.EqualTo(expected));
            if (expected is INS_movss or INS_movsd_simd)
            {
                Assert.That(Section(codeGen).dsSize, Is.EqualTo(type.Size));
                Assert.That(BitConverter.ToUInt32(Section(codeGen).Data), Is.EqualTo(
                    type == TYP_FLOAT ? 0x80000000u : unchecked((uint)bits)));
            }
            else
            {
                Assert.That(codeGen.Emitter.emitConsDsc.dsdList, Is.Null);
            }
        });
    }

    [TestCase(TYP_INT, 0L, INS_xor, GCInfo.GCtype.GCT_NONE)]
    [TestCase(TYP_LONG, long.MinValue, INS_mov, GCInfo.GCtype.GCT_NONE)]
    [TestCase(TYP_BYREF, 32L, INS_mov, GCInfo.GCtype.GCT_NONE)]
    [TestCase(TYP_BYREF, 0x100000000L, INS_mov, GCInfo.GCtype.GCT_BYREF)]
    [TestCase(TYP_REF, 0L, INS_xor, GCInfo.GCtype.GCT_GCREF)]
    public static void IntegerNodesKeepWidthsAndGcClassification(
        var_types type, long value, instruction expected, GCInfo.GCtype gcType)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewIconNode(type, unchecked((nint)value));
            codeGen.genSetRegToConst(REG_RAX, type, tree);
            Assert.That(Last(codeGen).idIns(), Is.EqualTo(expected));
            Assert.That(Last(codeGen).idGCref(), Is.EqualTo(gcType));
        });
    }

    [TestCase(GTF_ICON_SECREL_OFFSET, false)]
    [TestCase(GTF_ICON_TLSGD_OFFSET, true)]
    public static void NativeAotOffsetsKeepSectionTagsAndEagerTlsGcDeath(GenTreeFlags flag, bool tls)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.opts.compReloc = true;
            codeGen.RegSet.SetMaskVars(default);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            var tree = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            tree.Flags |= flag;

            codeGen.genSetRegToConst(REG_RCX, TYP_I_IMPL, tree);

            Assert.That(Last(codeGen).idIns(), Is.EqualTo(INS_mov));
            Assert.That(Last(codeGen).idAddr().iiaSecRel, Is.EqualTo(!tls));
            var rax = regMaskTP.CreateFromRegNum(REG_RAX, REG_RAX.SingleTypeMask);
            Assert.That((codeGen.GCInfo.gcRegGCrefSetCur & rax).IsEmpty, Is.EqualTo(tls));
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void DspCodeRecordsConstantsAndUpdatesGcState(int kind)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.dspCode = true;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            var before = codeGen.GCInfo.gcRegGCrefSetCur;
            string diagnostic;
            if (kind == 0)
            {
                compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
                var tree = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
                tree.Flags |= GTF_ICON_TLSGD_OFFSET;
                diagnostic = InstructionRecordingTestSupport.Capture(
                    () => codeGen.genSetRegToConst(REG_RCX, TYP_I_IMPL, tree));
            }
            else if (kind == 1)
            {
                var value = Pattern(16, 16);
                diagnostic = InstructionRecordingTestSupport.Capture(
                    () => codeGen.Emitter.emitSimdConstCompressedLoad(in value, EA_16BYTE, REG_XMM1));
            }
            else if (kind == 2)
            {
                simdmask_t value = default;
                value.u64[0] = 1;
                diagnostic = InstructionRecordingTestSupport.Capture(
                    () => codeGen.genSetRegToConst(REG_K1, TYP_MASK, in value));
            }
            else
            {
                var tree = compiler.gtNewDconNode(TYP_DOUBLE, 1.5);
                diagnostic = InstructionRecordingTestSupport.Capture(
                    () => codeGen.genSetRegToConst(REG_XMM1, TYP_DOUBLE, tree));
            }

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(before));
            Assert.That(InstructionCount(codeGen.Emitter), Is.GreaterThan(0));
            Assert.That(diagnostic, Is.Not.Empty);
        });
    }
#endif

    private static simd64_t Pattern(int populated, int period)
    {
        simd64_t value = default;
        for (var i = 0; i < populated / 4; i++)
        {
            value.u32[i] = (uint)((i % (period / 4)) + 1);
        }

        return value;
    }

    private static void Enable(Compiler compiler, CORINFO_InstructionSet isa)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
            codeGen.RegSet.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            Enable(compiler, InstructionSet_AVX);
            Enable(compiler, InstructionSet_AVX2);
            Enable(compiler, InstructionSet_AVX512);
            action(compiler, codeGen);
        });
    }

    private static Emitter.dataSection Section(CodeGen codeGen)
        => codeGen.Emitter.emitConsDsc.dsdLast ?? throw new AssertionException("No constant data was recorded.");

    private static Emitter.instrDesc Last(CodeGen codeGen)
        => LastInstruction(codeGen.Emitter) ?? throw new AssertionException("No instruction was recorded.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int InstructionCount(Emitter emitter);
}
