// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForInitBlkUnroll(GenTreeBlk node)
    {
        assert(node.Oper is GT_STORE_BLK);

        var dstLclNum = BAD_VAR_NUM;
        var dstAddrBaseReg = REG_NA;
        var dstOffset = 0;
        var dstAddr = node.Addr;

        if (!dstAddr.IsContained)
        {
            dstAddrBaseReg = genConsumeReg(dstAddr);
        }
        else if (dstAddr.Oper.IsAddrMode)
        {
            var addrMode = dstAddr.AsAddrMode();
            assert(!addrMode.HasIndex);
            assert(addrMode.HasBaseAddress);

            if (addrMode.BaseAddress is not { } baseAddress)
            {
                unreached();
                return;
            }

            dstAddrBaseReg = genConsumeReg(baseAddress);
            dstOffset = addrMode.Offset;
        }
        else
        {
            assert(dstAddr.Oper is GT_LCL_ADDR);
            var dstLocal = dstAddr.AsLclVarCommon();
            dstLclNum = dstLocal.LclNum;
            dstOffset = dstLocal.LclOffs;
        }

        var src = node.Data;
        if (src.Oper is GT_INIT_VAL)
        {
            assert(src.IsContained);
            src = src.AsUnOp().Op1;
        }

        if (node.IsVolatile)
        {
            instGen_MemoryBarrier(BARRIER_FULL);
        }

        var emit = GetEmitter();
        var size = node.Layout.Size;

        assert(size <= int.MaxValue);
        assert(dstOffset < int.MaxValue - unchecked((int)size));

#if TARGET_ARM64
        var helper = new InitBlockUnrollHelper(dstOffset, size);
        regNumber srcReg;

        if (!src.IsContained)
        {
            srcReg = genConsumeReg(src);
        }
        else
        {
            assert(src.IsIntegralConst(0));
            srcReg = REG_ZR;
        }

        var dstReg = dstAddrBaseReg;
        var dstRegAddrAlignment = 0;

        if (dstLclNum != BAD_VAR_NUM)
        {
            var baseAddr = _compiler.lvaFrameAddress(dstLclNum, out var fpBased);
            dstReg = fpBased ? REG_FPBASE : REG_SPBASE;
            dstRegAddrAlignment = fpBased ? genSPtoFPdelta % 16 : 0;

            helper.SetDstOffset(baseAddr + dstOffset);
        }

        if (!helper.CanEncodeAllOffsets(REGSIZE_BYTES))
        {
            var dstOffsetAdjustment = helper.GetDstOffset() - dstRegAddrAlignment;
            dstRegAddrAlignment = 0;

            var tempReg = InternalRegisters.Extract(node, new regMaskTP(SRBM_ALLINT));
            genInstrWithConstant(INS_add, EA_PTRSIZE, tempReg, dstReg, dstOffsetAdjustment, tempReg);
            dstReg = tempReg;

            helper.SetDstOffset(helper.GetDstOffset() - dstOffsetAdjustment);
        }

        var shouldUse16ByteWideInstrs = false;
        var hasAvailableSimdReg = size > FP_REGSIZE_BYTES;
        var canUse16ByteWideInstrs =
            hasAvailableSimdReg && (dstRegAddrAlignment == 0) &&
            helper.CanEncodeAllOffsets(FP_REGSIZE_BYTES);

        if (canUse16ByteWideInstrs)
        {
            var instrCount16ByteWide = helper.InstructionCount(FP_REGSIZE_BYTES) + 1;
            shouldUse16ByteWideInstrs = instrCount16ByteWide < helper.InstructionCount(REGSIZE_BYTES);
        }

        if (shouldUse16ByteWideInstrs)
        {
            var simdReg = InternalRegisters.GetSingle(node, new regMaskTP(SRBM_ALLFLOAT));
            var initValue = (int)(src.AsIntCon().IconValue & 0xFF);
            emit.emitIns_R_I(INS_movi, EA_16BYTE, simdReg, initValue, INS_OPTS_16B);

            helper.Unroll(srcReg, simdReg, dstReg, emit);
        }
        else
        {
            helper.UnrollBaseInstrs(srcReg, dstReg, emit);
        }
#endif

#if TARGET_ARM
        var srcReg = genConsumeReg(src);

        for (uint regSize = REGSIZE_BYTES; size > 0; size -= regSize, dstOffset += (int)regSize)
        {
            while (regSize > size)
            {
                regSize /= 2;
            }

            instruction storeIns;
            emitAttr attr;

            switch (regSize)
            {
                case 1:
                {
                    storeIns = INS_strb;
                    attr = EA_4BYTE;
                    break;
                }

                case 2:
                {
                    storeIns = INS_strh;
                    attr = EA_4BYTE;
                    break;
                }

                case 4:
                {
                    storeIns = INS_str;
                    attr = (emitAttr)regSize;
                    break;
                }

                default:
                {
                    unreached();
                    return;
                }
            }

            if (dstLclNum != BAD_VAR_NUM)
            {
                emit.emitIns_S_R(storeIns, attr, srcReg, dstLclNum, dstOffset);
            }
            else
            {
#if TARGET_ARM64
                emit.emitIns_R_R_I(storeIns, attr, srcReg, dstAddrBaseReg, dstOffset, INS_OPTS_NONE);
#else
                emit.emitIns_R_R_I(storeIns, attr, srcReg, dstAddrBaseReg, dstOffset, INS_FLAGS_DONT_CARE);
#endif
            }
        }
#endif
    }

#if TARGET_ARM64
    private interface IBlockUnrollInstructionStream
    {
        void LoadPairRegs(int offset, int regSizeBytes);

        void StorePairRegs(int offset, int regSizeBytes);

        void LoadReg(int offset, int regSizeBytes);

        void StoreReg(int offset, int regSizeBytes);
    }

    private struct CountingStream : IBlockUnrollInstructionStream
    {
        private uint _instructionCount;

        public readonly uint InstructionCount => _instructionCount;

        public void LoadPairRegs(int offset, int regSizeBytes)
        {
            _instructionCount++;
        }

        public void StorePairRegs(int offset, int regSizeBytes)
        {
            _instructionCount++;
        }

        public void LoadReg(int offset, int regSizeBytes)
        {
            _instructionCount++;
        }

        public void StoreReg(int offset, int regSizeBytes)
        {
            _instructionCount++;
        }
    }

    private struct VerifyingStream : IBlockUnrollInstructionStream
    {
        private bool _canEncodeAllLoads;
        private bool _canEncodeAllStores;

        public VerifyingStream()
        {
            _canEncodeAllLoads = true;
            _canEncodeAllStores = true;
        }

        public readonly bool CanEncodeAllLoads => _canEncodeAllLoads;

        public readonly bool CanEncodeAllStores => _canEncodeAllStores;

        public void LoadPairRegs(int offset, int regSizeBytes)
        {
            _canEncodeAllLoads =
                _canEncodeAllLoads && canEncodeLoadOrStorePairOffsetArm64(offset, (emitAttr)regSizeBytes);
        }

        public void StorePairRegs(int offset, int regSizeBytes)
        {
            _canEncodeAllStores =
                _canEncodeAllStores && canEncodeLoadOrStorePairOffsetArm64(offset, (emitAttr)regSizeBytes);
        }

        public void LoadReg(int offset, int regSizeBytes)
        {
            _canEncodeAllLoads =
                _canEncodeAllLoads && Emitter.emitIns_valid_imm_for_ldst_offset(offset, (emitAttr)regSizeBytes);
        }

        public void StoreReg(int offset, int regSizeBytes)
        {
            _canEncodeAllStores =
                _canEncodeAllStores && Emitter.emitIns_valid_imm_for_ldst_offset(offset, (emitAttr)regSizeBytes);
        }
    }

    private readonly struct ProducingStreamBaseInstrs : IBlockUnrollInstructionStream
    {
        private readonly regNumber _intReg1;
        private readonly regNumber _intReg2;
        private readonly regNumber _addrReg;
        private readonly Emitter _emitter;

        public ProducingStreamBaseInstrs(regNumber intReg1, regNumber intReg2, regNumber addrReg, Emitter emitter)
        {
            _intReg1 = intReg1;
            _intReg2 = intReg2;
            _addrReg = addrReg;
            _emitter = emitter;
        }

        public void LoadPairRegs(int offset, int regSizeBytes)
        {
            assert(regSizeBytes == 8);
            _emitter.emitIns_R_R_R_I(INS_ldp, (emitAttr)regSizeBytes, _intReg1, _intReg2, _addrReg, offset);
        }

        public void StorePairRegs(int offset, int regSizeBytes)
        {
            assert(regSizeBytes == 8);
            _emitter.emitIns_R_R_R_I(INS_stp, (emitAttr)regSizeBytes, _intReg1, _intReg2, _addrReg, offset);
        }

        public void LoadReg(int offset, int regSizeBytes)
        {
            var ins = regSizeBytes switch
            {
                1 => INS_ldrb,
                2 => INS_ldrh,
                _ => INS_ldr,
            };

            _emitter.emitIns_R_R_I(ins, (emitAttr)regSizeBytes, _intReg1, _addrReg, offset, INS_OPTS_NONE);
        }

        public void StoreReg(int offset, int regSizeBytes)
        {
            var ins = regSizeBytes switch
            {
                1 => INS_strb,
                2 => INS_strh,
                _ => INS_str,
            };

            _emitter.emitIns_R_R_I(ins, (emitAttr)regSizeBytes, _intReg1, _addrReg, offset, INS_OPTS_NONE);
        }
    }

    private readonly struct ProducingStream : IBlockUnrollInstructionStream
    {
        private readonly regNumber _intReg1;
        private readonly regNumber _simdReg1;
        private readonly regNumber _simdReg2;
        private readonly regNumber _addrReg;
        private readonly Emitter _emitter;

        public ProducingStream(
            regNumber intReg1,
            regNumber simdReg1,
            regNumber simdReg2,
            regNumber addrReg,
            Emitter emitter)
        {
            _intReg1 = intReg1;
            _simdReg1 = simdReg1;
            _simdReg2 = simdReg2;
            _addrReg = addrReg;
            _emitter = emitter;
        }

        public void LoadPairRegs(int offset, int regSizeBytes)
        {
            assert((regSizeBytes == 8) || (regSizeBytes == 16));
            _emitter.emitIns_R_R_R_I(INS_ldp, (emitAttr)regSizeBytes, _simdReg1, _simdReg2, _addrReg, offset);
        }

        public void StorePairRegs(int offset, int regSizeBytes)
        {
            assert((regSizeBytes == 8) || (regSizeBytes == 16));
            _emitter.emitIns_R_R_R_I(INS_stp, (emitAttr)regSizeBytes, _simdReg1, _simdReg2, _addrReg, offset);
        }

        public void LoadReg(int offset, int regSizeBytes)
        {
            var ins = INS_ldr;
            var tempReg = _intReg1;

            if ((regSizeBytes == 16) || (_intReg1 == REG_NA))
            {
                tempReg = _simdReg1;
            }
            else if (regSizeBytes == 1)
            {
                ins = INS_ldrb;
            }
            else if (regSizeBytes == 2)
            {
                ins = INS_ldrh;
            }

            _emitter.emitIns_R_R_I(ins, (emitAttr)regSizeBytes, tempReg, _addrReg, offset, INS_OPTS_NONE);
        }

        public void StoreReg(int offset, int regSizeBytes)
        {
            var ins = INS_str;
            var tempReg = _intReg1;

            if ((regSizeBytes == 16) || (_intReg1 == REG_NA))
            {
                tempReg = _simdReg1;
            }
            else if (regSizeBytes == 1)
            {
                ins = INS_strb;
            }
            else if (regSizeBytes == 2)
            {
                ins = INS_strh;
            }

            _emitter.emitIns_R_R_I(ins, (emitAttr)regSizeBytes, tempReg, _addrReg, offset, INS_OPTS_NONE);
        }
    }

    private static class BlockUnrollHelper
    {
        // Round up to a legal scalar/SIMD access width without changing how many bytes are actually written.
        public static int GetRegSizeAtLeastBytes(int byteCount)
        {
            assert(byteCount != 0);
            assert(byteCount < 16);

            var regSizeBytes = byteCount;

            if (byteCount > 8)
            {
                regSizeBytes = 16;
            }
            else if (byteCount > 4)
            {
                regSizeBytes = 8;
            }
            else if (byteCount > 2)
            {
                regSizeBytes = 4;
            }

            return regSizeBytes;
        }
    }

    private struct InitBlockUnrollHelper
    {
        private int _dstStartOffset;
        private readonly uint _byteCount;

        public InitBlockUnrollHelper(int dstOffset, uint byteCount)
        {
            _dstStartOffset = dstOffset;
            _byteCount = byteCount;
        }

        public readonly int GetDstOffset() => _dstStartOffset;

        public void SetDstOffset(int dstOffset) => _dstStartOffset = dstOffset;

        public readonly bool CanEncodeAllOffsets(int regSizeBytes)
        {
            var stream = new VerifyingStream();
            UnrollInitBlock(ref stream, regSizeBytes);
            return stream.CanEncodeAllStores;
        }

        public readonly uint InstructionCount(int regSizeBytes)
        {
            var stream = new CountingStream();
            UnrollInitBlock(ref stream, regSizeBytes);
            return stream.InstructionCount;
        }

        public readonly void Unroll(regNumber intReg, regNumber simdReg, regNumber addrReg, Emitter emitter)
        {
            var stream = new ProducingStream(intReg, simdReg, simdReg, addrReg, emitter);
            UnrollInitBlock(ref stream, FP_REGSIZE_BYTES);
        }

        public readonly void UnrollBaseInstrs(regNumber intReg, regNumber addrReg, Emitter emitter)
        {
            var stream = new ProducingStreamBaseInstrs(intReg, intReg, addrReg, emitter);
            UnrollInitBlock(ref stream, REGSIZE_BYTES);
        }

        private readonly void UnrollInitBlock<TStream>(ref TStream stream, int initialRegSizeBytes)
            where TStream : struct, IBlockUnrollInstructionStream
        {
            assert((initialRegSizeBytes == 8) || (initialRegSizeBytes == 16));

            var offset = _dstStartOffset;
            var endOffset = unchecked(offset + (int)_byteCount);
            var storePairRegsAlignment = initialRegSizeBytes;
            var storePairRegsWritesBytes = 2 * initialRegSizeBytes;

            // Native AlignUp operates on UINT; preserve its wrap for negative frame-relative offsets.
            var offsetAligned = unchecked((int)(((uint)offset + (uint)storePairRegsAlignment - 1) &
                ~((uint)storePairRegsAlignment - 1)));
            var storePairRegsInstrCount = (endOffset - offsetAligned) / storePairRegsWritesBytes;

            if (storePairRegsInstrCount > 0)
            {
                if (offset != offsetAligned)
                {
                    var firstRegSizeBytes = BlockUnrollHelper.GetRegSizeAtLeastBytes(offsetAligned - offset);
                    stream.StoreReg(offset, firstRegSizeBytes);
                    offset = offsetAligned;
                }

                while (endOffset - offset >= storePairRegsWritesBytes)
                {
                    stream.StorePairRegs(offset, initialRegSizeBytes);
                    offset += storePairRegsWritesBytes;
                }

                if (endOffset - offset >= initialRegSizeBytes)
                {
                    stream.StoreReg(offset, initialRegSizeBytes);
                    offset += initialRegSizeBytes;
                }

                if (offset != endOffset)
                {
                    var lastRegSizeBytes = BlockUnrollHelper.GetRegSizeAtLeastBytes(endOffset - offset);
                    stream.StoreReg(endOffset - lastRegSizeBytes, lastRegSizeBytes);
                }
            }
            else
            {
                var isSafeToWriteBehind = false;

                while (endOffset - offset >= initialRegSizeBytes)
                {
                    stream.StoreReg(offset, initialRegSizeBytes);
                    offset += initialRegSizeBytes;
                    isSafeToWriteBehind = true;
                }

                assert(endOffset - offset < initialRegSizeBytes);

                while (offset != endOffset)
                {
                    if (isSafeToWriteBehind)
                    {
                        assert(endOffset - offset < initialRegSizeBytes);
                        var lastRegSizeBytes = BlockUnrollHelper.GetRegSizeAtLeastBytes(endOffset - offset);
                        stream.StoreReg(endOffset - lastRegSizeBytes, lastRegSizeBytes);
                        break;
                    }

                    if (offset + initialRegSizeBytes > endOffset)
                    {
                        initialRegSizeBytes /= 2;
                    }
                    else
                    {
                        stream.StoreReg(offset, initialRegSizeBytes);
                        offset += initialRegSizeBytes;
                        isSafeToWriteBehind = true;
                    }
                }
            }
        }
    }
#endif
}
#endif
