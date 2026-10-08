// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genFnEpilogLoongArch64(BasicBlock block)
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

        var jumpEpilog = block.HasFlag(BBF_HAS_JMP);
        var lastNode = block.GetLastNode()
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Missing epilog node.");

        CORINFO_METHOD_HANDLE methodHandle = null;
        CORINFO_CONST_LOOKUP addressInfo = default;
        addressInfo.accessType = IAT_VALUE;

        if (jumpEpilog && lastNode.OperIs(GT_JMP))
        {
            methodHandle = (CORINFO_METHOD_HANDLE)lastNode.AsVal().Val1;
            _compiler.info.compCompHnd->getFunctionEntryPoint(methodHandle, &addressInfo);
        }

        _compiler.unwindBegEpilog();
        genPopCalleeSavedRegistersLoongArch64();

        if (jumpEpilog)
        {
            HasTailCalls = true;

            noway_assert(block.Kind == BBJ_RETURN);
            noway_assert(block.FirstNode is not null);

            var jumpNode = lastNode;
#if !FEATURE_FASTTAILCALL
            noway_assert(jumpNode.OperIs(GT_JMP));
#else
            noway_assert(!jumpNode.OperIs(GT_JMP) || (jumpNode.Next is null));
            noway_assert(jumpNode.OperIs(GT_JMP) ||
                (jumpNode.OperIs(GT_CALL) && jumpNode.AsCall().IsFastTailCall));

            if (jumpNode.OperIs(GT_JMP))
#endif
            {
                assert(methodHandle is not null);
                assert(addressInfo.addr is not null);

                var parameters = new EmitCallParams { methHnd = methodHandle };
                switch (addressInfo.accessType)
                {
                    case IAT_VALUE:
                    case IAT_PVALUE:
                    {
                        parameters.callType = EC_INDIR_R;
                        parameters.ireg = REG_INDIRECT_CALL_TARGET_REG;
                        instGen_Set_Reg_To_Imm(
                            EA_HANDLE_CNS_RELOC, parameters.ireg, unchecked((nint)addressInfo.addr));
                        if (addressInfo.accessType == IAT_PVALUE)
                        {
                            Emitter.emitIns_R_R_I(INS_ld_d, EA_PTRSIZE, parameters.ireg, parameters.ireg, 0);
                            _regSet.verifyRegUsed(parameters.ireg);
                        }

                        break;
                    }

                    case IAT_RELPVALUE:
                    {
                        parameters.callType = EC_INDIR_R;
                        parameters.ireg = REG_T2;
                        _regSet.verifyRegUsed(parameters.ireg);
                        break;
                    }

                    case IAT_PPVALUE:
                    default:
                    {
                        noway_assert(false);
                        throw new FatalJitException(CORJIT_INTERNALERROR, "Unsupported JMP indirection.");
                    }
                }

                parameters.isJump = true;
                genEmitCallWithCurrentGC(ref parameters);
            }
#if FEATURE_FASTTAILCALL
            else
            {
                genCallInstructionLoongArch64(jumpNode.AsCall());
            }
#endif
        }
        else
        {
            Emitter.emitIns_R_R_I(INS_jirl, EA_PTRSIZE, REG_R0, REG_RA, 0);
            _compiler.unwindReturn(REG_RA);
        }

        _compiler.unwindEndEpilog();
    }
}
#endif
