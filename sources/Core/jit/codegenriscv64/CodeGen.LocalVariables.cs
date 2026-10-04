// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private const uint StackProbeBoundaryThresholdBytes = 0;

    public void genCodeForLclVar(GenTreeLclVar tree)
    {
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

    public void genCodeForStoreLclFld(GenTreeLclFld tree)
    {
        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        noway_assert(targetType is not TYP_STRUCT);

#if FEATURE_SIMD
        if (targetType is TYP_SIMD12)
        {
            genStoreLclTypeSimd12(tree);
            return;
        }
#endif

        var offset = tree.LclOffs;
        noway_assert(targetReg == REG_NA);

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
            dataReg = REG_R0;
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
        assert(dataReg != REG_NA);

        var ins = ins_StoreFromSrc(dataReg, targetType);
        Emitter.emitIns_S_R(ins, targetType.EmitSize, dataReg, varNum, offset);

        genUpdateLife(tree);
        varDsc.RegNum = REG_STK;
    }

    public void genCodeForStoreLclVar(GenTreeLclVar lclNode)
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
            NYI_RISCV64("genCodeForStoreLclVar-----unimplemented on RISCV64 yet----");
            throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 multi-register local stores are not ported.");
        }

        var targetReg = lclNode.RegNum;
        var varNum = lclNode.LclNum;
        var targetType = varDsc.GetRegisterType(lclNode);

#if FEATURE_SIMD
        if (lclNode.Type is TYP_SIMD12)
        {
            genStoreLclTypeSimd12(lclNode);
            return;
        }
#endif

        genConsumeRegs(data);

        var dataReg = REG_NA;
        if (data.IsContained)
        {
            // Contained store operands are zero-inits, constants, or bitcasts.
            var zeroInit = data.IsIntegralConst(0);
            // TODO-RISCV64-CQ: supporting the SIMD.
            assert(!varTypeIsSimd(targetType));

            if (zeroInit)
            {
                dataReg = REG_R0;
            }
            else if (data.Oper.IsIntegralConst)
            {
                var immediate = data.AsIntConCommon().IconValue;
                dataReg = (targetReg == REG_NA) ? rsGetRsvdReg() : targetReg; // Use tempReg if spilled

                if (data.IsIconHandle() && data.AsIntCon().FitsInAddrBase(_compiler) &&
                    data.AsIntCon().AddrNeedsReloc(_compiler))
                {
                    // Support PC-relative addresses for handles hoisted by constant CSE.
                    var attr = targetType.EmitActualSize | EA_DSP_RELOC_FLG;
                    genEmitRiscvRelocatableImmediate(INS_addi, attr, dataReg, dataReg, immediate);
                }
                else
                {
                    RyuJitSharp.Emitter.emitLoadImmediate(true, EA_PTRSIZE, dataReg, immediate);
                }
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
        assert(dataReg != REG_NA);

        if (targetReg == REG_NA)
        {
            inst_set_SV_var(lclNode);

            var ins = ins_StoreFromSrc(dataReg, targetType);
            Emitter.emitIns_S_R(ins, targetType.EmitActualSize, dataReg, varNum, 0);

            genUpdateLife(lclNode);
            varDsc.RegNum = REG_STK;
        }
        else
        {
            if (data.Oper.IsCnsIntOrI && data.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL))
            {
                assert(data.AsIntCon().IconValue == 0);
                // Load the address from the thread pointer register.
                Emitter.emitIns_R_R(INS_mov, targetType.EmitActualSize, targetReg, REG_TP);
            }
            else
            {
                inst_Mov(targetType, targetReg, dataReg, true);
            }

            genProduceReg(lclNode);
        }
    }

    private void genLclHeapRiscV64(GenTree tree)
    {
        unchecked
        {
            assert(tree.Oper is GT_LCLHEAP);
            assert(_compiler.compLocallocUsed);

            var size = tree.AsUnOp().Op1;
            noway_assert(size.Type.ActualType is TYP_INT or TYP_I_IMPL);

            var targetReg = tree.RegNum;
            var regCnt = REG_NA;
            var tempReg = REG_NA;
            var spSourceReg = REG_SPBASE;
            var type = size.Type.ActualType;
            var attr = type.EmitSize;
            BasicBlock? endLabel = null;
            uint stackAdjustment = 0;
            const nint IllegalLastTouchDelta = -1;
            var lastTouchDelta = IllegalLastTouchDelta;
            noway_assert(IsFramePointerUsed);
            noway_assert(genStackLevel == 0);

            var pageSize = _compiler.eeGetPageSize();
            // The RISC-V privileged ISA uses a 4 KiB base page.
            noway_assert(pageSize == 0x1000);

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

                amount = unchecked((amount + STACK_ALIGN - 1) & ~((nuint)STACK_ALIGN - 1));
            }
            else
            {
                genConsumeRegAndCopy(size, targetReg);
                endLabel = genCreateTempLabel();
                Emitter.emitIns_J_cond_la(INS_beq, endLabel, targetReg, REG_R0);

                if (_compiler.info.compInitMem)
                {
                    regCnt = targetReg;
                }
                else
                {
                    regCnt = InternalRegisters.Extract(tree);
                    if (regCnt != targetReg)
                    {
                        Emitter.emitIns_R_R(INS_mov, attr, regCnt, targetReg);
                    }
                }

                inst_RV_IV(INS_addi, regCnt, STACK_ALIGN - 1, type.EmitActualSize);
                Emitter.emitIns_R_R_I(INS_andi, type.EmitActualSize, regCnt, regCnt,
                    unchecked(~((nint)STACK_ALIGN - 1)));
            }

            if (_compiler.lvaOutgoingArgSpaceSize.Value > 0)
            {
                var outgoingArgSpaceAligned = unchecked((uint)(((nuint)_compiler.lvaOutgoingArgSpaceSize.Value +
                    STACK_ALIGN - 1) & ~((nuint)STACK_ALIGN - 1)));
                tempReg = InternalRegisters.Extract(tree);
                _ = genInstrWithConstant(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE,
                    (nint)outgoingArgSpaceAligned, tempReg);
                stackAdjustment = unchecked(stackAdjustment + outgoingArgSpaceAligned);
            }

            if (size.Oper.IsCnsIntOrI)
            {
                assert(amount > 0);
                nint immediate;
                assert(STACK_ALIGN == (REGSIZE_BYTES * 2));
                assert(amount % (REGSIZE_BYTES * 2) == 0);
                var storePairCount = amount / (REGSIZE_BYTES * 2);

                if (_compiler.info.compInitMem && (storePairCount <= 4))
                {
                    immediate = unchecked(-16 * (nint)storePairCount);
                    Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, immediate);

                    immediate = -immediate;
                    while (storePairCount != 0)
                    {
                        immediate -= 8;
                        Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_R0, REG_SPBASE, immediate);
                        immediate -= 8;
                        Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_R0, REG_SPBASE, immediate);
                        storePairCount--;
                    }

                    lastTouchDelta = 0;
                    goto ALLOC_DONE;
                }
                else if (!_compiler.info.compInitMem && (amount < pageSize))
                {
                    // Probe the current SP before subtracting a sub-page allocation.
                    Emitter.emitIns_R_R_I(INS_lw, EA_4BYTE, REG_R0, REG_SP, 0);
                    lastTouchDelta = (nint)amount;
                    immediate = unchecked(-(nint)amount);
                    if (Emitter.isValidSimm12(immediate))
                    {
                        Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, immediate);
                    }
                    else
                    {
                        if (tempReg == REG_NA)
                        {
                            tempReg = InternalRegisters.Extract(tree);
                        }

                        _ = RyuJitSharp.Emitter.emitLoadImmediate(true, EA_PTRSIZE, tempReg, (nint)amount);
                        Emitter.emitIns_R_R_R(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, tempReg);
                    }

                    goto ALLOC_DONE;
                }

                assert(regCnt == REG_NA);
                if (_compiler.info.compInitMem)
                {
                    regCnt = targetReg;
                }
                else
                {
                    regCnt = InternalRegisters.Extract(tree);
                }

                instGen_Set_Reg_To_Imm((uint)amount == amount ? EA_4BYTE : EA_8BYTE, regCnt,
                    (nint)amount);
            }

            if (_compiler.info.compInitMem)
            {
                var loop = genCreateTempLabel();
                genDefineTempLabel(loop);
                Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, -16);
                Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_R0, REG_SPBASE, 8);
                Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_R0, REG_SPBASE, 0);

                assert(genIsValidIntReg(regCnt));
                Emitter.emitIns_R_R_I(INS_addi, type.EmitActualSize, regCnt, regCnt, -16);
                Emitter.emitIns_J_cond_la(INS_bne, loop, regCnt, REG_R0);
                lastTouchDelta = 0;
            }
            else
            {
                if (tempReg == REG_NA)
                {
                    tempReg = InternalRegisters.Extract(tree);
                }

                assert(regCnt != tempReg);
                // Clamp subtraction underflow before entering the page-probe loop.
                if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb))
                {
                    Emitter.emitIns_R_R_R(INS_maxu, EA_PTRSIZE, tempReg, REG_SPBASE, regCnt);
                    Emitter.emitIns_R_R_R(INS_sub, EA_PTRSIZE, regCnt, tempReg, regCnt);
                }
                else
                {
                    Emitter.emitIns_R_R_R(INS_sltu, EA_PTRSIZE, tempReg, REG_SPBASE, regCnt);
                    Emitter.emitIns_R_R_R(INS_sub, EA_PTRSIZE, regCnt, REG_SPBASE, regCnt);
                    Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, tempReg, tempReg, -1);
                    Emitter.emitIns_R_R_R(INS_and, EA_PTRSIZE, regCnt, regCnt, tempReg);
                }

                var pageSizeReg = InternalRegisters.GetSingle(tree);
                noway_assert(pageSizeReg != tempReg);
                Emitter.emitIns_R_I(INS_lui, EA_PTRSIZE, pageSizeReg, unchecked((nint)(pageSize >> 12)));
                _regSet.verifyRegUsed(pageSizeReg);
                Emitter.emitIns_R_R(INS_mov, EA_PTRSIZE, tempReg, REG_SPBASE);

                // Touch each current page before stepping SP toward the final allocation address.
                var loop = genCreateTempLabel();
                genDefineTempLabel(loop);
                Emitter.emitIns_R_R_I(INS_lw, EA_4BYTE, REG_R0, tempReg, 0);
                Emitter.emitIns_R_R_R(INS_sub, EA_4BYTE, tempReg, tempReg, pageSizeReg);
                Emitter.emitIns_J_cond_la(INS_bgeu, loop, tempReg, regCnt);
                Emitter.emitIns_R_R(INS_mov, EA_PTRSIZE, REG_SPBASE, regCnt);
                spSourceReg = regCnt;
            }

        ALLOC_DONE:
            if (stackAdjustment != 0)
            {
                assert((stackAdjustment % STACK_ALIGN) == 0);
                assert((lastTouchDelta == IllegalLastTouchDelta) || (lastTouchDelta >= 0));

                if ((lastTouchDelta == IllegalLastTouchDelta) ||
                    (unchecked(stackAdjustment + (uint)lastTouchDelta + StackProbeBoundaryThresholdBytes) >
                        pageSize))
                {
                    _ = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)stackAdjustment), tempReg);
                }
                else
                {
                    genStackPointerConstantAdjustment(unchecked(-(nint)stackAdjustment), tempReg);
                }

                _ = genInstrWithConstant(INS_addi, EA_PTRSIZE, targetReg, REG_SPBASE,
                    (nint)stackAdjustment, tempReg);
            }
            else
            {
                Emitter.emitIns_Mov(EA_PTRSIZE, targetReg, spSourceReg, true);
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
                Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, spDelta);
            }
            else
            {
                _ = RyuJitSharp.Emitter.emitLoadImmediate(true, EA_PTRSIZE, regTmp, spDelta);
                Emitter.emitIns_R_R_R(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, regTmp);
            }
        }
    }

    private void genStackPointerConstantAdjustmentWithProbe(nint spDelta, regNumber regTmp)
    {
        Emitter.emitIns_R_R_I(INS_lw, EA_4BYTE, regTmp, REG_SP, 0);
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
                Emitter.emitIns_R_R_I(INS_lw, EA_4BYTE, regTmp, REG_SP, 0);
                lastTouchDelta = 0;
            }

            return (nint)lastTouchDelta;
        }
    }

    private void genEmitRiscvRelocatableImmediate(instruction ins, emitAttr attr, regNumber dataReg, regNumber addrReg,
        nint displacement)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 relocatable immediate instruction recording is not ported.");
    }

#if FEATURE_SIMD
    private void genStoreLclTypeSimd12(GenTreeLclVarCommon tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V SIMD12 local-store recording is not ported.");
    }
#endif
}
#endif
