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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Profiler enter callbacks require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        // Give the profiler a chance to back out of hooking this method.
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        // The callback uses the outgoing argument area, and the incoming argument registers
        // must be preserved in their homes across the call.
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

        // RCX = ProfilerMethHnd, RDX = caller's SP.
        if (_compiler.compProfilerMethHndIndirected)
        {
            // AOT profiler handles are accessed through a relocated pointer.
            Emitter.emitIns_R_AI(INS_mov, EA_PTRSIZE | EA_DSP_RELOC_FLG, REG_ARG_0,
                unchecked((nint)_compiler.compProfilerMethHnd));
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_8BYTE, REG_ARG_0, unchecked((nint)_compiler.compProfilerMethHnd));
        }

        noway_assert(_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
        // The final frame layout makes the caller's SP offset available here. Its offset
        // relative to the frame pointer is negative, so negate it to form the address.
        var callerSPOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
        Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_ARG_1, genFramePointerReg(), -callerSPOffset);

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

        // The helper call or its argument setup may have overwritten the prolog's zero register.
        var initMask = regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask);
        if ((_compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_ENTER) & initMask).IsNonEmpty
            || (initMask & new regMaskTP(SRBM_ARG_0 | SRBM_ARG_1)).IsNonEmpty)
        {
            initRegZeroed = false;
        }
#endif
    }
}
#endif
