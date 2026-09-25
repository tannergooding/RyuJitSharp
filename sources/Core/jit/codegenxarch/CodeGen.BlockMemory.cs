// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreBlk(GenTreeBlk node)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Block memory generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(node.Oper == GT_STORE_BLK);
        var isCopy = node.IsCopyBlkOp;

        switch (node._kind)
        {
            case GenTreeBlk.BlkOpKindLoop:
            {
                assert(!isCopy);
                genCodeForInitBlkLoop(node);
                break;
            }

            case GenTreeBlk.BlkOpKindUnroll:
            case GenTreeBlk.BlkOpKindUnrollMemmove:
            {
                if (isCopy)
                {
                    if (node._gcUnsafe)
                    {
                        Emitter.emitDisableGC();
                    }

                    if (node._kind == GenTreeBlk.BlkOpKindUnroll)
                    {
                        genCodeForCpBlkUnroll(node);
                    }
                    else
                    {
                        assert(node._kind == GenTreeBlk.BlkOpKindUnrollMemmove);
                        genCodeForMemmove(node);
                    }

                    if (node._gcUnsafe)
                    {
                        Emitter.emitEnableGC();
                    }
                }
                else
                {
                    assert(!node._gcUnsafe);
                    genCodeForInitBlkUnroll(node);
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif
    }

    public void genCodeForMemmove(GenTreeBlk node)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Unrolled memmove requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var srcIndir = node.Data.AsIndir();
        assert(srcIndir.IsContained && !srcIndir.Addr.IsContained);
        var dst = genConsumeReg(node.Addr);
        var src = genConsumeReg(srcIndir.Addr);
        var size = node.Size;
        var simdSize = (uint)_compiler.roundDownSimdSize(size);
        var floatMask = new regMaskTP(SRBM_ALLFLOAT);
        var intMask = new regMaskTP(SRBM_ALLINT);

        if ((size >= simdSize) && (simdSize > 0))
        {
            var count = InternalRegisters.Count(node, floatMask);
            assert(count * simdSize >= size);
            var tempRegs = new regNumber[count];
            for (var index = 0; index < count; index++)
            {
                tempRegs[index] = InternalRegisters.Extract(node, floatMask);
            }

            void EmitSimdLoadStore(bool load)
            {
                var offset = 0u;
                var regIndex = 0;
                var ins = simdUnalignedMovIns();
                var currentSize = simdSize;

                do
                {
                    assert(currentSize >= XMM_REGSIZE_BYTES);
                    if (load)
                    {
                        Emitter.emitIns_R_AR(ins, (emitAttr)currentSize, tempRegs[regIndex++], src, unchecked((int)offset));
                    }
                    else
                    {
                        Emitter.emitIns_AR_R(ins, (emitAttr)currentSize, tempRegs[regIndex++], dst, (nint)offset);
                    }
                    offset = unchecked(offset + currentSize);
                    if (size == offset)
                    {
                        break;
                    }

                    assert(size > offset);
                    var remainder = size - offset;
                    if (remainder < currentSize)
                    {
                        currentSize = (uint)_compiler.roundUpSimdSize((int)remainder);
                        offset = size - currentSize;
                    }
                }
                while (true);
            }

            // All source bytes must be captured before any destination write.
            EmitSimdLoadStore(true);
            EmitSimdLoadStore(false);
        }
        else
        {
            assert((size > 0) && (size < XMM_REGSIZE_BYTES));

            void EmitScalarLoadStore(bool load, uint accessSize, regNumber reg, uint offset)
            {
                var memType = accessSize switch
                {
                    1 => TYP_UBYTE,
                    2 => TYP_USHORT,
                    4 => TYP_INT,
                    8 => TYP_LONG,
                    _ => throw new FatalJitException("Invalid scalar memmove width."),
                };

                if (load)
                {
                    Emitter.emitIns_R_AR(ins_Load(memType), memType.EmitSize, reg, src, unchecked((int)offset));
                }
                else
                {
                    Emitter.emitIns_AR_R(ins_Store(memType), memType.EmitSize, reg, dst, (nint)offset);
                }
            }

            var width = 1u << BitOperations.Log2(size);
            if (width == size)
            {
                var reg = InternalRegisters.GetSingle(node, intMask);
                EmitScalarLoadStore(true, width, reg, 0);
                EmitScalarLoadStore(false, width, reg, 0);
            }
            else
            {
                assert(InternalRegisters.Count(node) == 2);
                var reg1 = InternalRegisters.Extract(node, intMask);
                var reg2 = InternalRegisters.Extract(node, intMask);
                EmitScalarLoadStore(true, width, reg1, 0);
                EmitScalarLoadStore(true, width, reg2, size - width);
                EmitScalarLoadStore(false, width, reg1, 0);
                EmitScalarLoadStore(false, width, reg2, size - width);
            }
        }
#endif
    }

    public void genCodeForInitBlkLoop(GenTreeBlk node)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Loop block initialization requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var dstNode = node.Addr;
        var zeroNode = node.Data;
        _ = genConsumeReg(dstNode);
        _ = genConsumeReg(zeroNode);
        var dstReg = dstNode.RegNum;
        var zeroReg = zeroNode.RegNum;
        var size = node.Layout.Size;
        assert((size >= TARGET_POINTER_SIZE) && ((size % TARGET_POINTER_SIZE) == 0));

        // Touch the first pointer before the reverse loop to preserve nullcheck
        // behavior even when the final offset lies outside the null page.
        Emitter.emitIns_AR_R(INS_mov, EA_PTRSIZE, zeroReg, dstReg, 0);
        if (size > TARGET_POINTER_SIZE)
        {
            GCInfo.gcMarkRegPtrVal(dstReg, dstNode.Type);
            var offsetReg = InternalRegisters.GetSingle(node);
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, offsetReg, (nint)(size - TARGET_POINTER_SIZE));
            var loop = genCreateTempLabel();
            genDefineTempLabel(loop);

            Emitter.emitIns_ARX_R(INS_mov, EA_PTRSIZE, zeroReg, dstReg, offsetReg, 1, 0);
            Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, offsetReg, TARGET_POINTER_SIZE);
            inst_JMP(EJ_jne, loop);
            GCInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(dstReg, dstReg.SingleTypeMask));
        }
#endif
    }

    public void genCodeForInitBlkUnroll(GenTreeBlk node)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Unrolled block initialization requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(node.Oper == GT_STORE_BLK);
        var dst = genConsumeBlockAddress(node.Addr);
        var srcIntReg = REG_NA;
        var src = node.Data;
        if (src.Oper == GT_INIT_VAL)
        {
            assert(src.IsContained);
            src = src.AsUnOp().Op1;
        }

        var size = node.Layout.Size;
        var willUseSimdMov = !node.IsOnHeapAndContainsReferences && (size >= XMM_REGSIZE_BYTES);
        if (!src.IsContained)
        {
            srcIntReg = genConsumeReg(src);
        }
        else
        {
            assert(willUseSimdMov);
            assert(size >= XMM_REGSIZE_BYTES);
        }
        assert(size <= int.MaxValue);
        assert(dst.Offset < int.MaxValue - (int)size);

        void EmitStore(instruction ins, uint width, regNumber reg)
        {
            if (dst.Local != BAD_VAR_NUM)
            {
                Emitter.emitIns_S_R(ins, (emitAttr)width, reg, dst.Local, dst.Offset);
            }
            else
            {
                Emitter.emitIns_ARX_R(ins, (emitAttr)width, reg, dst.Base, dst.Index, dst.Scale, dst.Offset);
            }
        }

#if FEATURE_SIMD
        var floatMask = new regMaskTP(SRBM_ALLFLOAT);
        if (willUseSimdMov)
        {
            var srcXmmReg = InternalRegisters.GetSingle(node, floatMask);
            var regSize = (uint)_compiler.roundDownSimdSize(size);
            var loadType = Compiler.GetSimdTypeForSize((int)regSize);
            simd_t value = default;
            ((Span<byte>)value.u8).Fill(unchecked((byte)src.AsIntCon().IconValue));
            genSetRegToConst(srcXmmReg, loadType, in value);
            var ins = simdUnalignedMovIns();
            var bytesWritten = 0u;

            while (bytesWritten < size)
            {
                if (unchecked(bytesWritten + regSize) > size)
                {
                    break;
                }
                EmitStore(ins, regSize, srcXmmReg);
                dst.Offset = unchecked(dst.Offset + (int)regSize);
                bytesWritten = unchecked(bytesWritten + regSize);
            }
            size -= bytesWritten;
            if ((size > 0) && (size < regSize) && (regSize >= XMM_REGSIZE_BYTES))
            {
                regSize = (uint)_compiler.roundUpSimdSize((int)size);
                dst.Offset = unchecked(dst.Offset - (int)(regSize - size));
                EmitStore(ins, regSize, srcXmmReg);
                size = 0;
            }
        }
        else if (node.IsOnHeapAndContainsReferences && (InternalRegisters.GetAll(node) & floatMask).IsNonEmpty)
        {
            assert(!willUseSimdMov);
            var layout = node.Layout;
            var simdZeroReg = REG_NA;
            var slots = layout.SlotCount;
            var slot = 0;

            while (slot < slots)
            {
                if (!layout.IsGCPtr(slot))
                {
                    var nonGcSlotCount = 0;
                    do
                    {
                        nonGcSlotCount++;
                        slot++;
                    }
                    while ((slot < slots) && !layout.IsGCPtr(slot));

                    for (var nonGcSlot = 0; nonGcSlot < nonGcSlotCount; nonGcSlot++)
                    {
                        var simdSize = (uint)_compiler.roundDownSimdSize((uint)((nonGcSlotCount - nonGcSlot) * REGSIZE_BYTES));
                        if (simdSize > 0)
                        {
                            if (simdZeroReg == REG_NA)
                            {
                                simdZeroReg = InternalRegisters.GetSingle(node, floatMask);
                                simd_t zero = default;
                                genSetRegToConst(simdZeroReg, TYP_SIMD16, in zero);
                            }
                            EmitStore(simdUnalignedMovIns(), simdSize, simdZeroReg);
                            dst.Offset = unchecked(dst.Offset + (int)simdSize);
                            nonGcSlot += ((int)simdSize / REGSIZE_BYTES) - 1;
                        }
                        else
                        {
                            EmitStore(INS_mov, REGSIZE_BYTES, srcIntReg);
                            dst.Offset = unchecked(dst.Offset + REGSIZE_BYTES);
                        }
                    }
                }
                else
                {
                    // GC slots must remain pointer-sized atomic stores.
                    EmitStore(INS_mov, REGSIZE_BYTES, srcIntReg);
                    dst.Offset = unchecked(dst.Offset + REGSIZE_BYTES);
                    slot++;
                }
            }
            assert((layout.Size % TARGET_POINTER_SIZE) == 0);
            size = 0;
        }
#endif
        assert((srcIntReg != REG_NA) || (size == 0));
        var scalarSize = (uint)REGSIZE_BYTES;
        while (scalarSize > size)
        {
            scalarSize /= 2;
        }
        for (; size > scalarSize; size -= scalarSize, dst.Offset = unchecked(dst.Offset + (int)scalarSize))
        {
            EmitStore(INS_mov, scalarSize, srcIntReg);
        }
        if (size > 0)
        {
            assert(size <= REGSIZE_BYTES);
            scalarSize = uint.Min(scalarSize, (uint)Compiler.roundUpGprSize((int)size));
            var shiftBack = scalarSize - size;
            assert(shiftBack <= scalarSize);
            dst.Offset = unchecked(dst.Offset - (int)shiftBack);
            EmitStore(INS_mov, scalarSize, srcIntReg);
        }
#endif
    }

    public void genCodeForCpBlkUnroll(GenTreeBlk node)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Unrolled block copy requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(node.Oper == GT_STORE_BLK);
        var dst = genConsumeBlockAddress(node.Addr);
        var data = node.Data;
        assert(data.IsContained);
        if (data.Oper is not GT_LCL_VAR and not GT_LCL_FLD)
        {
            assert(data.Oper == GT_IND);
        }
        var src = data.Oper is GT_LCL_VAR or GT_LCL_FLD
            ? (Local: data.AsLclVarCommon().LclNum, Base: REG_NA, Index: REG_NA, Scale: 1u,
                Offset: (int)data.AsLclVarCommon().LclOffs)
            : genConsumeBlockAddress(data.AsIndir().Addr);
        var size = node.Layout.Size;
        assert(size <= int.MaxValue);
        assert(src.Offset < int.MaxValue - (int)size);
        assert(dst.Offset < int.MaxValue - (int)size);

        void EmitMoves(instruction ins, uint width, regNumber reg)
        {
            if (src.Local != BAD_VAR_NUM)
            {
                Emitter.emitIns_R_S(ins, (emitAttr)width, reg, src.Local, src.Offset);
            }
            else
            {
                Emitter.emitIns_R_ARX(ins, (emitAttr)width, reg, src.Base, src.Index, src.Scale, src.Offset);
            }
            if (dst.Local != BAD_VAR_NUM)
            {
                Emitter.emitIns_S_R(ins, (emitAttr)width, reg, dst.Local, dst.Offset);
            }
            else
            {
                Emitter.emitIns_ARX_R(ins, (emitAttr)width, reg, dst.Base, dst.Index, dst.Scale, dst.Offset);
            }
        }

        var regSize = (uint)_compiler.roundDownSimdSize(size);
        if ((size >= regSize) && (regSize > 0))
        {
            var tempReg = InternalRegisters.GetSingle(node, new regMaskTP(SRBM_ALLFLOAT));
            var ins = simdUnalignedMovIns();
            while (size >= regSize)
            {
                EmitMoves(ins, regSize, tempReg);
                src.Offset = unchecked(src.Offset + (int)regSize);
                dst.Offset = unchecked(dst.Offset + (int)regSize);
                size -= regSize;
            }
            assert(size < regSize);
            if ((size > 0) && (size < regSize))
            {
                assert(regSize >= XMM_REGSIZE_BYTES);
                if (!BitOperations.IsPow2(size) || (size > REGSIZE_BYTES))
                {
                    regSize = (uint)_compiler.roundUpSimdSize((int)size);
                    src.Offset = unchecked(src.Offset - (int)(regSize - size));
                    dst.Offset = unchecked(dst.Offset - (int)(regSize - size));
                    EmitMoves(ins, regSize, tempReg);
                    size = 0;
                }
            }
        }

        if (size > 0)
        {
            var tempReg = InternalRegisters.GetSingle(node, new regMaskTP(SRBM_ALLINT));
            var scalarSize = (uint)REGSIZE_BYTES;
            while (scalarSize > size)
            {
                scalarSize /= 2;
            }
            for (; size > scalarSize; size -= scalarSize,
                src.Offset = unchecked(src.Offset + (int)scalarSize),
                dst.Offset = unchecked(dst.Offset + (int)scalarSize))
            {
                EmitMoves(INS_mov, scalarSize, tempReg);
            }
            if (size > 0)
            {
                assert(size <= REGSIZE_BYTES);
                scalarSize = uint.Min(scalarSize, (uint)Compiler.roundUpGprSize((int)size));
                var shiftBack = scalarSize - size;
                assert(shiftBack <= scalarSize);
                src.Offset = unchecked(src.Offset - (int)shiftBack);
                dst.Offset = unchecked(dst.Offset - (int)shiftBack);
                EmitMoves(INS_mov, scalarSize, tempReg);
            }
        }
#endif
    }

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    private (int Local, regNumber Base, regNumber Index, uint Scale, int Offset) genConsumeBlockAddress(GenTree addr)
    {
        if (!addr.IsContained)
        {
            return (BAD_VAR_NUM, genConsumeReg(addr), REG_NA, 1, 0);
        }
        if (addr.Oper == GT_LEA)
        {
            var mode = addr.AsAddrMode();
            var baseReg = mode.BaseAddress is not null ? genConsumeReg(mode.BaseAddress) : REG_NA;
            var indexReg = REG_NA;
            var scale = 1u;
            if (mode.Index is not null)
            {
                indexReg = genConsumeReg(mode.Index);
                scale = mode.Scale;
            }

            return (BAD_VAR_NUM, baseReg, indexReg, scale, mode.Offset);
        }

        assert(addr.Oper == GT_LCL_ADDR);
        var local = addr.AsLclVarCommon();

        return (local.LclNum, REG_NA, REG_NA, 1, local.LclOffs);
    }

    private instruction simdUnalignedMovIns()
    {
        // Legacy MOVUPS is shorter; VEX MOVDQU has broader port availability
        // than VEX MOVUPS on older processors, with the same encoding size.
        return _compiler.canUseVexEncoding() ? INS_movdqu32 : INS_movups;
    }
#endif
}
