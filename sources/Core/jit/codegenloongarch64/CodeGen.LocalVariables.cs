// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private const uint StackProbeBoundaryThresholdBytes = 0;

    public void genCodeForLclVar(GenTreeLclVar tree)
    {
        if (!Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "This local-load implementation requires LoongArch64 or RISC-V64.");
            return;
        }

        var varNum = tree.LclNum;
        assert((uint)varNum < (uint)_compiler.lvaCount);
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        var isRegCandidate = varDsc.lvIsRegCandidate;

        assert((tree.Flags & GTF_VAR_DEF) == 0);

        if (!isRegCandidate && !tree.IsMultiReg && ((tree.Flags & GTF_SPILLED) == 0))
        {
            var targetType = varDsc.GetRegisterType(tree);
            assert(targetType is not TYP_STRUCT);

            var ins = ins_Load(targetType);
            Emitter.emitIns_R_S(ins, targetType.EmitSize, tree.RegNum, varNum, 0);
            genProduceReg(tree);
        }
    }

    public void genCodeForStoreLclFldLoongArch64(GenTreeLclFld tree)
    {
        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        noway_assert(targetType is not TYP_STRUCT);

        if (Configuration.FEATURE_SIMD && targetType is TYP_SIMD12)
        {
            genStoreLclTypeSimd12LoongArch64(tree);
            return;
        }

        var offset = tree.LclOffs;
        noway_assert(targetReg == REG_LOONGARCH64_NA);

        var varNum = tree.LclNum;
        assert((uint)varNum < (uint)_compiler.lvaCount);
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        assert(!varDsc.lvNormalizeOnStore || (targetType == genActualType(varDsc.Type)));

        var data = tree.Op1;
        genConsumeRegs(data);

        regNumber dataReg;
        if (data.IsContainedIntOrIImmed)
        {
            assert(data.IsIntegralConst(0));
            dataReg = REG_LOONGARCH64_R0;
        }
        else if (data.IsContained)
        {
            assert(data.Oper is GT_BITCAST);
            var bitCastSrc = data.AsUnOp().Op1;
            assert(!bitCastSrc.IsContained);
            dataReg = bitCastSrc.RegNum;
        }
        else
        {
            assert(!data.IsContained);
            dataReg = data.RegNum;
        }
        assert(dataReg != REG_LOONGARCH64_NA);

        var ins = ins_StoreFromSrc(dataReg, targetType);
        Emitter.emitIns_S_R(ins, targetType.EmitSize, dataReg, varNum, offset);

        genUpdateLife(tree);
        varDsc.RegNum = REG_LOONGARCH64_STK;
    }

    public void genCodeForStoreLclVarLoongArch64(GenTreeLclVar lclNode)
    {
        var data = lclNode.Op1;
        if (data.SkipCopyOrReload.IsMultiRegNode)
        {
            genMultiRegStoreToLocal(lclNode);
            return;
        }

        ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);
        if (lclNode.IsMultiReg)
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                NYIRAW("NYI_LOONGARCH64: genCodeForStoreLclVar : unimplemented on LoongArch64 yet");
            }

            var operandReg = genConsumeReg(data);
            var regCount = varDsc.lvFieldCnt;
            for (byte i = 0; i < regCount; i++)
            {
                var varReg = lclNode.GetRegByIndex(i);
                assert(varReg != REG_LOONGARCH64_NA);

                var fieldLclNum = varDsc.lvFieldLclStart + i;
                ref var fieldVarDsc = ref _compiler.lvaGetDesc(fieldLclNum);
                assert(fieldVarDsc.Type is TYP_FLOAT);

                Emitter.emitIns_R_R_I(INS_st_d, TYP_FLOAT.EmitSize, varReg, operandReg, i);
            }
            genProduceReg(lclNode);
            return;
        }

        var targetReg = lclNode.RegNum;
        var varNum = lclNode.LclNum;
        var targetType = varDsc.GetRegisterType(lclNode);

        if (Configuration.FEATURE_SIMD && lclNode.Type is TYP_SIMD12)
        {
            genStoreLclTypeSimd12LoongArch64(lclNode);
            return;
        }

        genConsumeRegs(data);

        regNumber dataReg;
        if (data.IsContained)
        {
            // Contained store operands are zero-inits, constants, or bitcasts.
            var zeroInit = data.IsIntegralConst(0);
            // TODO-LOONGARCH64-CQ: supporting the SIMD.
            assert(!varTypeIsSimd(targetType));

            if (zeroInit)
            {
                dataReg = REG_LOONGARCH64_R0;
            }
            else if (data.Oper.IsIntegralConst)
            {
                var immediate = data.AsIntConCommon().IconValue;
                Emitter.emitIns_I_la(EA_PTRSIZE, REG_LOONGARCH64_R21, immediate);
                dataReg = REG_LOONGARCH64_R21;
            }
            else
            {
                assert(data.Oper is GT_BITCAST);
                var bitCastSrc = data.AsUnOp().Op1;
                assert(!bitCastSrc.IsContained);
                dataReg = bitCastSrc.RegNum;
            }
        }
        else
        {
            assert(!data.IsContained);
            dataReg = data.RegNum;
        }
        assert(dataReg != REG_LOONGARCH64_NA);

        if (targetReg == REG_LOONGARCH64_NA)
        {
            inst_set_SV_var(lclNode);

            var ins = ins_StoreFromSrc(dataReg, targetType);
            Emitter.emitIns_S_R(ins, targetType.EmitActualSize, dataReg, varNum, 0);

            genUpdateLife(lclNode);
            varDsc.RegNum = REG_LOONGARCH64_STK;
        }
        else
        {
            if (data.Oper.IsCnsIntOrI && data.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL))
            {
                assert(data.AsIntCon().IconValue == 0);
                // Load the address from the thread pointer register.
                Emitter.emitIns_R_R_I(INS_ori, targetType.EmitActualSize, targetReg, REG_LOONGARCH64_TP, 0);
            }
            else if (dataReg != targetReg)
            {
                inst_Mov(targetType, targetReg, dataReg, true, targetType.EmitActualSize);
            }

            genProduceReg(lclNode);
        }
    }

    private void genLclHeapLoongArch64(GenTree tree)
    {
        unchecked
        {
            assert(tree.Oper is GT_LCLHEAP);
            assert(_compiler.compLocallocUsed);

            var size = tree.AsUnOp().Op1;
            noway_assert((size.Type.ActualType is var __targetAliasValue0 && (__targetAliasValue0 is TYP_INT || __targetAliasValue0 == TYP_I_IMPL)));

            var targetReg = tree.RegNum;
            var regCnt = REG_LOONGARCH64_NA;
            var type = size.Type.ActualType;
            var attr = type.EmitSize;
            BasicBlock? endLabel = null;
            uint stackAdjustment = 0;
            const nint IllegalLastTouchDelta = -1;
            var lastTouchDelta = IllegalLastTouchDelta;

            noway_assert(IsFramePointerUsed);
            noway_assert(genStackLevel == 0);

            nuint amount = 0;
            if (size.Oper.IsCnsIntOrI)
            {
                assert(size.IsContained);
                amount = unchecked((nuint)size.AsIntCon().IconValue);
                if (amount == 0)
                {
                    instGen_Set_Reg_To_Zero(EA_PTRSIZE, targetReg);
                    goto BAILOUT;
                }

                amount = unchecked((amount + (nuint)Configuration.STACK_ALIGN - 1) & ~((nuint)Configuration.STACK_ALIGN - 1));
            }
            else
            {
                genConsumeRegAndCopy(size, targetReg);
                endLabel = genCreateTempLabel();
                Emitter.emitIns_J_cond_la(INS_beq, endLabel, targetReg, REG_LOONGARCH64_R0);

                if (_compiler.info.compInitMem)
                {
                    assert(InternalRegisters.Count(tree) == 0);
                    regCnt = targetReg;
                }
                else
                {
                    regCnt = InternalRegisters.Extract(tree);
                    if (regCnt != targetReg)
                    {
                        Emitter.emitIns_R_R_I(INS_ori, attr, regCnt, targetReg, 0);
                    }
                }

                inst_RV_IV(INS_addi_d, regCnt, Configuration.STACK_ALIGN - 1, type.EmitActualSize);

                assert(regCnt != REG_LOONGARCH64_R21);
                var mask = unchecked(~((nint)Configuration.STACK_ALIGN - 1));
                Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_LOONGARCH64_R21, REG_LOONGARCH64_R0, unchecked((int)mask));
                Emitter.emitIns_R_R_R(INS_and, type.EmitActualSize, regCnt, regCnt, REG_LOONGARCH64_R21);
            }

            if (_compiler.lvaOutgoingArgSpaceSize.Value > 0)
            {
                var outgoingArgSpaceAligned = unchecked((uint)(((nuint)_compiler.lvaOutgoingArgSpaceSize.Value +
                    (nuint)Configuration.STACK_ALIGN - 1) & ~((nuint)Configuration.STACK_ALIGN - 1)));
                _ = genInstrWithConstantLoongArch64(INS_addi_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64,
                    (nint)outgoingArgSpaceAligned, REG_LOONGARCH64_RA);
                stackAdjustment = unchecked(stackAdjustment + outgoingArgSpaceAligned);
            }

            if (size.Oper.IsCnsIntOrI)
            {
                assert(amount > 0);

                nint immediate;
                var storePairSize = unchecked((nuint)Configuration.REGSIZE_BYTES * 2);
                assert(amount % storePairSize == 0);
                var storePairCount = amount / storePairSize;
                if (_compiler.info.compInitMem && (storePairCount <= 4))
                {
                    immediate = unchecked(-16 * (nint)storePairCount);
                    Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, unchecked((int)immediate));

                    immediate = -immediate;
                    while (storePairCount != 0)
                    {
                        immediate -= 8;
                        Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, unchecked((int)immediate));
                        immediate -= 8;
                        Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, unchecked((int)immediate));
                        storePairCount--;
                    }

                    lastTouchDelta = 0;
                    goto ALLOC_DONE;
                }
                else if (!_compiler.info.compInitMem && (amount < _compiler.eeGetPageSize()))
                {
                    // Probe the current SP before subtracting a sub-page allocation.
                    Emitter.emitIns_R_R_I(INS_ld_w, EA_4BYTE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, 0);

                    lastTouchDelta = unchecked((nint)amount);
                    immediate = unchecked(-(nint)amount);
                    if (Emitter.isValidSimm12(immediate))
                    {
                        Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, unchecked((int)immediate));
                    }
                    else
                    {
                        Emitter.emitIns_I_la(EA_PTRSIZE, REG_LOONGARCH64_RA, unchecked((nint)amount));
                        Emitter.emitIns_R_R_R(INS_sub_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, REG_LOONGARCH64_RA);
                    }

                    goto ALLOC_DONE;
                }

                assert(regCnt == REG_LOONGARCH64_NA);
                if (_compiler.info.compInitMem)
                {
                    assert(InternalRegisters.Count(tree) == 0);
                    regCnt = targetReg;
                }
                else
                {
                    regCnt = InternalRegisters.Extract(tree);
                }

                instGen_Set_Reg_To_Imm((uint)amount == amount ? EA_4BYTE : EA_8BYTE, regCnt,
                    unchecked((nint)amount));
            }

            if (_compiler.info.compInitMem)
            {
                Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, -16);
                Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, 8);
                Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, 0);

                assert(genIsValidIntReg(regCnt));
                Emitter.emitIns_R_R_I(INS_addi_d, type.EmitActualSize, regCnt, regCnt, -16);
                Emitter.emitIns_R_R_I(INS_bne, EA_PTRSIZE, regCnt, REG_LOONGARCH64_R0, -16);
                lastTouchDelta = 0;
            }
            else
            {
                // Clamp underflow before probing so SP always remains on a valid page.
                var regTmp = InternalRegisters.GetSingle(tree);

                assert(regCnt != REG_LOONGARCH64_R21);
                Emitter.emitIns_R_R_R(INS_sltu, EA_PTRSIZE, REG_LOONGARCH64_R21, REG_SPBASE_LOONGARCH64, regCnt);
                Emitter.emitIns_R_R_R(INS_sub_d, EA_PTRSIZE, regCnt, REG_SPBASE_LOONGARCH64, regCnt);
                Emitter.emitIns_R_R_R(INS_masknez, EA_PTRSIZE, regCnt, regCnt, REG_LOONGARCH64_R21);

                var pageSize = _compiler.eeGetPageSize();
                assert(pageSize == ((pageSize >> 12) << 12));
                Emitter.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, regTmp, unchecked((nint)(pageSize >> 12)));
                _regSet.verifyRegUsed(regTmp);

                Emitter.emitIns_R_R_I(INS_ld_w, EA_4BYTE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, 0);
                Emitter.emitIns_R_R_R(INS_sub_d, EA_PTRSIZE, REG_LOONGARCH64_R21, REG_SPBASE_LOONGARCH64, regTmp);
                assert(regTmp != REG_LOONGARCH64_R21);

                Emitter.emitIns_R_R_I(INS_bltu, EA_PTRSIZE, REG_LOONGARCH64_R21, regCnt, 12);
                Emitter.emitIns_R_R_R(INS_sub_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, regTmp);
                Emitter.emitIns_I(INS_b, EA_PTRSIZE, -16);
                Emitter.emitIns_R_R_I(INS_ori, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, regCnt, 0);
            }

        ALLOC_DONE:
            if (stackAdjustment != 0)
            {
                assert((stackAdjustment % Configuration.STACK_ALIGN) == 0);
                assert((lastTouchDelta == IllegalLastTouchDelta) || (lastTouchDelta >= 0));

                var tmpReg = REG_LOONGARCH64_RA;
                if ((lastTouchDelta == IllegalLastTouchDelta) ||
                    (unchecked(stackAdjustment + (uint)lastTouchDelta + StackProbeBoundaryThresholdBytes) >
                        _compiler.eeGetPageSize()))
                {
                    // The allocation can leave SP near a guard boundary; probe while restoring outgoing space.
                    _ = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)stackAdjustment), tmpReg);
                }
                else
                {
                    genStackPointerConstantAdjustment(unchecked(-(nint)stackAdjustment), tmpReg);
                }

                _ = genInstrWithConstantLoongArch64(INS_addi_d, EA_PTRSIZE, targetReg, REG_SPBASE_LOONGARCH64,
                    unchecked((nint)stackAdjustment), tmpReg);
            }
            else
            {
                inst_Mov(TYP_I_IMPL, targetReg, REG_SPBASE_LOONGARCH64, canSkip: false);
            }

        BAILOUT:
            if (endLabel is not null)
            {
                genDefineTempLabel(endLabel);
            }

            genProduceReg(tree);
        }
    }

    private void genStackPointerConstantAdjustment(nint spDelta, regNumber regTmp)
    {
        unchecked
        {
            assert(spDelta < 0);
            assert((nuint)(-spDelta) <= _compiler.eeGetPageSize());

            if (Emitter.isValidSimm12(spDelta))
            {
                Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, unchecked((int)spDelta));
            }
            else
            {
                Emitter.emitIns_I_la(EA_PTRSIZE, regTmp, spDelta);
                Emitter.emitIns_R_R_R(INS_add_d, EA_PTRSIZE, REG_SPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, regTmp);
            }
        }
    }

    private void genStackPointerConstantAdjustmentWithProbe(nint spDelta, regNumber regTmp)
    {
        Emitter.emitIns_R_R_I(INS_ld_w, EA_4BYTE, regTmp, REG_SPBASE_LOONGARCH64, 0);
        genStackPointerConstantAdjustment(spDelta, regTmp);
    }

    private nint genStackPointerConstantAdjustmentLoopWithProbe(nint spDelta, regNumber regTmp)
    {
        unchecked
        {
            assert(spDelta < 0);
            var pageSize = _compiler.eeGetPageSize();
            var remaining = spDelta;

            do
            {
                var oneDelta = -(nint)nuint.Min((nuint)(-remaining), pageSize);
                genStackPointerConstantAdjustmentWithProbe(oneDelta, regTmp);
                remaining -= oneDelta;
            }
            while (remaining < 0);

            var lastTouchDelta = (nuint)(-spDelta) % pageSize;
            if ((lastTouchDelta == 0) ||
                (lastTouchDelta + StackProbeBoundaryThresholdBytes > pageSize))
            {
                Emitter.emitIns_R_R_I(INS_ld_w, EA_4BYTE, regTmp, REG_SPBASE_LOONGARCH64, 0);
                lastTouchDelta = 0;
            }

            return (nint)lastTouchDelta;
        }
    }

    private void genStoreLclTypeSimd12LoongArch64(GenTreeLclVarCommon tree)
    {
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD12 local-store recording is not ported.");
        return;
    }
}
