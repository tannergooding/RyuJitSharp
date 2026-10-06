// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genFnEpilogArmArch(BasicBlock block)
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
        var lastNode = block.GetLastNode() ??
            throw new FatalJitException(CORJIT_INTERNALERROR, "Missing epilog node.");
        CORINFO_METHOD_HANDLE methHnd = null;
        CORINFO_CONST_LOOKUP addrInfo = default;
        addrInfo.accessType = IAT_VALUE;

        if (jmpEpilog && lastNode.OperIs(GT_JMP))
        {
            methHnd = (CORINFO_METHOD_HANDLE)lastNode.AsVal().Val1;
            _compiler.info.compCompHnd->getFunctionEntryPoint(methHnd, &addrInfo);
        }

#if TARGET_ARM
        // Keep large-frame immediate materialization outside unwind codes until an
        // instruction that changes the frame is emitted.
        var unwindStarted = false;

        if (_compiler.compLocallocUsed)
        {
            _compiler.unwindBegEpilog();
            unwindStarted = true;
            inst_Mov(TYP_I_IMPL, REG_SP, REG_SAVED_LOCALLOC_SP, canSkip: false);
            _compiler.unwindSetFrameReg(REG_SAVED_LOCALLOC_SP, 0);
        }

        if (jmpEpilog ||
            genStackAllocRegisterMask(unchecked((uint)_compiler.compLclFrameSize),
                _regSet.rsGetModifiedFltCalleeSavedRegsMask()) == RBM_NONE)
        {
            genFreeLclFrame(unchecked((uint)_compiler.compLclFrameSize), ref unwindStarted);
        }

        if (!unwindStarted)
        {
            _compiler.unwindBegEpilog();
            unwindStarted = true;
        }

        if (jmpEpilog && lastNode.OperIs(GT_JMP) && addrInfo.accessType == IAT_RELPVALUE)
        {
            // Use LR as the relative-address base before the epilog restores its saved value.
            var indCallReg = REG_R12;
            var vptrReg1 = REG_LR;

            instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, indCallReg, (nint)addrInfo.addr);
            _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, vptrReg1, indCallReg, canSkip: false);
            Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, indCallReg, indCallReg, 0, INS_FLAGS_DONT_CARE);
            Emitter.emitIns_R_R(INS_add, EA_PTRSIZE, indCallReg, vptrReg1);
        }

        genPopCalleeSavedRegisters(jmpEpilog);

        var preSpillRegs = _regSet.rsMaskPreSpillRegs(true);
        if (preSpillRegs.IsNonEmpty)
        {
            noway_assert(!genUsedPopToReturn);
            var preSpillRegArgSize = unchecked((int)genCountBits(preSpillRegs)) * REGSIZE_BYTES;
            inst_RV_IV(INS_add, REG_SPBASE, preSpillRegArgSize, EA_PTRSIZE);
            _compiler.unwindAllocStack(unchecked((uint)preSpillRegArgSize));
        }

        if (jmpEpilog)
        {
            noway_assert(!genUsedPopToReturn);
        }
#else
        _compiler.unwindBegEpilog();
        genPopCalleeSavedRegistersAndFreeLclFrame(jmpEpilog);
#endif

        if (jmpEpilog)
        {
            HasTailCalls = true;
            noway_assert(block.Kind is BBJ_RETURN);
            noway_assert(block.FirstNode is not null);

#if !FEATURE_FASTTAILCALL
            noway_assert(lastNode.OperIs(GT_JMP));
#else
            noway_assert(!lastNode.OperIs(GT_JMP) || (lastNode.Next is null));
            noway_assert(lastNode.OperIs(GT_JMP) ||
                (lastNode.OperIs(GT_CALL) && lastNode.AsCall().IsFastTailCall));

            if (lastNode.OperIs(GT_JMP))
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
                        if (validImmForBL((nint)addrInfo.addr))
                        {
                            parameters.callType = EC_FUNC_TOKEN;
                            parameters.addr = addrInfo.addr;
                            break;
                        }

                        goto case IAT_PVALUE;
                    }

                    case IAT_PVALUE:
                    {
                        parameters.callType = EC_INDIR_R;
#if TARGET_ARM
                        parameters.ireg = REG_R12;
#else
                        parameters.ireg = REG_INDIRECT_CALL_TARGET_REG;
#endif
                        instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, parameters.ireg, (nint)addrInfo.addr);
                        if (addrInfo.accessType == IAT_PVALUE)
                        {
#if TARGET_ARM
                            Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, parameters.ireg, parameters.ireg, 0,
                                INS_FLAGS_DONT_CARE);
#else
                            Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, parameters.ireg, parameters.ireg, 0);
#endif
                            _regSet.verifyRegUsed(parameters.ireg);
                        }
                        break;
                    }

                    case IAT_RELPVALUE:
                    {
                        parameters.callType = EC_INDIR_R;
                        parameters.ireg = REG_R12;
                        _regSet.verifyRegUsed(parameters.ireg);
                        break;
                    }

                    default:
                    {
                        throw new FatalJitException(CORJIT_INTERNALERROR, "Unsupported JMP indirection.");
                    }
                }

                parameters.isJump = true;
                genEmitCallWithCurrentGC(ref parameters);
            }
#if FEATURE_FASTTAILCALL
            else
            {
                genCallInstruction(lastNode.AsCall());
            }
#endif
        }
        else
        {
#if TARGET_ARM
            if (!genUsedPopToReturn)
            {
                inst_RV(INS_bx, REG_LR, TYP_I_IMPL);
                _compiler.unwindBranch16();
            }
#else
            inst_RV(INS_ret, REG_LR, TYP_I_IMPL);
            _compiler.unwindReturn(REG_LR);
#endif
        }

        _compiler.unwindEndEpilog();
    }
}
#endif
