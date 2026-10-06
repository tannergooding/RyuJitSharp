// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCpBlkUnroll(GenTreeBlk node)
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

        var srcLclNum = BAD_VAR_NUM;
        var srcAddrBaseReg = REG_NA;
        var srcOffset = 0;
        var src = node.Data;

        assert(src.IsContained);

        if (src.Oper is GT_LCL_VAR or GT_LCL_FLD)
        {
            var srcLocal = src.AsLclVarCommon();
            srcLclNum = srcLocal.LclNum;
            srcOffset = srcLocal.LclOffs;
        }
        else
        {
            assert(src.Oper is GT_IND);
            var srcAddr = src.AsIndir().Addr;

            if (!srcAddr.IsContained)
            {
                srcAddrBaseReg = genConsumeReg(srcAddr);
            }
            else if (srcAddr.Oper.IsAddrMode)
            {
                var addrMode = srcAddr.AsAddrMode();
                assert(!addrMode.HasIndex);

                if (addrMode.BaseAddress is not { } baseAddress)
                {
                    unreached();
                    return;
                }

                srcAddrBaseReg = genConsumeReg(baseAddress);
                srcOffset = addrMode.Offset;
            }
            else
            {
                assert(srcAddr.Oper is GT_LCL_ADDR);
                var srcLocal = srcAddr.AsLclVarCommon();
                srcLclNum = srcLocal.LclNum;
                srcOffset = srcLocal.LclOffs;
            }
        }

        if (node.IsVolatile)
        {
            instGen_MemoryBarrier(BARRIER_STORE_ONLY);
        }

        var emit = GetEmitter();
        var size = node.Layout.Size;

        assert(size <= int.MaxValue);
        assert(srcOffset < int.MaxValue - unchecked((int)size));
        assert(dstOffset < int.MaxValue - unchecked((int)size));

#if TARGET_ARM64
        var helper = new CopyBlockUnrollHelper(srcOffset, dstOffset, size);
        var srcReg = srcAddrBaseReg;

        if (srcLclNum != BAD_VAR_NUM)
        {
            var baseAddr = _compiler.lvaFrameAddress(srcLclNum, out var fpBased);
            srcReg = fpBased ? REG_FPBASE : REG_SPBASE;
            helper.SetSrcOffset(baseAddr + srcOffset);
        }

        var dstReg = dstAddrBaseReg;

        if (dstLclNum != BAD_VAR_NUM)
        {
            var baseAddr = _compiler.lvaFrameAddress(dstLclNum, out var fpBased);
            dstReg = fpBased ? REG_FPBASE : REG_SPBASE;
            helper.SetDstOffset(baseAddr + dstOffset);
        }

        helper.TryEncodeAllOffsets(REGSIZE_BYTES, out var canEncodeAllLoads, out var canEncodeAllStores);

        srcOffset = helper.GetSrcOffset();
        dstOffset = helper.GetDstOffset();

        var srcOffsetAdjustment = 0;
        var dstOffsetAdjustment = 0;

        if (!canEncodeAllLoads && !canEncodeAllStores)
        {
            srcOffsetAdjustment = srcOffset;
            dstOffsetAdjustment = dstOffset;
        }
        else if (!canEncodeAllLoads)
        {
            srcOffsetAdjustment = srcOffset - dstOffset;
        }
        else if (!canEncodeAllStores)
        {
            dstOffsetAdjustment = dstOffset - srcOffset;
        }

        helper.SetSrcOffset(srcOffset - srcOffsetAdjustment);
        helper.SetDstOffset(dstOffset - dstOffsetAdjustment);

        // Wider accesses can reduce alignment penalties, but address materialization must still save instructions.
        var canUse16ByteWideInstrs = size >= (uint)(2 * FP_REGSIZE_BYTES);
        var shouldUse16ByteWideInstrs = false;

        if (canUse16ByteWideInstrs)
        {
            helper.TryEncodeAllOffsets(
                FP_REGSIZE_BYTES,
                out var canEncodeAll16ByteWideLoads,
                out var canEncodeAll16ByteWideStores);

            if (canEncodeAll16ByteWideLoads && canEncodeAll16ByteWideStores)
            {
                shouldUse16ByteWideInstrs =
                    helper.InstructionCount(FP_REGSIZE_BYTES) < helper.InstructionCount(REGSIZE_BYTES);
            }
            else if (canEncodeAllLoads && canEncodeAllStores &&
                (canEncodeAll16ByteWideLoads || canEncodeAll16ByteWideStores))
            {
                if (helper.InstructionCount(FP_REGSIZE_BYTES) + 1 < helper.InstructionCount(REGSIZE_BYTES))
                {
                    shouldUse16ByteWideInstrs = true;

                    if (!canEncodeAll16ByteWideLoads)
                    {
                        srcOffsetAdjustment = srcOffset - dstOffset;
                    }
                    else
                    {
                        dstOffsetAdjustment = dstOffset - srcOffset;
                    }

                    helper.SetSrcOffset(srcOffset - srcOffsetAdjustment);
                    helper.SetDstOffset(dstOffset - dstOffsetAdjustment);
                }
            }
        }

#if DEBUG
        assert(helper.CanEncodeAllOffsets(
            shouldUse16ByteWideInstrs ? FP_REGSIZE_BYTES : REGSIZE_BYTES));
#endif

        if (!node._gcUnsafe && ((srcOffsetAdjustment != 0) || (dstOffsetAdjustment != 0)))
        {
            // Materialized bases can hold GC references that are not otherwise reported to the runtime.
            node._gcUnsafe = true;
            GetEmitter().emitDisableGC();
        }

        var allIntRegisters = new regMaskTP(SRBM_ALLINT);

        if ((srcOffsetAdjustment != 0) && (dstOffsetAdjustment != 0))
        {
            var tempReg1 = InternalRegisters.Extract(node, allIntRegisters);
            genInstrWithConstant(INS_add, EA_PTRSIZE, tempReg1, srcReg, srcOffsetAdjustment, tempReg1);
            srcReg = tempReg1;

            var tempReg2 = InternalRegisters.Extract(node, allIntRegisters);
            genInstrWithConstant(INS_add, EA_PTRSIZE, tempReg2, dstReg, dstOffsetAdjustment, tempReg2);
            dstReg = tempReg2;
        }
        else if (srcOffsetAdjustment != 0)
        {
            var tempReg = InternalRegisters.Extract(node, allIntRegisters);
            genInstrWithConstant(INS_add, EA_PTRSIZE, tempReg, srcReg, srcOffsetAdjustment, tempReg);
            srcReg = tempReg;
        }
        else if (dstOffsetAdjustment != 0)
        {
            var tempReg = InternalRegisters.Extract(node, allIntRegisters);
            genInstrWithConstant(INS_add, EA_PTRSIZE, tempReg, dstReg, dstOffsetAdjustment, tempReg);
            dstReg = tempReg;
        }

        var intRegCount = InternalRegisters.Count(node, allIntRegisters);
        var intReg1 = REG_NA;
        var intReg2 = REG_NA;

        if (intRegCount >= 2)
        {
            intReg1 = InternalRegisters.Extract(node, allIntRegisters);
            intReg2 = InternalRegisters.Extract(node, allIntRegisters);
        }
        else if (intRegCount == 1)
        {
            intReg1 = InternalRegisters.GetSingle(node, allIntRegisters);
            intReg2 = rsGetRsvdReg();
        }
        else
        {
            intReg1 = rsGetRsvdReg();
        }

        if (shouldUse16ByteWideInstrs)
        {
            var allFloatRegisters = new regMaskTP(SRBM_ALLFLOAT);
            var simdReg1 = InternalRegisters.Extract(node, allFloatRegisters);
            var simdReg2 = InternalRegisters.GetSingle(node, allFloatRegisters);

            helper.Unroll(
                FP_REGSIZE_BYTES,
                intReg1,
                simdReg1,
                simdReg2,
                srcReg,
                dstReg,
                emit);
        }
        else
        {
            helper.UnrollBaseInstrs(intReg1, intReg2, srcReg, dstReg, emit);
        }
#endif

#if TARGET_ARM
        var tempReg = InternalRegisters.Extract(node, new regMaskTP(SRBM_ALLINT));

        for (uint regSize = REGSIZE_BYTES; size > 0; size -= regSize, srcOffset += (int)regSize, dstOffset += (int)regSize)
        {
            while (regSize > size)
            {
                regSize /= 2;
            }

            instruction loadIns;
            instruction storeIns;
            emitAttr attr;

            switch (regSize)
            {
                case 1:
                {
                    loadIns = INS_ldrb;
                    storeIns = INS_strb;
                    attr = EA_4BYTE;
                    break;
                }

                case 2:
                {
                    loadIns = INS_ldrh;
                    storeIns = INS_strh;
                    attr = EA_4BYTE;
                    break;
                }

                case 4:
                {
                    loadIns = INS_ldr;
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

            if (srcLclNum != BAD_VAR_NUM)
            {
                emit.emitIns_R_S(loadIns, attr, tempReg, srcLclNum, srcOffset);
            }
            else
            {
                emit.emitIns_R_R_I(loadIns, attr, tempReg, srcAddrBaseReg, srcOffset, INS_FLAGS_DONT_CARE);
            }

            if (dstLclNum != BAD_VAR_NUM)
            {
                emit.emitIns_S_R(storeIns, attr, tempReg, dstLclNum, dstOffset);
            }
            else
            {
                emit.emitIns_R_R_I(storeIns, attr, tempReg, dstAddrBaseReg, dstOffset, INS_FLAGS_DONT_CARE);
            }
        }
#endif

        if (node.IsVolatile)
        {
            instGen_MemoryBarrier(BARRIER_LOAD_ONLY);
        }
    }

#if TARGET_ARM64
    private struct CopyBlockUnrollHelper
    {
        private int _srcStartOffset;
        private int _dstStartOffset;
        private readonly uint _byteCount;

        public CopyBlockUnrollHelper(int srcOffset, int dstOffset, uint byteCount)
        {
            _srcStartOffset = srcOffset;
            _dstStartOffset = dstOffset;
            _byteCount = byteCount;
        }

        public readonly int GetSrcOffset() => _srcStartOffset;

        public readonly int GetDstOffset() => _dstStartOffset;

        public void SetSrcOffset(int srcOffset) => _srcStartOffset = srcOffset;

        public void SetDstOffset(int dstOffset) => _dstStartOffset = dstOffset;

        public readonly uint InstructionCount(int regSizeBytes)
        {
            var loadStream = new CountingStream();
            var storeStream = new CountingStream();
            UnrollCopyBlock(ref loadStream, ref storeStream, regSizeBytes);
            return unchecked(loadStream.InstructionCount + storeStream.InstructionCount);
        }

        public readonly bool CanEncodeAllOffsets(int regSizeBytes)
        {
            TryEncodeAllOffsets(regSizeBytes, out var canEncodeAllLoads, out var canEncodeAllStores);
            return canEncodeAllLoads && canEncodeAllStores;
        }

        public readonly void TryEncodeAllOffsets(int regSizeBytes, out bool canEncodeAllLoads, out bool canEncodeAllStores)
        {
            var loadStream = new VerifyingStream();
            var storeStream = new VerifyingStream();
            UnrollCopyBlock(ref loadStream, ref storeStream, regSizeBytes);
            canEncodeAllLoads = loadStream.CanEncodeAllLoads;
            canEncodeAllStores = storeStream.CanEncodeAllStores;
        }

        public readonly void Unroll(
            int initialRegSizeBytes,
            regNumber intReg,
            regNumber simdReg1,
            regNumber simdReg2,
            regNumber srcAddrReg,
            regNumber dstAddrReg,
            Emitter emitter)
        {
            var loadStream = new ProducingStream(intReg, simdReg1, simdReg2, srcAddrReg, emitter);
            var storeStream = new ProducingStream(intReg, simdReg1, simdReg2, dstAddrReg, emitter);
            UnrollCopyBlock(ref loadStream, ref storeStream, initialRegSizeBytes);
        }

        public readonly void UnrollBaseInstrs(
            regNumber intReg1,
            regNumber intReg2,
            regNumber srcAddrReg,
            regNumber dstAddrReg,
            Emitter emitter)
        {
            var loadStream = new ProducingStreamBaseInstrs(intReg1, intReg2, srcAddrReg, emitter);
            var storeStream = new ProducingStreamBaseInstrs(intReg1, intReg2, dstAddrReg, emitter);
            UnrollCopyBlock(ref loadStream, ref storeStream, REGSIZE_BYTES);
        }

        private readonly void UnrollCopyBlock<TStream>(
            ref TStream loadStream,
            ref TStream storeStream,
            int initialRegSizeBytes)
            where TStream : struct, IBlockUnrollInstructionStream
        {
            assert((initialRegSizeBytes == 8) || (initialRegSizeBytes == 16));

            var srcOffset = _srcStartOffset;
            var dstOffset = _dstStartOffset;
            var endSrcOffset = unchecked(srcOffset + (int)_byteCount);
            var endDstOffset = unchecked(dstOffset + (int)_byteCount);

            var storePairRegsAlignment = initialRegSizeBytes;
            var storePairRegsWritesBytes = 2 * initialRegSizeBytes;

            // Use the destination's alignment for paired accesses and advance the source by the same byte count.
            var dstOffsetAligned = unchecked((int)(((uint)dstOffset + (uint)storePairRegsAlignment - 1) &
                ~((uint)storePairRegsAlignment - 1)));

            if (_byteCount >= (uint)storePairRegsWritesBytes)
            {
                var dstBytesToAlign = dstOffsetAligned - dstOffset;

                if (dstBytesToAlign != 0)
                {
                    var firstRegSizeBytes = BlockUnrollHelper.GetRegSizeAtLeastBytes(dstBytesToAlign);
                    loadStream.LoadReg(srcOffset, firstRegSizeBytes);
                    storeStream.StoreReg(dstOffset, firstRegSizeBytes);

                    srcOffset = unchecked(srcOffset + dstBytesToAlign);
                    dstOffset = dstOffsetAligned;
                }

                while (endDstOffset - dstOffset >= storePairRegsWritesBytes)
                {
                    loadStream.LoadPairRegs(srcOffset, initialRegSizeBytes);
                    storeStream.StorePairRegs(dstOffset, initialRegSizeBytes);

                    srcOffset = unchecked(srcOffset + storePairRegsWritesBytes);
                    dstOffset = unchecked(dstOffset + storePairRegsWritesBytes);
                }

                if (endDstOffset - dstOffset >= initialRegSizeBytes)
                {
                    loadStream.LoadReg(srcOffset, initialRegSizeBytes);
                    storeStream.StoreReg(dstOffset, initialRegSizeBytes);

                    dstOffset = unchecked(dstOffset + initialRegSizeBytes);
                }

                if (dstOffset != endDstOffset)
                {
                    var lastRegSizeBytes = BlockUnrollHelper.GetRegSizeAtLeastBytes(endDstOffset - dstOffset);
                    loadStream.LoadReg(endSrcOffset - lastRegSizeBytes, lastRegSizeBytes);
                    storeStream.StoreReg(endDstOffset - lastRegSizeBytes, lastRegSizeBytes);
                }
            }
            else
            {
                var isSafeToWriteBehind = false;

                while (endDstOffset - dstOffset >= initialRegSizeBytes)
                {
                    loadStream.LoadReg(srcOffset, initialRegSizeBytes);
                    storeStream.StoreReg(dstOffset, initialRegSizeBytes);

                    srcOffset = unchecked(srcOffset + initialRegSizeBytes);
                    dstOffset = unchecked(dstOffset + initialRegSizeBytes);
                    isSafeToWriteBehind = true;
                }

                assert(endSrcOffset - srcOffset < initialRegSizeBytes);

                while (dstOffset != endDstOffset)
                {
                    if (isSafeToWriteBehind)
                    {
                        var lastRegSizeBytes = BlockUnrollHelper.GetRegSizeAtLeastBytes(endDstOffset - dstOffset);
                        loadStream.LoadReg(endSrcOffset - lastRegSizeBytes, lastRegSizeBytes);
                        storeStream.StoreReg(endDstOffset - lastRegSizeBytes, lastRegSizeBytes);
                        break;
                    }

                    if (dstOffset + initialRegSizeBytes > endDstOffset)
                    {
                        initialRegSizeBytes /= 2;
                    }
                    else
                    {
                        loadStream.LoadReg(srcOffset, initialRegSizeBytes);
                        storeStream.StoreReg(dstOffset, initialRegSizeBytes);

                        srcOffset = unchecked(srcOffset + initialRegSizeBytes);
                        dstOffset = unchecked(dstOffset + initialRegSizeBytes);
                        isSafeToWriteBehind = true;
                    }
                }
            }
        }
    }
#endif
}
#endif
