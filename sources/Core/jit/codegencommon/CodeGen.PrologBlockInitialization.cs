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

#if !TARGET_ARM64
    public void genZeroInitFrameUsingBlockInit(int untrLclHi, int untrLclLo, regNumber initReg, ref bool initRegZeroed)
    {
#if TARGET_WASM
        unreached();
#elif TARGET_ARM
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(UseBlockInit);
        assert(untrLclHi > untrLclLo);

        var availMask = _regSet.rsGetModifiedRegsMask() | new regMaskTP(SRBM_INT_CALLEE_TRASH);
        availMask &= ~_calleeRegArgMaskLiveIn;
        availMask &= ~genRegMask(initReg);
        if (_compiler.compLocallocUsed)
        {
            availMask &= ~new regMaskTP(SRBM_SAVED_LOCALLOC_SP);
        }

        noway_assert(availMask.IsNonEmpty);
        var rZero2Mask = genFindLowestBit(availMask);
        var rZero2 = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)rZero2Mask.Lower));
        availMask &= ~rZero2Mask;
        assert((genRegMask(rZero2) & _calleeRegArgMaskLiveIn).IsEmpty);

        noway_assert(availMask.IsNonEmpty);
        var rAddrMask = genFindLowestBit(availMask);
        var rAddr = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)rAddrMask.Lower));
        availMask &= ~rAddrMask;

        var cntBytes = unchecked((uint)(untrLclHi - untrLclLo));
        assert((cntBytes % sizeof(int)) == 0);
        var cntSlots = cntBytes / REGSIZE_BYTES;
        var useLoop = cntSlots >= 10;
        var rCnt = REG_NA;
        if (useLoop)
        {
            noway_assert(availMask.IsNonEmpty);
            var rCntMask = genFindLowestBit(availMask);
            rCnt = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)rCntMask.Lower));
            availMask &= ~rCntMask;
        }

        assert((genRegMask(rAddr) & _calleeRegArgMaskLiveIn).IsEmpty);
        if (arm_Valid_Imm_For_Add(untrLclLo, INS_FLAGS_DONT_CARE))
        {
            Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, rAddr, genFramePointerReg(), untrLclLo,
                INS_FLAGS_DONT_CARE);
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, untrLclLo);
            Emitter.emitIns_R_R_R(INS_add, EA_PTRSIZE, rAddr, genFramePointerReg(), initReg);
            initRegZeroed = false;
        }

        if (useLoop)
        {
            noway_assert(cntSlots >= 2);
            assert((genRegMask(rCnt) & _calleeRegArgMaskLiveIn).IsEmpty);
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, rCnt, unchecked((nint)(cntSlots / 2)));
        }

        var rZero1 = genGetZeroReg(initReg, ref initRegZeroed);
        instGen_Set_Reg_To_Zero(EA_PTRSIZE, rZero2);
        var stmImm = unchecked((nint)(genRegMask(rZero1) | genRegMask(rZero2)).Lower);

        if (!useLoop)
        {
            while (cntBytes >= REGSIZE_BYTES * 2)
            {
                Emitter.emitIns_R_I(INS_stm, EA_PTRSIZE, rAddr, stmImm);
                cntBytes -= REGSIZE_BYTES * 2;
            }
        }
        else
        {
            var loopHead = genCreateTempLabel();
            genDefineInlineTempLabel(loopHead);
            Emitter.emitIns_R_I(INS_stm, EA_PTRSIZE, rAddr, stmImm);
            Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, rCnt, 1, INS_FLAGS_SET);
            Emitter.emitIns_ShortJ(INS_bhi, loopHead);
            cntBytes %= REGSIZE_BYTES * 2;
        }

        if (cntBytes >= REGSIZE_BYTES)
        {
            Emitter.emitIns_R_R_I(INS_str, EA_PTRSIZE, rZero1, rAddr, 0, INS_FLAGS_DONT_CARE);
            cntBytes -= REGSIZE_BYTES;
        }

        noway_assert(cntBytes == 0);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog block initialization requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(UseBlockInit);
        assert(untrLclHi > untrLclLo);
        var frameReg = genFramePointerReg();
        var zeroReg = REG_NA;
        var blkSize = untrLclHi - untrLclLo;
        assert(blkSize >= 0);
        noway_assert((blkSize % sizeof(int)) == 0);
        assert((regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask) & _calleeRegArgMaskLiveIn).IsEmpty);

#if TARGET_AMD64
        var simdMov = simdAlignedMovIns();
        var alignedLclLo = (untrLclLo + XMM_REGSIZE_BYTES - 1) & -XMM_REGSIZE_BYTES;
        if ((untrLclLo != alignedLclLo) && (blkSize < 2 * XMM_REGSIZE_BYTES))
        {
            assert(alignedLclLo - untrLclLo < XMM_REGSIZE_BYTES);
            simdMov = simdUnalignedMovIns();
        }
#else
        var simdMov = simdUnalignedMovIns();
        var alignedLclLo = untrLclLo;
#endif

        if (blkSize < XMM_REGSIZE_BYTES)
        {
            zeroReg = genGetZeroReg(initReg, ref initRegZeroed);
            var i = 0;
            for (; i + REGSIZE_BYTES <= blkSize; i += REGSIZE_BYTES)
            {
                Emitter.emitIns_AR_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, zeroReg, frameReg, untrLclLo + i);
            }

#if TARGET_AMD64
            assert((i == blkSize) || (i + sizeof(int) == blkSize));
            if (i != blkSize)
            {
                Emitter.emitIns_AR_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, frameReg, untrLclLo + i);
                i += sizeof(int);
            }
#endif
            assert(i == blkSize);
        }
        else
        {
#if UNIX_AMD64_ABI
            var zeroSimdReg = REG_XMM8;
#else
            // Windows xarch's first non-argument, caller-saved SIMD register is XMM4.
            var zeroSimdReg = REG_XMM4;
#endif
            int alignedLclHi;
            int alignmentHiBlkSize;
#if TARGET_AMD64
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
#else
            var alignmentBlkSize = blkSize & -XMM_REGSIZE_BYTES;
            alignmentHiBlkSize = blkSize - alignmentBlkSize;
            alignedLclHi = untrLclLo + alignmentBlkSize;
            blkSize = alignmentBlkSize;
            assert(blkSize + alignmentHiBlkSize == untrLclHi - untrLclLo);
#endif

            var maxSimdSize = _compiler.roundDownSimdSize((uint)blkSize);
#if TARGET_AMD64
            assert((maxSimdSize >= XMM_REGSIZE_BYTES) && (maxSimdSize <= ZMM_REGSIZE_BYTES));
#else
            // ZMM_REGSIZE_BYTES is not declared for managed x86; four XMM widths retain its 64-byte bound.
            assert((maxSimdSize >= XMM_REGSIZE_BYTES) && (maxSimdSize <= 4 * XMM_REGSIZE_BYTES));
#endif
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

#if TARGET_AMD64
                assert((i == alignmentHiBlkSize) || (i + sizeof(int) == alignmentHiBlkSize));
                if (i != alignmentHiBlkSize)
                {
                    Emitter.emitIns_AR_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, frameReg, alignedLclHi + i);
                    i += sizeof(int);
                }
#endif
                assert(i == alignmentHiBlkSize);
            }
        }
#endif
    }
#endif
}
