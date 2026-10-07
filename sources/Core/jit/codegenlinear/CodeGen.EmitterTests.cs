// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEmitterUnitTests()
    {
        if (!JitConfig.JitEmitUnitTests.contains(_compiler.info.compMethodHnd, _compiler.info.compClassHnd,
            &_compiler.info.compMethodInfo->args))
        {
            return;
        }

        var unitTestSection = JitConfig.JitEmitUnitTestsSections;
        if (unitTestSection is null)
        {
            return;
        }

        JITDUMP("*************** In genEmitterUnitTests()\n");

        var skipLabel = genCreateTempLabel();
#if TARGET_WASM
        genEmitWasmEmitterTestSkipBlock();
#else
        inst_JMP(EJ_jmp, skipLabel);
#endif
        instGen(INS_nop);

        var sections = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(unitTestSection);
        var all = sections.IndexOf("all"u8) >= 0;
#if TARGET_AMD64
        if (all || (sections.IndexOf("sse2"u8) >= 0))
        {
            genAmd64EmitterUnitTestsSse2();
        }
        if (all || (sections.IndexOf("apx"u8) >= 0))
        {
            genAmd64EmitterUnitTestsApx();
        }
        if (all || (sections.IndexOf("avx10v2"u8) >= 0))
        {
            genAmd64EmitterUnitTestsAvx10v2();
        }
        if (all || (sections.IndexOf("ccmp"u8) >= 0))
        {
            genAmd64EmitterUnitTestsCCMP();
        }
        if (all || (sections.IndexOf("cfcmov"u8) >= 0))
        {
            genAmd64EmitterUnitTestsCFCMOV();
        }
        if (all || (sections.IndexOf("ctest"u8) >= 0))
        {
            genAmd64EmitterUnitTestsCTEST();
        }
#elif TARGET_ARM64
        if (all || (sections.IndexOf("general"u8) >= 0))
        {
            genArm64EmitterUnitTestsGeneral();
        }
        if (all || (sections.IndexOf("advsimd"u8) >= 0))
        {
            genArm64EmitterUnitTestsAdvSimd();
        }
        if (all || (sections.IndexOf("fp16"u8) >= 0))
        {
            genArm64EmitterUnitTestsFp16();
        }
        if (all || (sections.IndexOf("sve"u8) >= 0))
        {
            genArm64EmitterUnitTestsSve();
        }
        if (all || (sections.IndexOf("pac"u8) >= 0))
        {
            genArm64EmitterUnitTestsPac();
        }
#elif TARGET_WASM
        if (all || (sections.IndexOf("simd"u8) >= 0))
        {
            genWasmEmitterUnitTestsSimd();
        }
        instGen(INS_end);
#endif

        genDefineTempLabel(skipLabel);
        instGen(INS_nop);
        instGen(INS_nop);
        instGen(INS_nop);
        instGen(INS_nop);

        JITDUMP("*************** End of genEmitterUnitTests()\n");
    }

#if TARGET_ARM64
    private void genArm64EmitterUnitTestsGeneral()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 general emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsAdvSimd()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 AdvSimd emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsFp16()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 FP16 emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsSve()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsPac()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 PAC emitter tests are not ported.");
    }
#endif

#if TARGET_WASM
    private void genEmitWasmEmitterTestSkipBlock()
    {
        GetEmitter().emitIns_BlockTy(INS_block);
        GetEmitter().emitIns_J(INS_br, EA_4BYTE, 0, null);
    }

    private void genWasmEmitterUnitTestsSimd()
    {
        static void pushV128(Emitter emitter, ReadOnlySpan<byte> bytes)
        {
            emitter.emitIns_V128Imm(INS_v128_const, bytes);
        }

        static void pushI32(Emitter emitter, nint value)
        {
            emitter.emitIns_I(INS_i32_const, EA_4BYTE, value);
        }

        static void pushI64(Emitter emitter, nint value)
        {
            emitter.emitIns_I(INS_i64_const, EA_8BYTE, value);
        }

        static void pushF32(Emitter emitter, nint value)
        {
            emitter.emitIns_I(INS_f32_const, EA_4BYTE, value);
        }

        static void pushF64(Emitter emitter, nint value)
        {
            emitter.emitIns_I(INS_f64_const, EA_8BYTE, value);
        }

        static void testUnaryV128(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins)
        {
            pushV128(emitter, bytes);
            emitter.emitIns(ins);
            emitter.emitIns(INS_drop);
        }

        static void testBinaryV128(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins)
        {
            pushV128(emitter, bytes);
            pushV128(emitter, bytes);
            emitter.emitIns(ins);
            emitter.emitIns(INS_drop);
        }

        static void testExtractLane(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins, byte laneIndex)
        {
            pushV128(emitter, bytes);
            emitter.emitIns_Lane(ins, laneIndex);
            emitter.emitIns(INS_drop);
        }

        static void testReplaceLaneI32(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins, byte laneIndex)
        {
            pushV128(emitter, bytes);
            pushI32(emitter, 42);
            emitter.emitIns_Lane(ins, laneIndex);
            emitter.emitIns(INS_drop);
        }

        static void testReplaceLaneI64(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins, byte laneIndex)
        {
            pushV128(emitter, bytes);
            pushI64(emitter, 42);
            emitter.emitIns_Lane(ins, laneIndex);
            emitter.emitIns(INS_drop);
        }

        static void testReplaceLaneF32(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins, byte laneIndex)
        {
            pushV128(emitter, bytes);
            pushF32(emitter, 0);
            emitter.emitIns_Lane(ins, laneIndex);
            emitter.emitIns(INS_drop);
        }

        static void testReplaceLaneF64(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins, byte laneIndex)
        {
            pushV128(emitter, bytes);
            pushF64(emitter, 0);
            emitter.emitIns_Lane(ins, laneIndex);
            emitter.emitIns(INS_drop);
        }

        static void testLoadLane(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins, emitAttr attr,
            nint offset, byte laneIndex)
        {
            pushI32(emitter, 0);
            pushV128(emitter, bytes);
            emitter.emitIns_MemargLane(ins, attr, offset, laneIndex);
            emitter.emitIns(INS_drop);
        }

        static void testStoreLane(Emitter emitter, ReadOnlySpan<byte> bytes, instruction ins, emitAttr attr,
            nint offset, byte laneIndex)
        {
            pushI32(emitter, 0);
            pushV128(emitter, bytes);
            emitter.emitIns_MemargLane(ins, attr, offset, laneIndex);
        }

        static void testShuffle(Emitter emitter, ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> shuffleBytes)
        {
            pushV128(emitter, bytes);
            pushV128(emitter, bytes);
            emitter.emitIns_V128Imm(INS_i8x16_shuffle, shuffleBytes);
            emitter.emitIns(INS_drop);
        }

        var emitter = GetEmitter();
        ReadOnlySpan<byte> v128Bytes =
        [
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        ];
        ReadOnlySpan<byte> v128Zeros = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        ReadOnlySpan<byte> v128Ones =
        [
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        ];

        pushV128(emitter, v128Bytes);
        emitter.emitIns(INS_drop);
        pushV128(emitter, v128Zeros);
        emitter.emitIns(INS_drop);
        pushV128(emitter, v128Ones);
        emitter.emitIns(INS_drop);

        testExtractLane(emitter, v128Ones, INS_i8x16_extract_lane_s, 0);
        testExtractLane(emitter, v128Ones, INS_i8x16_extract_lane_u, 15);
        testReplaceLaneI32(emitter, v128Ones, INS_i8x16_replace_lane, 7);

        testExtractLane(emitter, v128Ones, INS_i16x8_extract_lane_s, 0);
        testExtractLane(emitter, v128Ones, INS_i16x8_extract_lane_u, 7);
        testReplaceLaneI32(emitter, v128Ones, INS_i16x8_replace_lane, 3);

        testExtractLane(emitter, v128Ones, INS_i32x4_extract_lane, 0);
        testReplaceLaneI32(emitter, v128Ones, INS_i32x4_replace_lane, 3);

        testExtractLane(emitter, v128Ones, INS_i64x2_extract_lane, 0);
        testReplaceLaneI64(emitter, v128Ones, INS_i64x2_replace_lane, 1);

        testExtractLane(emitter, v128Ones, INS_f32x4_extract_lane, 3);
        testReplaceLaneF32(emitter, v128Ones, INS_f32x4_replace_lane, 0);

        testExtractLane(emitter, v128Ones, INS_f64x2_extract_lane, 0);
        testReplaceLaneF64(emitter, v128Ones, INS_f64x2_replace_lane, 1);

        testLoadLane(emitter, v128Ones, INS_v128_load8_lane, EA_1BYTE, 0, 5);
        testLoadLane(emitter, v128Ones, INS_v128_load16_lane, EA_2BYTE, 16, 3);
        testLoadLane(emitter, v128Ones, INS_v128_load32_lane, EA_4BYTE, 64, 2);
        testLoadLane(emitter, v128Ones, INS_v128_load64_lane, EA_8BYTE, 128, 1);
        testStoreLane(emitter, v128Ones, INS_v128_store8_lane, EA_1BYTE, 0, 0);
        testStoreLane(emitter, v128Ones, INS_v128_store16_lane, EA_2BYTE, 8, 7);
        testStoreLane(emitter, v128Ones, INS_v128_store32_lane, EA_4BYTE, 32, 1);
        testStoreLane(emitter, v128Ones, INS_v128_store64_lane, EA_8BYTE, 256, 0);

        ReadOnlySpan<byte> identityShuffle = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        testShuffle(emitter, v128Bytes, identityShuffle);

        ReadOnlySpan<byte> reverseShuffle = [15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0];
        testShuffle(emitter, v128Bytes, reverseShuffle);

        ReadOnlySpan<byte> crossShuffle = [0, 17, 2, 19, 4, 21, 6, 23, 8, 25, 10, 27, 12, 29, 14, 31];
        testShuffle(emitter, v128Bytes, crossShuffle);

        pushI32(emitter, 1);
        emitter.emitIns(INS_i8x16_splat);
        emitter.emitIns(INS_drop);

        pushI32(emitter, 2);
        emitter.emitIns(INS_i16x8_splat);
        emitter.emitIns(INS_drop);

        pushI32(emitter, 3);
        emitter.emitIns(INS_i32x4_splat);
        emitter.emitIns(INS_drop);

        pushI64(emitter, 4);
        emitter.emitIns(INS_i64x2_splat);
        emitter.emitIns(INS_drop);

        pushF32(emitter, 0);
        emitter.emitIns(INS_f32x4_splat);
        emitter.emitIns(INS_drop);

        pushF64(emitter, 0);
        emitter.emitIns(INS_f64x2_splat);
        emitter.emitIns(INS_drop);

        testBinaryV128(emitter, v128Ones, INS_i8x16_swizzle);
        testBinaryV128(emitter, v128Ones, INS_i8x16_eq);
        testBinaryV128(emitter, v128Ones, INS_i32x4_ne);
        testBinaryV128(emitter, v128Ones, INS_f64x2_lt);
        testBinaryV128(emitter, v128Ones, INS_i8x16_add);
        testBinaryV128(emitter, v128Ones, INS_i32x4_mul);
        testUnaryV128(emitter, v128Ones, INS_f32x4_sqrt);
        testUnaryV128(emitter, v128Ones, INS_f64x2_neg);
        testUnaryV128(emitter, v128Ones, INS_v128_not);
        testBinaryV128(emitter, v128Ones, INS_v128_and);
        testBinaryV128(emitter, v128Ones, INS_v128_or);
        testBinaryV128(emitter, v128Ones, INS_v128_xor);
        testBinaryV128(emitter, v128Ones, INS_v128_andnot);
        testUnaryV128(emitter, v128Ones, INS_v128_any_true);
        testUnaryV128(emitter, v128Ones, INS_i8x16_all_true);
        testUnaryV128(emitter, v128Ones, INS_i32x4_bitmask);
        testUnaryV128(emitter, v128Ones, INS_f32x4_convert_s_i32x4);
        testUnaryV128(emitter, v128Ones, INS_f64x2_convert_low_u_i32x4);
        testUnaryV128(emitter, v128Ones, INS_i32x4_trunc_sat_s_f32x4);
    }
#endif
}
#endif
