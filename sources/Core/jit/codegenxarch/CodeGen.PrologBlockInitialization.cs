// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_AMD64
    private instruction simdAlignedMovIns()
    {
        // VEX removes MOVAPS's size advantage; MOVDQA can use more ports on older CPUs.
        return _compiler.canUseVexEncoding() ? INS_movdqa32 : INS_movaps;
    }
#endif

    public void genZeroInitFrameUsingBlockInit(int untrLclHi, int untrLclLo, regNumber initReg, ref bool initRegZeroed)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog block initialization requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(UseBlockInit);
        assert(untrLclHi > untrLclLo);
        var frameReg = genFramePointerReg();
        var zeroReg = REG_NA;
        var blkSize = untrLclHi - untrLclLo;
        assert(blkSize >= 0);
        noway_assert((blkSize % sizeof(int)) == 0);
        assert((regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask) & _calleeRegArgMaskLiveIn).IsEmpty);

        var simdMov = simdAlignedMovIns();
        var alignedLclLo = (untrLclLo + XMM_REGSIZE_BYTES - 1) & -XMM_REGSIZE_BYTES;
        if ((untrLclLo != alignedLclLo) && (blkSize < 2 * XMM_REGSIZE_BYTES))
        {
            assert(alignedLclLo - untrLclLo < XMM_REGSIZE_BYTES);
            simdMov = simdUnalignedMovIns();
        }

        if (blkSize < XMM_REGSIZE_BYTES)
        {
            zeroReg = genGetZeroReg(initReg, ref initRegZeroed);
            var i = 0;
            for (; i + REGSIZE_BYTES <= blkSize; i += REGSIZE_BYTES)
            {
                Emitter.emitIns_AR_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, zeroReg, frameReg, untrLclLo + i);
            }

            assert((i == blkSize) || (i + sizeof(int) == blkSize));
            if (i != blkSize)
            {
                Emitter.emitIns_AR_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, frameReg, untrLclLo + i);
                i += sizeof(int);
            }
            assert(i == blkSize);
        }
        else
        {
            // Windows AMD64's first non-argument, caller-saved SIMD register is XMM4.
            var zeroSimdReg = REG_XMM4;
            int alignedLclHi;
            int alignmentHiBlkSize;
            if ((blkSize < 2 * XMM_REGSIZE_BYTES) || (untrLclLo == alignedLclLo))
            {
                var alignmentBlkSize = blkSize & -XMM_REGSIZE_BYTES;
                alignmentHiBlkSize = blkSize - alignmentBlkSize;
                alignedLclHi = untrLclLo + alignmentBlkSize;
                alignedLclLo = untrLclLo;
                blkSize = alignmentBlkSize;
                assert(blkSize + alignmentHiBlkSize == untrLclHi - untrLclLo);
            }
            else
            {
                alignedLclHi = untrLclHi & -XMM_REGSIZE_BYTES;
                alignmentHiBlkSize = untrLclHi - alignedLclHi;
                var alignmentLoBlkSize = alignedLclLo - untrLclLo;
                blkSize = alignedLclHi - alignedLclLo;
                assert(blkSize + alignmentLoBlkSize + alignmentHiBlkSize == untrLclHi - untrLclLo);
                assert((alignmentLoBlkSize > 0) && (alignmentLoBlkSize < XMM_REGSIZE_BYTES));
                assert(alignedLclLo - alignmentLoBlkSize == untrLclLo);

                zeroReg = genGetZeroReg(initReg, ref initRegZeroed);
                var i = 0;
                for (; i + REGSIZE_BYTES <= alignmentLoBlkSize; i += REGSIZE_BYTES)
                {
                    Emitter.emitIns_AR_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, zeroReg, frameReg, untrLclLo + i);
                }

                assert((i == alignmentLoBlkSize) || (i + sizeof(int) == alignmentLoBlkSize));
                if (i != alignmentLoBlkSize)
                {
                    Emitter.emitIns_AR_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, frameReg, untrLclLo + i);
                    i += sizeof(int);
                }
                assert(i == alignmentLoBlkSize);
            }

            var maxSimdSize = _compiler.roundDownSimdSize((uint)blkSize);
            assert((maxSimdSize >= XMM_REGSIZE_BYTES) && (maxSimdSize <= ZMM_REGSIZE_BYTES));
            // Three stores per loop iteration: use a loop only for at least two iterations.
            if (blkSize < 6 * maxSimdSize)
            {
                // VEX/EVEX XORPS also clears the upper YMM/ZMM lanes.
                Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, zeroSimdReg, zeroSimdReg, zeroSimdReg, INS_OPTS_NONE);
                assert((blkSize % XMM_REGSIZE_BYTES) == 0);
                var regSize = _compiler.roundDownSimdSize((uint)blkSize);
                var lenRemaining = blkSize;
                while (lenRemaining > 0)
                {
                    // For 112 bytes, two overlapping 64-byte stores replace 64+32+16.
                    if ((regSize > lenRemaining) && !BitOperations.IsPow2(lenRemaining))
                    {
                        lenRemaining = regSize;
                    }

                    regSize = _compiler.roundDownSimdSize((uint)lenRemaining);
                    assert(regSize >= XMM_REGSIZE_BYTES);
                    var ins = regSize > XMM_REGSIZE_BYTES ? simdUnalignedMovIns() : simdMov;
                    var offset = blkSize - lenRemaining;
                    Emitter.emitIns_AR_R(ins, (emitAttr)regSize, zeroSimdReg, frameReg, alignedLclLo + offset);
                    lenRemaining -= regSize;
                }
                assert(lenRemaining == 0);
            }
            else
            {
                Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, zeroSimdReg, zeroSimdReg, zeroSimdReg, INS_OPTS_NONE);
                var extraSimd = blkSize % (XMM_REGSIZE_BYTES * 3) / XMM_REGSIZE_BYTES;
                if (extraSimd != 0)
                {
                    blkSize -= XMM_REGSIZE_BYTES;
                    Emitter.emitIns_AR_R(simdMov, EA_16BYTE, zeroSimdReg, frameReg, alignedLclLo);
                    if (extraSimd == 2)
                    {
                        blkSize -= XMM_REGSIZE_BYTES;
                        Emitter.emitIns_AR_R(simdMov, EA_16BYTE, zeroSimdReg, frameReg, alignedLclLo + XMM_REGSIZE_BYTES);
                    }
                }

                noway_assert((blkSize % (3 * XMM_REGSIZE_BYTES)) == 0);
                assert(blkSize >= 3 * XMM_REGSIZE_BYTES);
                assert(alignedLclHi - blkSize >= untrLclLo);
                assert(alignedLclHi - blkSize + 2 * XMM_REGSIZE_BYTES < untrLclHi - XMM_REGSIZE_BYTES);
                assert(alignedLclHi - 3 * XMM_REGSIZE_BYTES + 2 * XMM_REGSIZE_BYTES <= untrLclHi - XMM_REGSIZE_BYTES);
                assert(alignedLclHi - (blkSize + extraSimd * XMM_REGSIZE_BYTES) == alignedLclLo);

                Emitter.emitIns_R_I(INS_mov, EA_PTRSIZE, initReg, -(nint)blkSize);
                var loopHead = genCreateTempLabel();
                genDefineInlineTempLabel(loopHead);
                Emitter.emitIns_ARX_R(simdMov, EA_16BYTE, zeroSimdReg, frameReg, initReg, 1, alignedLclHi);
                Emitter.emitIns_ARX_R(simdMov, EA_16BYTE, zeroSimdReg, frameReg, initReg, 1, alignedLclHi + XMM_REGSIZE_BYTES);
                Emitter.emitIns_ARX_R(simdMov, EA_16BYTE, zeroSimdReg, frameReg, initReg, 1,
                    alignedLclHi + 2 * XMM_REGSIZE_BYTES);
                Emitter.emitIns_R_I(INS_add, EA_PTRSIZE, initReg, 3 * XMM_REGSIZE_BYTES);
                Emitter.emitIns_ShortJ(INS_jne, loopHead);
                initRegZeroed = true;
            }

            if (untrLclHi != alignedLclHi)
            {
                assert((alignmentHiBlkSize > 0) && (alignmentHiBlkSize < XMM_REGSIZE_BYTES));
                assert(alignedLclHi + alignmentHiBlkSize == untrLclHi);
                zeroReg = genGetZeroReg(initReg, ref initRegZeroed);
                var i = 0;
                for (; i + REGSIZE_BYTES <= alignmentHiBlkSize; i += REGSIZE_BYTES)
                {
                    Emitter.emitIns_AR_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, zeroReg, frameReg, alignedLclHi + i);
                }

                assert((i == alignmentHiBlkSize) || (i + sizeof(int) == alignmentHiBlkSize));
                if (i != alignmentHiBlkSize)
                {
                    Emitter.emitIns_AR_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, frameReg, alignedLclHi + i);
                    i += sizeof(int);
                }
                assert(i == alignmentHiBlkSize);
            }
        }
#endif
    }
}
