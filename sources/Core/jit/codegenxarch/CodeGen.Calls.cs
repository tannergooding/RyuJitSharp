// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCall(GenTreeCall call)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Call generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        genAlignStackBeforeCall(call);
        genCallPlaceRegArgs(call);

        if (call.NeedsNullCheck)
        {
            var regThis = genGetThisArgReg(call);
            Emitter.emitIns_AR_R(INS_cmp, EA_4BYTE, regThis, regThis, 0);
        }

        if (call.IsFastTailCall)
        {
            var target = getCallTarget(call, out _);
            if (target is not null)
            {
                if (target.IsContainedIndir)
                {
                    var indir = target.AsIndir();
                    genConsumeAddress(indir.Addr);

                    // Consumption kills these roots, but the address remains live through the epilog.
                    if (indir.HasBase && varTypeIsGC(indir.Base.Type))
                    {
                        GCInfo.gcMarkRegPtrVal(indir.Base.RegNum, indir.Base.Type);
                    }
                    if (indir.HasIndex && varTypeIsGC(indir.Index.Type))
                    {
                        GCInfo.gcMarkRegPtrVal(indir.Index.RegNum, indir.Index.Type);
                    }
                }
                else
                {
                    assert(!target.IsContained);
                    _ = genConsumeReg(target);
                }
            }

            return;
        }

        // P/Invoke needs a GC-state boundary before the call, not lazy killing at the callsite.
        if (_compiler.killGCRefs(call))
        {
            genDefineTempLabel(genCreateTempLabel());
        }

        if (Emitter.Contains256BitOrMoreAvxInstruction && call.NeedsVzeroupper(_compiler))
        {
            // A single prolog vzeroupper is insufficient when this method also uses wide vectors.
            instGen(INS_vzeroupper);
        }

        genCallInstruction(call);
        genDefinePendingCallLabel(call);

#if DEBUG
        var killMask = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH);
#if FEATURE_MASKED_HW_INTRINSICS
        killMask |= regMaskTP.CreateFromRegNum(REG_MASK_FIRST, SRBM_MSK_CALLEE_TRASH);
#endif
        if (call.IsHelperCall())
        {
            killMask = _compiler.compHelperCallKillSet(call.HelperNum);
        }
        assert((GCInfo.gcRegGCrefSetCur & killMask).IsEmpty);
        assert((GCInfo.gcRegByrefSetCur & killMask).IsEmpty);
#endif
        var returnType = call.Type;
        if (returnType != TYP_VOID)
        {
            regNumber returnReg;
            if (call.HasMultiRegRetVal)
            {
                ref readonly var retTypeDesc = ref call.ReturnTypeDesc;
                var regCount = retTypeDesc.ReturnRegCount;
                for (byte i = 0; i < regCount; i++)
                {
                    var regType = retTypeDesc.GetReturnRegType(i);
                    returnReg = retTypeDesc.GetAbiReturnReg(i, call.UnmanagedCallConv);
                    var allocatedReg = call.GetRegNumByIdx(i);
                    inst_Mov(regType, allocatedReg, returnReg, canSkip: true);
                }

#if FEATURE_SIMD
                if (call.IsUnmanaged && (returnType == TYP_SIMD12))
                {
                    returnReg = retTypeDesc.GetAbiReturnReg(1, call.UnmanagedCallConv);
                    genSimd12UpperClear(returnReg);
                }
#endif
            }
            else
            {
                returnReg = varTypeIsFloating(returnType) ? REG_FLOATRET : REG_INTRET;
                inst_Mov(returnType, call.RegNum, returnReg, canSkip: true);
            }

            genProduceReg(call);
        }

        // Keep unused return values visible to the debugger in minopts/debuggable methods.
        if ((call.Next is null) && _compiler.opts.OptimizationEnabled)
        {
            GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_INTRET));
        }

        genRemoveAlignmentAfterCall(call);
#endif
    }

    private static regNumber genGetThisArgReg(GenTreeCall call) => REG_ARG_0;

    private static void genAlignStackBeforeCall(GenTreeCall call)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Call stack alignment requires Windows AMD64.");
#endif
        // Native per-call alignment is Unix-x86-only; Windows AMD64 uses the fixed outgoing area.
    }

    private static void genRemoveAlignmentAfterCall(GenTreeCall call, uint bias = 0)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Call stack realignment requires Windows AMD64.");
#else
        assert(bias == 0);
#endif
    }

    public unsafe void genDefinePendingCallLabel(GenTreeCall call)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Pending call labels require Windows AMD64.");
#else
        if (genPendingCallLabel is null)
        {
            return;
        }

        if (call.IsHelperCall())
        {
            switch (call.HelperNum)
            {
                case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
                case CORINFO_HELP_VIRTUAL_FUNC_PTR:
                case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
                case CORINFO_HELP_MEMSET:
                case CORINFO_HELP_MEMCPY:
                {
                    return;
                }

                default:
                {
                    break;
                }
            }
        }

        genDefineInlineTempLabel(genPendingCallLabel);
        genPendingCallLabel = null;
#endif
    }

#if FEATURE_SIMD
    public void genSimd12UpperClear(regNumber targetReg)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 upper clearing requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(genIsValidFloatReg(targetReg));

        // INSERTPS selects element 3 as source/destination (0xF0), then zeros that lane (0x08).
        Emitter.emitIns_SIMD_R_R_R_I(INS_insertps, EA_16BYTE, targetReg, targetReg, targetReg,
            unchecked((sbyte)0xF8), INS_OPTS_NONE);
#endif
    }
#endif
}
