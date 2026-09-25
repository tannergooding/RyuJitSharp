// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPutArgStk(GenTreePutArgStk putArgStk)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack argument generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var data = putArgStk.Op1;
        var targetType = data.Type.ActualType;
        var baseVarNum = getBaseVarForPutArgStk(putArgStk);

        if (data.Oper is GT_FIELD_LIST)
        {
            genPutArgStkFieldList(putArgStk, baseVarNum);
            return;
        }
        else if (varTypeIsStruct(targetType))
        {
            _stkArgVarNum = baseVarNum;
            _stkArgOffset = putArgStk.ArgOffset;
            genPutStructArgStk(putArgStk);
            _stkArgVarNum = BAD_VAR_NUM;
            return;
        }

        noway_assert(targetType != TYP_STRUCT);
        var argOffset = putArgStk.ArgOffset;
#if DEBUG
        var call = putArgStk.Call;
        assert(call is not null);
        var callArg = call.Args.FindByNode(putArgStk);
        assert(callArg is not null);
        assert(callArg.AbiInfo.HasExactlyOneStackSegment);
        assert(argOffset == callArg.AbiInfo.Segments[0].StackOffset);
#endif
        if (data.IsContainedIntOrIImmed)
        {
            Emitter.emitIns_S_I(ins_Store(targetType), targetType.EmitSize, baseVarNum, argOffset,
                unchecked((int)data.AsIntConCommon().IconValue));
        }
        else
        {
            assert(data.IsUsedFromReg);
            _ = genConsumeReg(data);
            Emitter.emitIns_S_R(ins_Store(targetType), targetType.EmitSize, data.RegNum, baseVarNum, argOffset);
        }
#endif
    }

    public void genPutStructArgStk(GenTreePutArgStk putArgStk)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Struct stack argument generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var source = putArgStk.Op1;
        var targetType = source.Type;

        if (varTypeIsSimd(targetType))
        {
            var srcReg = genConsumeReg(source);
            assert((srcReg != REG_NA) && genIsValidFloatReg(srcReg));
            genStoreRegToStackArg(targetType, srcReg, 0);
            return;
        }

        assert(targetType == TYP_STRUCT);
        switch (putArgStk._kind)
        {
            case GenTreePutArgStk.Kind.RepInstr:
            {
                genStructPutArgRepMovs(putArgStk);
                break;
            }

            case GenTreePutArgStk.Kind.PartialRepInstr:
            {
                genStructPutArgPartialRepMovs(putArgStk);
                break;
            }

            case GenTreePutArgStk.Kind.Unroll:
            {
                genStructPutArgUnroll(putArgStk);
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

    public int getFirstArgWithStackSlot()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Incoming argument stack-slot selection requires Windows AMD64.");
#else
        // Windows AMD64 reserves homing space even for register-passed arguments.
        return 0;
#endif
    }

    public int getBaseVarForPutArgStk(GenTree treeNode)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack argument base selection requires Windows AMD64.");
#else
        assert(treeNode.Oper is GT_PUTARG_STK);
        var baseVarNum = treeNode.AsPutArgStk().PutInIncomingArgArea
            ? getFirstArgWithStackSlot()
            : _compiler.lvaOutgoingArgSpaceVar;

        return baseVarNum;
#endif
    }

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    private void genPutArgStkFieldList(GenTreePutArgStk putArgStk, int outArgVarNum)
    {
        assert(putArgStk.Op1.Oper is GT_FIELD_LIST);
        var argOffset = putArgStk.ArgOffset;

        foreach (var use in putArgStk.Op1.AsFieldList().Uses)
        {
            var nextArgNode = use.Node;
            _ = genConsumeReg(nextArgNode);
            var reg = nextArgNode.RegNum;
            var type = use.Type;
            var thisFieldOffset = unchecked(argOffset + use.Offset);

#if FEATURE_SIMD
            if (type == TYP_SIMD12)
            {
                Emitter.emitStoreSimd12ToLclOffset(unchecked((uint)outArgVarNum),
                    unchecked((uint)thisFieldOffset), reg, nextArgNode);
            }
            else
#endif
            {
                Emitter.emitIns_S_R(ins_Store(type), type.EmitSize, reg, outArgVarNum, thisFieldOffset);
            }

#if DEBUG
            var areaSize = _compiler.lvaLclStackHomeSize(outArgVarNum);
#if FEATURE_FASTTAILCALL
            var call = putArgStk.Call;
            assert(call is not null);
            if (call.IsFastTailCall)
            {
                areaSize = _compiler.lvaParameterStackSize;
            }
#endif
            assert(unchecked((uint)(thisFieldOffset + type.Size)) <= unchecked((uint)areaSize));
#endif
        }
    }

    private void genStoreRegToStackArg(var_types type, regNumber srcReg, int offset)
    {
        assert(srcReg != REG_NA);
        instruction ins;
        emitAttr attr;

        if (type == TYP_STRUCT)
        {
            // TYP_STRUCT requests one 16-byte chunk, not the entire struct.
            ins = INS_movdqu32;
            attr = EA_16BYTE;
        }
        else
        {
#if FEATURE_SIMD
            if (varTypeIsSimd(type))
            {
                assert(genIsValidFloatReg(srcReg));
                ins = ins_Store(type);
            }
            else
#endif
            {
                assert((varTypeUsesFloatReg(type) && genIsValidFloatReg(srcReg)) ||
                    (varTypeUsesIntReg(type) && genIsValidIntReg(srcReg)));
                ins = ins_Store(type);
            }
            attr = type.EmitSize;
        }

        assert(_stkArgVarNum != BAD_VAR_NUM);
        Emitter.emitIns_S_R(ins, attr, srcReg, _stkArgVarNum, unchecked(_stkArgOffset + offset));
    }

    private void genCodeForLoadOffset(instruction ins, emitAttr size, regNumber dst, GenTree source, int offset)
    {
        if (source.Oper.IsLocalRead)
        {
            var local = source.AsLclVarCommon();
            Emitter.emitIns_R_S(ins, size, dst, local.LclNum, unchecked(offset + local.LclOffs));
        }
        else
        {
            Emitter.emitIns_R_AR(ins, size, dst, source.AsIndir().Addr.RegNum, offset);
        }
    }

    private int genMove8IfNeeded(int size, regNumber longTmpReg, GenTree src, int offset)
    {
        if ((size & 8) != 0)
        {
            genCodeForLoadOffset(INS_mov, EA_8BYTE, longTmpReg, src, offset);
            genStoreRegToStackArg(TYP_LONG, longTmpReg, offset);
            return 8;
        }

        return 0;
    }

    private int genMove4IfNeeded(int size, regNumber intTmpReg, GenTree src, int offset)
    {
        if ((size & 4) != 0)
        {
            genCodeForLoadOffset(INS_mov, EA_4BYTE, intTmpReg, src, offset);
            genStoreRegToStackArg(TYP_INT, intTmpReg, offset);
            return 4;
        }

        return 0;
    }

    private int genMove2IfNeeded(int size, regNumber intTmpReg, GenTree src, int offset)
    {
        if ((size & 2) != 0)
        {
            genCodeForLoadOffset(INS_mov, EA_2BYTE, intTmpReg, src, offset);
            genStoreRegToStackArg(TYP_SHORT, intTmpReg, offset);
            return 2;
        }

        return 0;
    }

    private int genMove1IfNeeded(int size, regNumber intTmpReg, GenTree src, int offset)
    {
        if ((size & 1) != 0)
        {
            genCodeForLoadOffset(INS_mov, EA_1BYTE, intTmpReg, src, offset);
            genStoreRegToStackArg(TYP_BYTE, intTmpReg, offset);
            return 1;
        }

        return 0;
    }

    private void genStructPutArgUnroll(GenTreePutArgStk putArgNode)
    {
        var src = putArgNode.Data;
        assert(src.IsContained && (src.Type == TYP_STRUCT) &&
            ((src.Oper is GT_BLK) || src.Oper.IsLocalRead));

        if (src.Oper is GT_BLK)
        {
            _ = genConsumeReg(src.AsBlk().Addr);
        }

        var loadSize = putArgNode.ArgLoadSize;
        assert(!src.GetLayout(_compiler).HasGCPtr &&
            (loadSize <= _compiler.GetUnrollThreshold(Compiler.UnrollKind.Memcpy)));
        var offset = 0;
        var xmmTmpReg = REG_NA;
        var intTmpReg = REG_NA;

        if (loadSize >= XMM_REGSIZE_BYTES)
        {
            xmmTmpReg = _internalRegisters.GetSingle(putArgNode, new regMaskTP(SRBM_ALLFLOAT));
        }
        if ((loadSize % XMM_REGSIZE_BYTES) != 0)
        {
            intTmpReg = _internalRegisters.GetSingle(putArgNode, new regMaskTP(SRBM_ALLINT));
        }

        var slots = loadSize / XMM_REGSIZE_BYTES;
        while (slots-- > 0)
        {
            genCodeForLoadOffset(INS_movdqu32, EA_16BYTE, xmmTmpReg, src, offset);
            genStoreRegToStackArg(TYP_STRUCT, xmmTmpReg, offset);
            offset += XMM_REGSIZE_BYTES;
        }

        if ((loadSize % XMM_REGSIZE_BYTES) != 0)
        {
            offset += genMove8IfNeeded(loadSize, intTmpReg, src, offset);
            offset += genMove4IfNeeded(loadSize, intTmpReg, src, offset);
            offset += genMove2IfNeeded(loadSize, intTmpReg, src, offset);
            offset += genMove1IfNeeded(loadSize, intTmpReg, src, offset);
            assert(offset == loadSize);
        }
    }

    private void genConsumePutStructArgStk(GenTreePutArgStk putArgNode,
        regNumber dstReg, regNumber srcReg, regNumber sizeReg)
    {
        var src = putArgNode.Data;
        assert(src.IsContained);
        assert(varTypeIsStruct(src.Type));
        assert((src.Oper is GT_BLK) || src.Oper.IsLocalRead ||
            ((src.Oper is GT_IND) && varTypeIsSimd(src.Type)));
        assert(dstReg != REG_NA);
        assert(srcReg != REG_NA);
        var srcAddrReg = REG_NA;

        if (src.Oper.IsIndir)
        {
            srcAddrReg = genConsumeReg(src.AsIndir().Addr);
        }

        if (putArgNode.RegNum != dstReg)
        {
            // The destination is always a stack address, including incoming homes for tail calls.
            assert(_stkArgVarNum != BAD_VAR_NUM);
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, dstReg, _stkArgVarNum, putArgNode.ArgOffset);
        }

        if (srcAddrReg != REG_NA)
        {
            _ = Emitter.emitIns_Mov(INS_mov, EA_BYREF, srcReg, srcAddrReg, canSkip: true);
        }
        else
        {
            var local = src.AsLclVarCommon();
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, srcReg, local.LclNum, local.LclOffs);
        }

        if (sizeReg != REG_NA)
        {
            inst_RV_IV(INS_mov, sizeReg, putArgNode.StackByteSize, EA_PTRSIZE);
        }
    }

    private void genStructPutArgRepMovs(GenTreePutArgStk putArgNode)
    {
        var src = putArgNode.Op1;
        assert((src.Type == TYP_STRUCT) && !src.GetLayout(_compiler).HasGCPtr);
        assert(_internalRegisters.GetAll(putArgNode) ==
            new regMaskTP(REG_RDI.SingleTypeMask | REG_RCX.SingleTypeMask | REG_RSI.SingleTypeMask));
        assert(src.IsContained);

        genConsumePutStructArgStk(putArgNode, REG_RDI, REG_RSI, REG_RCX);
        instGen(INS_r_movsb);
    }

    private void genStructPutArgPartialRepMovs(GenTreePutArgStk putArgNode)
    {
        genConsumePutStructArgStk(putArgNode, REG_RDI, REG_RSI, REG_NA);
        var src = putArgNode.Data;
        var layout = src.GetLayout(_compiler);
        var srcAddrAttr = src.Oper.IsLocalRead ? EA_PTRSIZE : EA_BYREF;
#if DEBUG
        var numGCSlotsCopied = 0;
#endif
        assert(layout.HasGCPtr);
        var argSize = putArgNode.StackByteSize;
        assert((argSize % TARGET_POINTER_SIZE) == 0);
        var numSlots = argSize / TARGET_POINTER_SIZE;

        // Pointer slots are copied atomically and recorded as stack stores, so GC need not be disabled.
        for (var i = 0; i < numSlots;)
        {
            if (!layout.IsGCPtr(i))
            {
                var adjacentNonGCSlotCount = 0;
                do
                {
                    adjacentNonGCSlotCount++;
                    i++;
                }
                while ((i < numSlots) && !layout.IsGCPtr(i));

                if (adjacentNonGCSlotCount < CPOBJ_NONGC_SLOTS_LIMIT)
                {
                    for (; adjacentNonGCSlotCount > 0; adjacentNonGCSlotCount--)
                    {
                        instGen(INS_movsq);
                    }
                }
                else
                {
                    Emitter.emitIns_R_I(INS_mov, EA_4BYTE, REG_RCX, adjacentNonGCSlotCount);
                    instGen(INS_r_movsq);
                }
            }
            else
            {
                // String moves cannot record individual GC stack slots; use a typed load/store instead.
                var memType = layout.GetGCPtrType(i);
                Emitter.emitIns_R_AR(ins_Load(memType), memType.EmitSize, REG_RCX, REG_RSI, 0);
                genStoreRegToStackArg(memType, REG_RCX, i * TARGET_POINTER_SIZE);
#if DEBUG
                numGCSlotsCopied++;
#endif
                i++;
                if (i < numSlots)
                {
                    Emitter.emitIns_R_I(INS_add, srcAddrAttr, REG_RSI, TARGET_POINTER_SIZE);
                    Emitter.emitIns_R_I(INS_add, EA_PTRSIZE, REG_RDI, TARGET_POINTER_SIZE);
                }
            }
        }

#if DEBUG
        assert(numGCSlotsCopied == layout.GCPtrCount);
#endif
    }
#endif
}
