// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genFnEpilogRiscV(BasicBlock block)
    {
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFnEpilog()\n");
        }
#endif

        VarSetOps.Assign(_compiler, ref GCInfo.gcVarPtrSetCur, Emitter.InitGCrefVars);
        GCInfo.gcRegGCrefSetCur = Emitter.InitGCrefRegs;
        GCInfo.gcRegByrefSetCur = Emitter.InitByrefRegs;

#if DEBUG
        if (_compiler.opts.dspCode)
        {
            jitprintf("\n__epilog:\n");
        }

        if (_verbose)
        {
            jitprintf($"gcVarPtrSetCur={VarSetOps.ToString(_compiler, GCInfo.gcVarPtrSetCur)} ");
            dumpConvertedVarSet(_compiler, GCInfo.gcVarPtrSetCur);
            jitprintf(", gcRegGCrefSetCur=");
            printRegMaskInt(GCInfo.gcRegGCrefSetCur);
            Emitter.emitDispRegSet(GCInfo.gcRegGCrefSetCur);
            jitprintf(", gcRegByrefSetCur=");
            printRegMaskInt(GCInfo.gcRegByrefSetCur);
            Emitter.emitDispRegSet(GCInfo.gcRegByrefSetCur);
            jitprintf("\n");
        }
#endif

        var jmpEpilog = block.HasFlag(BBF_HAS_JMP);
        var lastNode = block.GetLastNode()
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Missing epilog node.");

        CORINFO_METHOD_HANDLE methHnd = null;
        CORINFO_CONST_LOOKUP addrInfo = default;
        addrInfo.accessType = IAT_VALUE;
        if (jmpEpilog && lastNode.OperIs(GT_JMP))
        {
            methHnd = (CORINFO_METHOD_HANDLE)lastNode.AsVal().Val1;
            _compiler.info.compCompHnd->getFunctionEntryPoint(methHnd, &addrInfo);
        }

        _compiler.unwindBegEpilog();
        if (jmpEpilog)
        {
            HasTailCalls = true;
            noway_assert(block.Kind is BBJ_RETURN);
            noway_assert(block.FirstNode is not null);

            var jmpNode = lastNode;
#if !FEATURE_FASTTAILCALL
            noway_assert(jmpNode.OperIs(GT_JMP));
#else
            noway_assert(!jmpNode.OperIs(GT_JMP) || (jmpNode.Next is null));
            noway_assert(jmpNode.OperIs(GT_JMP) ||
                (jmpNode.OperIs(GT_CALL) && jmpNode.AsCall().IsFastTailCall));
            if (jmpNode.OperIs(GT_JMP))
#endif
            {
                noway_assert(methHnd != null);
                noway_assert(addrInfo.addr != null);

                var parameters = new EmitCallParams
                {
                    methHnd = methHnd,
                };

                switch (addrInfo.accessType)
                {
                    case IAT_VALUE:
                    {
                        parameters.ireg = REG_INDIRECT_CALL_TARGET_REG;
                        parameters.callType = EC_FUNC_TOKEN;
                        parameters.addr = addrInfo.addr;
                        break;
                    }
                    case IAT_PVALUE:
                    {
                        parameters.callType = EC_INDIR_R;
                        parameters.ireg = REG_INDIRECT_CALL_TARGET_REG;
                        Emitter.emitIns_R_R_Addr(
                            INS_ld, EA_PTRSIZE, parameters.ireg, parameters.ireg, addrInfo.addr);
                        _regSet.verifyRegUsed(parameters.ireg);
                        break;
                    }
                    case IAT_RELPVALUE:
                    {
                        parameters.callType = EC_INDIR_R;
                        // Keep the target in a volatile temporary outside the argument registers.
                        parameters.ireg = REG_T2;
                        _regSet.verifyRegUsed(parameters.ireg);
                        break;
                    }
                    default:
                    {
                        throw new FatalJitException(CORJIT_INTERNALERROR, "Unsupported JMP indirection.");
                    }
                }

                genPopCalleeSavedRegisters(jmpEpilog: true);

                parameters.isJump = true;
                genEmitCallWithCurrentGC(ref parameters);
            }
#if FEATURE_FASTTAILCALL
            else
            {
                genPopCalleeSavedRegisters(jmpEpilog: true);
                genCallInstruction(jmpNode.AsCall());
            }
#endif
        }
        else
        {
            genPopCalleeSavedRegisters(jmpEpilog: false);

            Emitter.emitIns_R_R_I(INS_jalr, EA_PTRSIZE, REG_R0, REG_RA, 0);
            _compiler.unwindReturn(REG_RA);
        }

        _compiler.unwindEndEpilog();
    }

    private unsafe void genPopCalleeSavedRegistersRiscV(bool jmpEpilog)
    {
        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

        var regsToRestoreMask = _regSet.rsGetModifiedCalleeSavedRegsMask();
        assert(IsFramePointerUsed);

        var totalFrameSize = genTotalFrameSize;
        var localFrameSize = _compiler.compLclFrameSize;
        if ((_compiler.lvaMonAcquired != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            localFrameSize -= TARGET_POINTER_SIZE;
        }

        JITDUMP(
            $"Frame type. #outsz={_compiler.lvaOutgoingArgSpaceSize.Value}; #framesz={totalFrameSize}; " +
            $"#calleeSaveRegsPushed:{_compiler.compCalleeRegsPushed}; localloc? {dspBool(_compiler.compLocallocUsed)}\n");

        var fpOffset = localFrameSize;
        var remainingSpSize = totalFrameSize;
        if (totalFrameSize <= 2040)
        {
            if (_compiler.compLocallocUsed)
            {
                var spToFpDelta = genSPtoFPdelta;
                // Restore SP from FP after localloc changed the stack pointer.
                Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, -spToFpDelta);
                _compiler.unwindSetFrameReg(REG_FPBASE, unchecked((uint)spToFpDelta));
            }
        }
        else
        {
            if (_compiler.compLocallocUsed)
            {
                var spToFpDelta = genSPtoFPdelta;
                // Restore SP from FP after localloc changed the stack pointer.
                if (Emitter.isValidSimm12(spToFpDelta))
                {
                    Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, -spToFpDelta);
                }
                else
                {
                    var tempReg = rsGetRsvdReg();
                    _ = Emitter.emitLoadImmediate(true, EA_PTRSIZE, tempReg, spToFpDelta);
                    Emitter.emitIns_R_R_R(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, tempReg);
                }
            }

            if ((localFrameSize + (_compiler.compCalleeRegsPushed << 3)) > 2040)
            {
                remainingSpSize = localFrameSize & -16;
                genStackPointerAdjustment(remainingSpSize, REG_RA, null, reportUnwindData: true);

                remainingSpSize = totalFrameSize - remainingSpSize;
                fpOffset = localFrameSize & 0xf;
            }
        }

        JITDUMP($"    calleeSaveSPOffset={fpOffset + 16}\n");
        genRestoreCalleeSavedRegistersHelp(regsToRestoreMask, REG_SPBASE, fpOffset + 16,
            reportUnwindData: true);

        Emitter.emitIns_R_R_I(INS_ld, EA_PTRSIZE, REG_RA, REG_SPBASE, fpOffset + 8);
        _compiler.unwindSaveReg(REG_RA, unchecked((uint)(fpOffset + 8)));

        Emitter.emitIns_R_R_I(INS_ld, EA_PTRSIZE, REG_FP, REG_SPBASE, fpOffset);
        _compiler.unwindSaveReg(REG_FP, unchecked((uint)fpOffset));

        if (Emitter.isValidUimm11(remainingSpSize))
        {
            Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, remainingSpSize);
        }
        else
        {
            var tempReg = rsGetRsvdReg();
            _ = Emitter.emitLoadImmediate(true, EA_PTRSIZE, tempReg, remainingSpSize);
            Emitter.emitIns_R_R_R(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, tempReg);
        }
        _compiler.unwindAllocStack(unchecked((uint)remainingSpSize));

        if (_compiler.opts.IsOSR)
        {
            var tier0FrameSize = _compiler.info.compPatchpointInfo->TotalFrameSize;
            JITDUMP($"Extra SP adjust for OSR to pop off Tier0 frame: {tier0FrameSize} bytes\n");

            // Remove the original Tier0 frame after restoring the current frame.
            if (Emitter.isValidUimm11(tier0FrameSize))
            {
                Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, tier0FrameSize);
            }
            else
            {
                var tempReg = rsGetRsvdReg();
                _ = Emitter.emitLoadImmediate(true, EA_PTRSIZE, tempReg, tier0FrameSize);
                Emitter.emitIns_R_R_R(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, tempReg);
            }
            _compiler.unwindAllocStack(unchecked((uint)tier0FrameSize));
        }
    }
}
#endif
