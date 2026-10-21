// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insBarrier;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLockAdd(GenTreeOp node)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Locked addition generation outside xarch is not implemented.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        assert(node.Oper is GT_LOCKADD);
        var addr = node.Op1;
        var data = node.Op2;
        var size = data.Type.EmitActualSize;
        assert(addr.IsUsedFromReg);
        assert(data.IsUsedFromReg || data.IsContainedIntOrIImmed);
        assert((size == EA_4BYTE) || (size == EA_PTRSIZE));

        genConsumeOperands(node);
        instGen(INS_lock);
        if (data.IsContainedIntOrIImmed)
        {
            var imm = unchecked((int)data.AsIntCon().IconValue);
            assert(imm == data.AsIntCon().IconValue);
            if (imm == 1)
            {
                Emitter.emitIns_AR(INS_inc, size, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
            else if (imm == -1)
            {
                Emitter.emitIns_AR(INS_dec, size, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
            else
            {
                Emitter.emitIns_I_AR(INS_add, size, imm, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
        }
        else
        {
            Emitter.emitIns_AR_R(INS_add, size, data.RegNum, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
        }
    }

    public void genLockedInstructions(GenTreeOp node)
    {
        if (Configuration.TARGET_ARM64)
        {
            var arm64Data = node.Op2;
            var arm64Address = node.Op1;
            var targetReg = node.RegNum;
            var dataReg = arm64Data.RegNum;
            var addrReg = arm64Address.RegNum;

            genConsumeAddress(arm64Address);
            genConsumeRegs(arm64Data);

            assert((node.Oper is GT_XCHG) || !varTypeIsSmall(node.Type));

            var dataSize = arm64Data.Type.EmitActualSize;

            if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Atomics))
            {
                assert(!arm64Data.IsContainedIntOrIImmed);
                assert((targetReg != REG_NA) && (targetReg != TargetRuntimeMetadata.ZeroRegister));

                // These instructions change semantics when targetReg is ZR (the memory ordering becomes weaker).
                // See atomicBarrierDroppedOnZero in LLVM.
                switch (node.Oper)
                {
                    case GT_XORR:
                    {
                        Emitter.emitIns_R_R_R(INS_ldsetal, dataSize, dataReg, targetReg, addrReg);
                        break;
                    }

                    case GT_XAND:
                    {
                        var tempReg = InternalRegisters.GetSingle(node);
                        Emitter.emitIns_R_R(INS_mvn, dataSize, tempReg, dataReg);
                        Emitter.emitIns_R_R_R(INS_ldclral, dataSize, tempReg, targetReg, addrReg);
                        break;
                    }

                    case GT_XCHG:
                    {
                        var ins = INS_swpal;
                        if (varTypeIsByte(node.Type))
                        {
                            ins = INS_swpalb;
                        }
                        else if (varTypeIsShort(node.Type))
                        {
                            ins = INS_swpalh;
                        }

                        Emitter.emitIns_R_R_R(ins, dataSize, dataReg, targetReg, addrReg);
                        break;
                    }

                    case GT_XADD:
                    {
                        Emitter.emitIns_R_R_R(INS_ldaddal, dataSize, dataReg, targetReg, addrReg);
                        break;
                    }

                    default:
                    {
                        assert(false, "Unexpected operation in locked instruction generation.");
                        break;
                    }
                }
            }
            else
            {
                assert((node.Oper is not GT_XORR) && (node.Oper is not GT_XAND));

                var allIntRegs = new regMaskTP(SRBM_ALLINT);
                var exResultReg = InternalRegisters.Extract(node, allIntRegs);
                var storeDataReg = (node.Oper is GT_XCHG) ? dataReg : InternalRegisters.Extract(node, allIntRegs);
                var loadReg = (targetReg != REG_NA) ? targetReg : storeDataReg;

                // Check allocator assumptions: store-exclusive unpredictable cases must be avoided.
                noway_assert(addrReg != targetReg);
                noway_assert(addrReg != loadReg);
                noway_assert(dataReg != loadReg);
                noway_assert(addrReg != storeDataReg);
                noway_assert((node.Oper is GT_XCHG) || (addrReg != dataReg));
                assert(arm64Address.IsUsedFromReg);
                noway_assert(exResultReg != REG_NA);
                noway_assert(exResultReg != targetReg);
                noway_assert((targetReg != REG_NA) || (node.Oper is not GT_XCHG));
                noway_assert(exResultReg != storeDataReg);
                noway_assert(exResultReg != addrReg);

                // genConsumeAddress marks the address non-pointer at first use, but the retry loop uses it
                // repeatedly. Keep it reported as a GC pointer until the loop has completed.
                _gcInfo.gcMarkRegPtrVal(addrReg, arm64Address.Type);

                // Emit code like this:
                //   retry:
                //     ldaxr loadReg, [addrReg]
                //     add storeDataReg, loadReg, dataReg         # Only for GT_XADD
                //                                                # GT_XCHG storeDataReg === dataReg
                //     stlxr exResultReg, storeDataReg, [addrReg]
                //     cbnz exResultReg, retry
                //     dmb ish
                var labelRetry = genCreateTempLabel();
                genDefineTempLabel(labelRetry);

                var insLd = INS_ldaxr;
                var insSt = INS_stlxr;
                if (varTypeIsByte(node.Type))
                {
                    insLd = INS_ldaxrb;
                    insSt = INS_stlxrb;
                }
                else if (varTypeIsShort(node.Type))
                {
                    insLd = INS_ldaxrh;
                    insSt = INS_stlxrh;
                }

                // The load instruction includes an acquire half barrier.
                Emitter.emitIns_R_R(insLd, dataSize, loadReg, addrReg);

                switch (node.Oper)
                {
                    case GT_XADD:
                    {
                        if (arm64Data.IsContainedIntOrIImmed)
                        {
                            _ = genInstrWithConstant(INS_add, dataSize, storeDataReg, loadReg,
                                arm64Data.AsIntConCommon().IconValue, REG_NA);
                        }
                        else
                        {
                            Emitter.emitIns_R_R_R(INS_add, dataSize, storeDataReg, loadReg, dataReg);
                        }

                        break;
                    }

                    case GT_XCHG:
                    {
                        assert(!arm64Data.IsContained);
                        storeDataReg = dataReg;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                // The store instruction includes a release half barrier.
                Emitter.emitIns_R_R_R(insSt, dataSize, exResultReg, storeDataReg, addrReg);
                Arm64EmitRegisterBranch(INS_cbnz, EA_4BYTE, labelRetry, exResultReg);
                instGen_MemoryBarrier(BARRIER_FULL);
                _gcInfo.gcMarkRegSetNpt(arm64Address.RegMask);
            }

            if (targetReg != REG_NA)
            {
                if (varTypeIsSmall(node.Type) && varTypeIsSigned(node.Type))
                {
                    var mov = varTypeIsShort(node.Type) ? INS_sxth : INS_sxtb;
                    _ = Emitter.emitIns_Mov(mov, EA_4BYTE, targetReg, targetReg, canSkip: false);
                }

                genProduceReg(node);
            }
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "unimplemented on LOONGARCH64 yet");
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            assert(!varTypeIsSmall(node.Type));

            var riscvData = node.Op2;
            var riscvAddress = node.Op1;
            var dataReg = !riscvData.IsContained ? riscvData.RegNum : TargetRuntimeMetadata.ZeroRegister;
            var addrReg = riscvAddress.RegNum;
            var targetReg = node.RegNum;
            if (targetReg == REG_NA)
            {
                targetReg = TargetRuntimeMetadata.ZeroRegister;
            }

            genConsumeAddress(riscvAddress);
            genConsumeRegs(riscvData);

            var dataSize = riscvData.Type.EmitActualSize;
            var is4 = dataSize == EA_4BYTE;
            instruction ins;
            switch (node.Oper)
            {
                case GT_XORR:
                {
                    ins = is4 ? INS_amoor_w : INS_amoor_d;
                    break;
                }

                case GT_XAND:
                {
                    ins = is4 ? INS_amoand_w : INS_amoand_d;
                    break;
                }

                case GT_XCHG:
                {
                    ins = is4 ? INS_amoswap_w : INS_amoswap_d;
                    break;
                }

                case GT_XADD:
                {
                    ins = is4 ? INS_amoadd_w : INS_amoadd_d;
                    break;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR, "Unexpected operation in locked instruction generation.");
                    return;
                }
            }

            Emitter.emitIns_R_R_R(ins, dataSize, targetReg, addrReg, dataReg);

            if (targetReg != TargetRuntimeMetadata.ZeroRegister)
            {
                genProduceReg(node);
            }
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Locked instruction generation outside xarch is not implemented.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        assert(node.Oper is GT_XADD or GT_XCHG or GT_XORR or GT_XAND);
        assert((node.Oper is GT_XCHG) || !varTypeIsSmall(node.Type));
        var addr = node.Op1;
        var data = node.Op2;
        var size = node.Type.EmitSize;
        assert(addr.IsUsedFromReg && data.IsUsedFromReg);
        assert((size <= EA_PTRSIZE) || (size == EA_GCREF));
        genConsumeOperands(node);

        if (node.Oper is GT_XORR or GT_XAND)
        {
            var ins = (node.Oper is GT_XORR) ? INS_or : INS_and;
            if (node.IsUnusedValue)
            {
                instGen(INS_lock);
                Emitter.emitIns_AR_R(ins, size, data.RegNum, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
            else
            {
                // CMPXCHG retries keep the address live across the backedge and return the old value in RAX.
                _gcInfo.gcMarkRegPtrVal(addr.RegNum, addr.Type);
                var tmpReg = InternalRegisters.GetSingle(node);
                Emitter.emitIns_R_AR(INS_mov, size, TargetRuntimeMetadata.IntegerReturnRegister, addr.RegNum, 0);
                var loop = genCreateTempLabel();
                genDefineTempLabel(loop);
                _ = Emitter.emitIns_Mov(INS_mov, size, tmpReg,
                    TargetRuntimeMetadata.IntegerReturnRegister, canSkip: false);
                Emitter.emitIns_R_R(ins, size, tmpReg, data.RegNum);
                instGen(INS_lock);
                Emitter.emitIns_AR_R(INS_cmpxchg, size, tmpReg, addr.RegNum, 0);
                inst_JMP(EJ_jne, loop);

                _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(addr.RegNum, addr.RegNum.SingleTypeMask));
                inst_Mov(node.Type, node.RegNum, TargetRuntimeMetadata.IntegerReturnRegister, canSkip: true);
                genProduceReg(node);
            }
            return;
        }

        assert((node.RegNum != addr.RegNum) || (node.RegNum == data.RegNum));
        _ = Emitter.emitIns_Mov(INS_mov, size, node.RegNum, data.RegNum, canSkip: true);
        var instruction = (node.Oper is GT_XADD) ? INS_xadd : INS_xchg;
        if (instruction != INS_xchg)
        {
            instGen(INS_lock);
        }
        Emitter.emitIns_AR_R(instruction, size, node.RegNum, addr.RegNum, 0);

        if (varTypeIsSmall(node.Type))
        {
            var mov = varTypeIsSigned(node.Type) ? INS_movsx : INS_movzx;
            _ = Emitter.emitIns_Mov(mov, size, node.RegNum, node.RegNum, canSkip: false);
        }
        genProduceReg(node);
    }

    public void genCodeForCmpXchg(GenTreeCmpXchg tree)
    {
        if (Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "unimplemented on LOONGARCH64 yet");
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            assert(tree.Oper is GT_CMPXCHG);
            assert(!varTypeIsSmall(tree.Type));

            var locOp = tree.Addr;
            var valOp = tree.Data;
            var comparandOp = tree.Comparand;

            var target = tree.RegNum;
            var loc = locOp.RegNum;
            var val = !valOp.IsContained ? valOp.RegNum : TargetRuntimeMetadata.ZeroRegister;
            var riscvComparand = TargetRuntimeMetadata.ZeroRegister;
            if (!comparandOp.IsContained)
            {
                riscvComparand = comparandOp.RegNum;
                if (comparandOp.Type is TYP_INT or TYP_UINT)
                {
                    var signExtendedComparand = InternalRegisters.Extract(tree);
                    Emitter.emitIns_R_R(INS_sext_w, EA_4BYTE, signExtendedComparand, riscvComparand);
                    riscvComparand = signExtendedComparand;
                }
            }
            var storeErr = InternalRegisters.GetSingle(tree);

            // Register allocation extends all input and internal register lifetimes; the registers must be distinct.
            noway_assert(target != loc);
            noway_assert(target != val);
            noway_assert(target != riscvComparand);
            noway_assert(target != storeErr);
            noway_assert(loc != val);
            noway_assert(loc != riscvComparand);
            noway_assert(loc != storeErr);
            noway_assert((val != riscvComparand) || (val == TargetRuntimeMetadata.ZeroRegister));
            noway_assert(val != storeErr);
            noway_assert(riscvComparand != storeErr);
            noway_assert(target != REG_NA);
            noway_assert(storeErr != REG_NA);

            genConsumeAddress(locOp);
            genConsumeRegs(valOp);
            genConsumeRegs(comparandOp);

            // genConsumeAddress marks the location non-pointer at first use, but this operation reuses it across the retry loop.
            _gcInfo.gcMarkRegPtrVal(loc, locOp.Type);

            var retry = genCreateTempLabel();
            var fail = genCreateTempLabel();
            var riscvSize = valOp.Type.EmitActualSize;
            var is4 = riscvSize == EA_4BYTE;

            genDefineTempLabel(retry);
            // Load the current value, fail on a mismatch, and retry only if the store-conditional fails.
            Emitter.emitIns_R_R_R(is4 ? INS_lr_w : INS_lr_d, riscvSize, target, loc, TargetRuntimeMetadata.Register0);
            Emitter.emitIns_J_cond_la(INS_bne, fail, target, riscvComparand);
            Emitter.emitIns_R_R_R(is4 ? INS_sc_w : INS_sc_d, riscvSize, storeErr, loc, val);
            Emitter.emitIns_J_cond_la(INS_bnez, retry, storeErr);
            genDefineTempLabel(fail);

            _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(loc, loc.SingleTypeMask));
            genProduceReg(tree);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            assert(tree.Oper is GT_CMPXCHG);

            var arm64TargetType = tree.Type;
            var arm64TargetReg = tree.RegNum;
            var address = tree.Addr;
            var data = tree.Data;
            var arm64Comparand = tree.Comparand;
            var dataReg = data.RegNum;
            var addressReg = address.RegNum;
            var comparandReg = arm64Comparand.RegNum;
            var dataSize = data.Type.EmitActualSize;

            genConsumeAddress(address);
            genConsumeRegs(data);
            genConsumeRegs(arm64Comparand);

            if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Atomics))
            {
                // CASAL uses the result register for the expected value and replaces it with the observed value.
                _ = Emitter.emitIns_Mov(INS_mov, dataSize, arm64TargetReg, comparandReg, canSkip: true);

                noway_assert((addressReg != arm64TargetReg) || (arm64TargetReg == comparandReg));
                noway_assert((dataReg != arm64TargetReg) || (arm64TargetReg == comparandReg));

                var ins = INS_casal;
                if (varTypeIsByte(arm64TargetType))
                {
                    ins = INS_casalb;
                }
                else if (varTypeIsShort(arm64TargetType))
                {
                    ins = INS_casalh;
                }

                Emitter.emitIns_R_R_R(ins, dataSize, arm64TargetReg, dataReg, addressReg);
            }
            else
            {
                var exResultReg = InternalRegisters.Extract(tree, new regMaskTP(SRBM_ALLINT));

                noway_assert(addressReg != arm64TargetReg);
                noway_assert(dataReg != arm64TargetReg);
                noway_assert(comparandReg != arm64TargetReg);
                noway_assert(addressReg != dataReg);
                noway_assert(arm64TargetReg != REG_NA);
                noway_assert(exResultReg != REG_NA);
                noway_assert(exResultReg != arm64TargetReg);

                assert(address.IsUsedFromReg);
                assert(data.IsUsedFromReg);
                assert(!arm64Comparand.IsUsedFromMemory);

                noway_assert(exResultReg != dataReg);
                noway_assert(exResultReg != addressReg);

                // The retry loop reuses the address after genConsumeAddress clears its pointer state.
                _gcInfo.gcMarkRegPtrVal(addressReg, address.Type);

                var labelRetry = genCreateTempLabel();
                var labelCompareFail = genCreateTempLabel();
                genDefineTempLabel(labelRetry);

                var insLd = INS_ldaxr;
                var insSt = INS_stlxr;
                if (varTypeIsByte(arm64TargetType))
                {
                    insLd = INS_ldaxrb;
                    insSt = INS_stlxrb;
                }
                else if (varTypeIsShort(arm64TargetType))
                {
                    insLd = INS_ldaxrh;
                    insSt = INS_stlxrh;
                }

                Emitter.emitIns_R_R(insLd, dataSize, arm64TargetReg, addressReg);

                if (arm64Comparand.IsContainedIntOrIImmed)
                {
                    if (arm64Comparand.IsIntegralConst(0))
                    {
                        Emitter.emitIns_J_R(INS_cbnz, arm64TargetType.EmitActualSize, labelCompareFail, arm64TargetReg);
                    }
                    else
                    {
                        Emitter.emitIns_R_I(INS_cmp, arm64TargetType.EmitActualSize, arm64TargetReg,
                            arm64Comparand.AsIntConCommon().IconValue);
                        Emitter.emitIns_J(INS_bne, labelCompareFail);
                    }
                }
                else
                {
                    Emitter.emitIns_R_R(INS_cmp, arm64TargetType.EmitActualSize, arm64TargetReg, comparandReg);
                    Emitter.emitIns_J(INS_bne, labelCompareFail);
                }

                Emitter.emitIns_R_R_R(insSt, dataSize, exResultReg, dataReg, addressReg);
                Emitter.emitIns_J_R(INS_cbnz, EA_4BYTE, labelRetry, exResultReg);
                genDefineTempLabel(labelCompareFail);

                // The exclusive pair has acquire/release ordering; this barrier also covers the compare-failure path.
                instGen_MemoryBarrier(BARRIER_FULL);
                _gcInfo.gcMarkRegSetNpt(address.RegMask);
            }

            if (varTypeIsSmall(arm64TargetType) && varTypeIsSigned(arm64TargetType))
            {
                var mov = varTypeIsShort(arm64TargetType) ? INS_sxth : INS_sxtb;
                _ = Emitter.emitIns_Mov(mov, EA_4BYTE, arm64TargetReg, arm64TargetReg, canSkip: false);
            }

            genProduceReg(tree);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Compare-exchange generation outside xarch is not implemented.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        assert(tree.Oper is GT_CMPXCHG);
        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        var size = tree.Type.EmitSize;
        var location = tree.Addr;
        var value = tree.Data;
        var comparand = tree.Comparand;
        var integerReturnRegister = TargetRuntimeMetadata.IntegerReturnRegister;
        assert(location.RegNum != REG_NA && location.RegNum != integerReturnRegister);
        assert(value.RegNum != REG_NA && value.RegNum != integerReturnRegister);

        _ = genConsumeReg(location);
        _ = genConsumeReg(value);
        _ = genConsumeReg(comparand);
        // Consume all operands first: a GT_COPY may still need the original RAX.
        inst_Mov(comparand.Type, integerReturnRegister, comparand.RegNum, canSkip: true);
        instGen(INS_lock);
        Emitter.emitIns_AR_R(INS_cmpxchg, size, value.RegNum, location.RegNum, 0);

        if (varTypeIsSmall(targetType))
        {
            var mov = varTypeIsSigned(targetType) ? INS_movsx : INS_movzx;
            _ = Emitter.emitIns_Mov(mov, size, targetReg, integerReturnRegister, canSkip: false);
        }
        else
        {
            inst_Mov(targetType, targetReg, integerReturnRegister, canSkip: true);
        }
        genProduceReg(tree);
    }

    public void instGen_MemoryBarrier(BarrierKind barrierKind)
    {
        if (Configuration.TARGET_ARM64)
        {
            if (Configuration.DEBUG && (JitConfig.JitNoMemoryBarriers == 1))
            {
                return;
            }
            if (barrierKind == BARRIER_STORE_ONLY)
            {
                // ISHST is too weak because loads must not move across a store barrier.
                barrierKind = BARRIER_FULL;
            }

            var barrier = barrierKind == BARRIER_LOAD_ONLY ? INS_BARRIER_ISHLD : INS_BARRIER_ISH;
            if (!_compiler.opts.OptimizationEnabled ||
                !Emitter.emitTryCoalesceLastMemBarrier(barrier))
            {
                Emitter.emitIns_BARR(INS_dmb, barrier);
            }
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            if (Configuration.DEBUG && (JitConfig.JitNoMemoryBarriers == 1))
            {
                return;
            }
            if (!_compiler.opts.OptimizationEnabled ||
                !Emitter.emitTryCoalesceLastMemBarrier(INS_BARRIER_SY))
            {
                Emitter.emitIns_I(INS_dmb, EA_4BYTE, unchecked((nint)(uint)INS_BARRIER_SY));
            }
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (Configuration.DEBUG && (JitConfig.JitNoMemoryBarriers == 1))
            {
                return;
            }
            var barrier = barrierKind switch {
                BARRIER_LOAD_ONLY => INS_BARRIER_LOAD_ONLY,
                BARRIER_STORE_ONLY => INS_BARRIER_STORE_ONLY,
                _ => INS_BARRIER_FULL,
            };
            Emitter.emitIns_I(INS_fence, EA_4BYTE, unchecked((nint)(uint)barrier));
            return;
        }

        if (Configuration.TARGET_LOONGARCH64 || !Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Memory barrier generation outside supported targets is not implemented.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        if (Configuration.DEBUG && (JitConfig.JitNoMemoryBarriers == 1))
        {
            return;
        }
        // Load-only and store-only barriers require no instructions on xarch.
        if (barrierKind == BARRIER_FULL)
        {
            instGen(INS_lock);
            Emitter.emitIns_I_AR(INS_or, EA_4BYTE, 0, TargetRuntimeMetadata.StackPointerBaseRegister, 0, INS_OPTS_EVEX_NoApxPromotion);
        }
    }
}
