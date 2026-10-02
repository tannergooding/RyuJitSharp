// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPutArgStk(GenTreePutArgStk putArgStk)
    {
#if TARGET_X86
        var data = putArgStk.Op1;
        var targetType = data.Type.ActualType;
        assert(targetType != TYP_LONG);
        assert((putArgStk.StackByteSize % TARGET_POINTER_SIZE) == 0);

        genAlignStackBeforeCall(putArgStk);

        if (data.Oper is GT_FIELD_LIST)
        {
            genPutArgStkFieldList(putArgStk);
            return;
        }

        if (varTypeIsStruct(targetType))
        {
            genAdjustStackForPutArgStk(putArgStk);
            genPutStructArgStk(putArgStk);
            return;
        }

        genConsumeRegs(data);
        if (data.IsUsedFromReg)
        {
            genPushReg(targetType, data.RegNum);
        }
        else
        {
            assert(data.Type.Size == TARGET_POINTER_SIZE);
            inst_TT(INS_push, data.Type.EmitSize, data);
            AddStackLevel(TARGET_POINTER_SIZE);
        }
#elif !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Stack argument generation requires xarch.");
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
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Struct stack argument generation requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        var source = putArgStk.Op1;
        var targetType = source.Type;

#if TARGET_X86 && FEATURE_SIMD
        if (putArgStk.IsSimd12)
        {
            genPutArgStkSimd12(putArgStk);
            return;
        }
#endif
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
#if TARGET_X86
                unreached();
#else
                genStructPutArgPartialRepMovs(putArgStk);
#endif
                break;
            }

            case GenTreePutArgStk.Kind.Unroll:
            {
                genStructPutArgUnroll(putArgStk);
                break;
            }

#if TARGET_X86
            case GenTreePutArgStk.Kind.Push:
            {
                genStructPutArgPush(putArgStk);
                break;
            }
#endif
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
#if UNIX_AMD64_ABI || TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
        for (var index = 0; index < _compiler.info.compArgsCount; index++)
        {
            assert(_compiler.lvaGetDesc(index).lvIsParam);
            ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(index);
            assert(!abiInfo.IsSplitAcrossRegistersAndStack);
            if (abiInfo.HasAnyStackSegment)
            {
                return index;
            }
        }

        assert(false, "Expected to find a parameter passed on the stack");
        return BAD_VAR_NUM;
#elif TARGET_X86
        throw new FatalJitException(CORJIT_SKIPPED, "The first incoming stack argument is not implemented for x86.");
#elif TARGET_AMD64
        return 0;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Incoming stack argument selection is not yet ported for this target.");
#endif
    }

    public int getBaseVarForPutArgStk(GenTree treeNode)
    {
#if TARGET_X86
        assert(treeNode.Oper is GT_PUTARG_STK);
        if (treeNode.AsPutArgStk().PutInIncomingArgArea)
        {
            return getFirstArgWithStackSlot();
        }

#if FEATURE_FIXED_OUT_ARGS
        return _compiler.lvaOutgoingArgSpaceVar;
#else
        assert(false, "No BaseVarForPutArgStk on x86");
        return BAD_VAR_NUM;
#endif
#elif !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Stack argument base selection requires xarch.");
#else
        assert(treeNode.Oper is GT_PUTARG_STK);
        var baseVarNum = treeNode.AsPutArgStk().PutInIncomingArgArea
            ? getFirstArgWithStackSlot()
            : _compiler.lvaOutgoingArgSpaceVar;

        return baseVarNum;
#endif
    }

#if TARGET_X86
    private bool genAdjustStackForPutArgStk(GenTreePutArgStk putArgStk)
    {
        var argSize = putArgStk.StackByteSize;
        var source = putArgStk.Op1;

#if FEATURE_SIMD
        if ((source.Oper is not GT_FIELD_LIST) && varTypeIsSimd(source.Type))
        {
            inst_RV_IV(INS_sub, REG_SPBASE, argSize, EA_PTRSIZE);
            AddStackLevel(unchecked((uint)argSize));
            _pushStkArg = false;
            return true;
        }
#endif

#if DEBUG
        switch (putArgStk._kind)
        {
            case GenTreePutArgStk.Kind.RepInstr:
            case GenTreePutArgStk.Kind.Unroll:
            {
                assert(!source.GetLayout(_compiler).HasGCPtr);
                break;
            }

            case GenTreePutArgStk.Kind.Push:
            {
                assert((source.Oper is GT_FIELD_LIST) || source.GetLayout(_compiler).HasGCPtr ||
                    (argSize < XMM_REGSIZE_BYTES));
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif

        if (!putArgStk.IsPush)
        {
            if ((argSize >= ARG_STACK_PROBE_THRESHOLD_BYTES) ||
                _compiler.compStressCompile(Compiler.STRESS_GENERIC_VARN, 5))
            {
                _ = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)argSize),
                    trackSpAdjustments: true);
            }
            else
            {
                inst_RV_IV(INS_sub, REG_SPBASE, argSize, EA_PTRSIZE);
            }

            AddStackLevel(unchecked((uint)argSize));
            _pushStkArg = false;
            return true;
        }

        _pushStkArg = true;
        return false;
    }

    private void genStructPutArgPush(GenTreePutArgStk putArgNode)
    {
        assert(_pushStkArg);
        var source = putArgNode.Data;
        var sourceReg = REG_NA;
        var sourceLocal = BAD_VAR_NUM;
        var sourceOffset = 0;

        if (source.Oper.IsLocalRead)
        {
            assert(source.IsContained);
            var local = source.AsLclVarCommon();
            sourceLocal = local.LclNum;
            sourceOffset = local.LclOffs;
        }
        else
        {
            sourceReg = genConsumeReg(source.AsBlk().Addr);
        }

        var layout = source.GetLayout(_compiler);
        var loadSize = putArgNode.ArgLoadSize;
        assert(((loadSize < XMM_REGSIZE_BYTES) || layout.HasGCPtr) &&
            ((loadSize % TARGET_POINTER_SIZE) == 0));

        for (var slot = (loadSize / TARGET_POINTER_SIZE) - 1; slot >= 0; slot--)
        {
            var slotAttr = layout.GetGCPtrType(slot).EmitSize;
            var offset = slot * TARGET_POINTER_SIZE;
            if (sourceReg != REG_NA)
            {
                Emitter.emitIns_AR_R(INS_push, slotAttr, REG_NA, sourceReg, offset);
            }
            else
            {
                Emitter.emitIns_S(INS_push, slotAttr, sourceLocal, unchecked(sourceOffset + offset));
            }

            AddStackLevel(TARGET_POINTER_SIZE);
        }
    }

    private void genPutArgStkFieldList(GenTreePutArgStk putArgStk)
    {
        var fieldList = putArgStk.Op1.AsFieldList();
        var preAdjustedStack = genAdjustStackForPutArgStk(putArgStk);
        assert(putArgStk.IsPush && !preAdjustedStack && _pushStkArg);

        var currentOffset = preAdjustedStack ? 0 : putArgStk.StackByteSize;
        var previousOffset = currentOffset;
        var intTmpReg = REG_NA;
        var simdTmpReg = REG_NA;
        if (_internalRegisters.Count(putArgStk) != 0)
        {
            var reserved = _internalRegisters.GetAll(putArgStk);
            if (!(reserved & new regMaskTP(SRBM_ALLINT)).IsEmpty)
            {
                intTmpReg = _internalRegisters.GetSingle(putArgStk, new regMaskTP(SRBM_ALLINT));
                assert(genIsValidIntReg(intTmpReg));
            }
            if (!(reserved & new regMaskTP(SRBM_ALLFLOAT)).IsEmpty)
            {
                simdTmpReg = _internalRegisters.GetSingle(putArgStk, new regMaskTP(SRBM_ALLFLOAT));
                assert(genIsValidFloatReg(simdTmpReg));
            }
            assert(_internalRegisters.Count(putArgStk) ==
                (uint)((intTmpReg == REG_NA ? 0 : 1) + (simdTmpReg == REG_NA ? 0 : 1)));
        }

        foreach (var use in fieldList.Uses)
        {
            var fieldNode = use.Node;
            var fieldOffset = use.Offset;
            var fieldType = use.Type;
            assert(!varTypeIsLong(fieldType));
            assert(fieldOffset <= previousOffset);

            genConsumeRegs(fieldNode);
            var argReg = fieldNode.IsUsedFromSpillTemp ? REG_NA : fieldNode.RegNum;
            var fieldIsSlot = ((fieldOffset % 4) == 0) && ((previousOffset - fieldOffset) >= 4);
            var adjustment = roundUp(currentOffset - fieldOffset, 4);

            if (fieldIsSlot && !varTypeIsSimd(fieldType))
            {
                var pushSize = fieldType.ActualType.Size;
                assert((pushSize % 4) == 0);
                adjustment -= pushSize;
                assert((adjustment % TARGET_POINTER_SIZE) == 0);
                while (adjustment != 0)
                {
                    inst_IV(INS_push, 0);
                    currentOffset -= TARGET_POINTER_SIZE;
                    AddStackLevel(TARGET_POINTER_SIZE);
                    adjustment -= TARGET_POINTER_SIZE;
                }

                _pushStkArg = true;
            }
            else
            {
                _pushStkArg = false;
                assert(varTypeIsIntegralOrI(fieldNode.Type) || varTypeIsSimd(fieldNode.Type));
                if (adjustment != 0)
                {
                    inst_RV_IV(INS_sub, REG_SPBASE, adjustment, EA_PTRSIZE);
                    currentOffset -= adjustment;
                    AddStackLevel(unchecked((uint)adjustment));
                }

                if (varTypeIsByte(fieldType) && ((argReg == REG_NA) ||
                    ((argReg != REG_EAX) && (argReg != REG_EBX) &&
                     (argReg != REG_ECX) && (argReg != REG_EDX))))
                {
                    assert(intTmpReg != REG_NA);
                    noway_assert((intTmpReg == REG_EAX) || (intTmpReg == REG_EBX) ||
                        (intTmpReg == REG_ECX) || (intTmpReg == REG_EDX));
                    if (argReg != REG_NA)
                    {
                        inst_Mov(fieldType, intTmpReg, argReg, canSkip: false);
                        argReg = intTmpReg;
                    }
                }
            }

            var canStoreFullSlot = fieldIsSlot;
            var canLoadFullSlot = genIsValidIntReg(argReg);
            if (argReg == REG_NA)
            {
                assert(fieldNode.Type.Size <= TARGET_POINTER_SIZE);
                assert(fieldNode.Type.ActualType.Size == fieldType.ActualType.Size);
                canLoadFullSlot = (fieldNode.Type.Size == TARGET_POINTER_SIZE) ||
                    fieldNode.IsUsedFromSpillTemp ||
                    (fieldNode.Oper.IsLocalRead && (fieldNode.Type.Size >= fieldType.Size));
            }

            if (canStoreFullSlot && canLoadFullSlot)
            {
                assert(_pushStkArg);
                assert(fieldNode.Type.Size <= TARGET_POINTER_SIZE);
                inst_TT(INS_push, fieldNode.Type.EmitActualSize, fieldNode);
                currentOffset -= TARGET_POINTER_SIZE;
                AddStackLevel(TARGET_POINTER_SIZE);
            }
            else
            {
                assert(!varTypeIsGC(fieldNode.Type));
                if (argReg == REG_NA)
                {
                    assert(varTypeIsIntegralOrI(fieldNode.Type) && genIsValidIntReg(intTmpReg));
                    if (fieldNode.IsContainedIntOrIImmed)
                    {
                        genSetRegToConst(intTmpReg, fieldNode.Type, fieldNode);
                    }
                    else
                    {
                        var loadIns = canLoadFullSlot ? INS_mov : ins_Load(fieldNode.Type);
                        var loadSize = canLoadFullSlot ? EA_PTRSIZE : fieldNode.Type.EmitSize;
                        inst_RV_TT(loadIns, loadSize, intTmpReg, fieldNode);
                    }

                    argReg = intTmpReg;
                }

#if FEATURE_SIMD
                if (fieldType == TYP_SIMD12)
                {
                    assert(genIsValidFloatReg(simdTmpReg));
                    genStoreSimd12ToStack(argReg, simdTmpReg);
                }
                else
#endif
                {
                    var storeType = canStoreFullSlot ? fieldType.ActualType : fieldType;
                    genStoreRegToStackArg(storeType, argReg, fieldOffset - currentOffset);
                }

                if (_pushStkArg)
                {
                    currentOffset -= roundUp(fieldType.Size, TARGET_POINTER_SIZE);
                }
            }

            previousOffset = fieldOffset;
        }

        if (currentOffset != 0)
        {
            inst_RV_IV(INS_sub, REG_SPBASE, currentOffset, EA_PTRSIZE);
            AddStackLevel(unchecked((uint)currentOffset));
        }
    }
#endif

#if TARGET_XARCH
#if TARGET_X86
    private bool _pushStkArg;

    private void genPushReg(var_types type, regNumber srcReg)
    {
        var size = type.Size;
        if (varTypeIsIntegralOrI(type) && (type != TYP_LONG))
        {
            assert(genIsValidIntReg(srcReg));
            inst_RV(INS_push, srcReg, type);
        }
        else
        {
            var ins = type == TYP_LONG ? INS_movq : ins_Store(type);
            assert(genIsValidFloatReg(srcReg));
            inst_RV_IV(INS_sub, REG_SPBASE, size, EA_PTRSIZE);
            Emitter.emitIns_AR_R(ins, type.EmitSize, srcReg, REG_SPBASE, 0);
        }

        AddStackLevel(unchecked((uint)size));
    }
#endif

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
#if TARGET_X86
            if (type == TYP_LONG)
            {
                assert(genIsValidFloatReg(srcReg));
                ins = INS_movq;
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

#if TARGET_X86
        if (_pushStkArg)
        {
            genPushReg(type, srcReg);
        }
        else
        {
            Emitter.emitIns_AR_R(ins, attr, srcReg, REG_SPBASE, offset);
        }
#else
        assert(_stkArgVarNum != BAD_VAR_NUM);
        Emitter.emitIns_S_R(ins, attr, srcReg, _stkArgVarNum, unchecked(_stkArgOffset + offset));
#endif
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
#if TARGET_X86
            genCodeForLoadOffset(INS_movq, EA_8BYTE, longTmpReg, src, offset);
#else
            genCodeForLoadOffset(INS_mov, EA_8BYTE, longTmpReg, src, offset);
#endif
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

#if TARGET_X86
        assert(!_pushStkArg);
#endif

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
        var longTmpReg = REG_NA;

#if TARGET_X86
        if (loadSize >= 8)
#else
        if (loadSize >= XMM_REGSIZE_BYTES)
#endif
        {
            xmmTmpReg = _internalRegisters.GetSingle(putArgNode, new regMaskTP(SRBM_ALLFLOAT));
        }
        if ((loadSize % XMM_REGSIZE_BYTES) != 0)
        {
            intTmpReg = _internalRegisters.GetSingle(putArgNode, new regMaskTP(SRBM_ALLINT));
        }

#if TARGET_X86
        longTmpReg = xmmTmpReg;
#else
        longTmpReg = intTmpReg;
#endif

        var slots = loadSize / XMM_REGSIZE_BYTES;
        while (slots-- > 0)
        {
            genCodeForLoadOffset(INS_movdqu32, EA_16BYTE, xmmTmpReg, src, offset);
            genStoreRegToStackArg(TYP_STRUCT, xmmTmpReg, offset);
            offset += XMM_REGSIZE_BYTES;
        }

        if ((loadSize % XMM_REGSIZE_BYTES) != 0)
        {
            offset += genMove8IfNeeded(loadSize, longTmpReg, src, offset);
            offset += genMove4IfNeeded(loadSize, intTmpReg, src, offset);
            offset += genMove2IfNeeded(loadSize, intTmpReg, src, offset);
            offset += genMove1IfNeeded(loadSize, intTmpReg, src, offset);
            assert(offset == loadSize);
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

#if !TARGET_X86
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
#endif
}
