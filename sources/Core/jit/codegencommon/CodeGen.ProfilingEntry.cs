// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if PROFILING_SUPPORTED
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genProfilingEnterCallback(regNumber initReg, ref bool initRegZeroed)
    {
#if TARGET_ARM64
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        const regNumber profilerFuncIdReg = REG_R10;
        const regNumber profilerCallerSpReg = REG_R11;
        if (_compiler.compProfilerMethHndIndirected)
        {
            instGen_Set_Reg_To_Imm(EA_PTR_DSP_RELOC, profilerFuncIdReg,
                unchecked((nint)_compiler.compProfilerMethHnd));
            Emitter.emitIns_R_R(INS_ldr, EA_PTRSIZE, profilerFuncIdReg, profilerFuncIdReg);
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, profilerFuncIdReg,
                unchecked((nint)_compiler.compProfilerMethHnd));
        }

        var callerSPOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
        genInstrWithConstant(INS_add, EA_PTRSIZE, profilerCallerSpReg, genFramePointerReg(),
            unchecked(-(nint)callerSPOffset), profilerCallerSpReg);

        genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN);

        var initRegMask = genRegMask(initReg);
        var profilerClobberMask = _compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_ENTER)
            | genRegMask(profilerFuncIdReg)
            | genRegMask(profilerCallerSpReg);
        if ((profilerClobberMask & initRegMask).IsNonEmpty)
        {
            initRegZeroed = false;
        }
#elif TARGET_ARM
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        var argReg = REG_PROFILER_ENTER_ARG;
        var argRegMask = genRegMask(argReg);
        assert((_regSet.rsMaskPreSpillRegArg & argRegMask).IsNonEmpty);

        if (_compiler.compProfilerMethHndIndirected)
        {
            Emitter.emitIns_R_AI(INS_ldr, EA_PTR_DSP_RELOC, argReg,
                unchecked((nint)_compiler.compProfilerMethHnd));
            _regSet.verifyRegUsed(argReg);
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_4BYTE, argReg, unchecked((nint)_compiler.compProfilerMethHnd));
        }

        genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN);

        var profilerClobberMask = new regMaskTP(SRBM_PROFILER_ENTER_TRASH | SRBM_PROFILER_ENTER_ARG);
        if ((profilerClobberMask & genRegMask(initReg)).IsNonEmpty)
        {
            initRegZeroed = false;
        }
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Profiler enter callbacks require xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        // Give the profiler a chance to back out of hooking this method.
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

#if TARGET_X86
        // The x86 helper consumes its pushed handle and preserves all registers.
        // Keep the caller's tracked stack level unchanged across the naked helper.
        var savedStackLevel = genStackLevel;
#if UNIX_X86_ABI
        Emitter.emitIns_R_I(INS_sub, EA_4BYTE, REG_SPBASE, 0xC);
#endif
        if (_compiler.compProfilerMethHndIndirected)
        {
            Emitter.emitIns_AR_R(INS_push, EA_PTRSIZE | EA_DSP_RELOC_FLG, REG_NA, REG_NA,
                unchecked((nint)_compiler.compProfilerMethHnd));
        }
        else
        {
            inst_IV(INS_push, unchecked((nint)_compiler.compProfilerMethHnd));
        }

        genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN);
#if UNIX_X86_ABI
        Emitter.emitIns_R_I(INS_add, EA_4BYTE, REG_SPBASE, 0x10);
#endif
        SetStackLevel(savedStackLevel);
#else
#if WINDOWS_AMD64_ABI
        // Windows requires homing and reloading incoming register arguments around the callback.
        noway_assert(_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 4 * REGSIZE_BYTES);

        // The prolog is non-GC interruptible. The profiler must not trigger GC while examining
        // the incoming arguments, which may contain object references.
        if (!_compiler.info.compIsVarArgs)
        {
            for (var varNum = 0; varNum < _compiler.info.compArgsCount; varNum++)
            {
                ref var local = ref _compiler.lvaGetDesc(varNum);
                noway_assert(local.lvIsParam);

                ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(varNum);
                if (!abiInfo.HasExactlyOneRegisterSegment)
                {
                    assert(!abiInfo.HasAnyRegisterSegment);
                    continue;
                }

                var storeType = local.GetRegisterType();
                var argReg = abiInfo.Segments[0].Register;
                var storeIns = ins_Store(storeType);

#if FEATURE_SIMD
                if ((storeType == TYP_SIMD8) && genIsValidIntReg(argReg))
                {
                    storeIns = INS_mov;
                }
#endif
                Emitter.emitIns_S_R(storeIns, storeType.EmitSize, argReg, varNum, 0);
            }
        }
#endif

#if UNIX_AMD64_ABI
        const regNumber profilerArg0 = REG_PROFILER_ENTER_ARG_0;
        const regNumber profilerArg1 = REG_PROFILER_ENTER_ARG_1;
#else
        const regNumber profilerArg0 = REG_ARG_0;
        const regNumber profilerArg1 = REG_ARG_1;
#endif
        if (_compiler.compProfilerMethHndIndirected)
        {
            // AOT profiler handles are accessed through a relocated pointer.
            Emitter.emitIns_R_AI(INS_mov, EA_PTRSIZE | EA_DSP_RELOC_FLG, profilerArg0,
                unchecked((nint)_compiler.compProfilerMethHnd));
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_8BYTE, profilerArg0, unchecked((nint)_compiler.compProfilerMethHnd));
        }

        noway_assert(_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
        // The final frame layout makes the caller's SP offset available here. Its offset
        // relative to the frame pointer is negative, so negate it to form the address.
        var callerSPOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
        Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, profilerArg1, genFramePointerReg(), -callerSPOffset);

#if UNIX_AMD64_ABI
        genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN, REG_DEFAULT_PROFILER_CALL_TARGET);
#else
        genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN);

        // TODO-AMD64-CQ: Consider combining this reload with prolog argument placement,
        // as computed by LSRA (genFnPrologCalleeRegArgs and genEnregisterIncomingStackArgs).
        // Varargs have already been homed; only their known register arguments are reloaded.
        // Floating arguments must also be restored to their corresponding integer registers.
        for (var varNum = 0; varNum < _compiler.info.compArgsCount; varNum++)
        {
            ref var local = ref _compiler.lvaGetDesc(varNum);
            noway_assert(local.lvIsParam);

            ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(varNum);
            if (!abiInfo.HasExactlyOneRegisterSegment)
            {
                assert(!abiInfo.HasAnyRegisterSegment);
                continue;
            }

            var loadType = local.GetRegisterType();
            var argReg = abiInfo.Segments[0].Register;
            var loadIns = ins_Load(loadType);

#if FEATURE_SIMD
            if ((loadType == TYP_SIMD8) && genIsValidIntReg(argReg))
            {
                loadIns = INS_mov;
            }
#endif
            Emitter.emitIns_R_S(loadIns, loadType.EmitSize, argReg, varNum, 0);

            if (compFeatureVarArg() && _compiler.info.compIsVarArgs && varTypeIsFloating(loadType))
            {
                var intArgReg = _compiler.getCallArgIntRegister(argReg);
                inst_Mov(TYP_LONG, intArgReg, argReg, canSkip: false, size: loadType.EmitActualSize);
            }
        }
#endif

        // The helper call or its argument setup may have overwritten the prolog's zero register.
        var initMask = regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask);
        if ((_compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_ENTER) & initMask).IsNonEmpty
            || (initMask & regMaskTP.CreateFromRegNum(profilerArg0, profilerArg0.SingleTypeMask)).IsNonEmpty
            || (initMask & regMaskTP.CreateFromRegNum(profilerArg1, profilerArg1.SingleTypeMask)).IsNonEmpty)
        {
            initRegZeroed = false;
        }
#endif
#endif
    }
}
#endif
