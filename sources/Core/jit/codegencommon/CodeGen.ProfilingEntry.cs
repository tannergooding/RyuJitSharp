// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genProfilingEnterCallback(regNumber initReg, ref bool initRegZeroed)
    {
        if (Configuration.TARGET_ARM64)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());

            if (!_compiler.compIsProfilerHookNeeded)
            {
                return;
            }

            const regNumber profilerFuncIdReg = REG_ARM64_R10;
            const regNumber profilerCallerSpReg = REG_ARM64_R11;
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

            var arm64CallerSPOffset = _compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed);
            _ = genInstrWithConstant(INS_add, EA_PTRSIZE, profilerCallerSpReg, genFramePointerReg(),
                unchecked(-(nint)arm64CallerSPOffset), profilerCallerSpReg);

            genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN);

            var initRegMask = genRegMask(initReg);
            var profilerClobberMask = _compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_ENTER)
                | genRegMask(profilerFuncIdReg)
                | genRegMask(profilerCallerSpReg);
            if ((profilerClobberMask & initRegMask).IsNonEmpty)
            {
                initRegZeroed = false;
            }

            return;
        }

        if (Configuration.TARGET_ARM)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            if (!_compiler.compIsProfilerHookNeeded)
            {
                return;
            }

            var argReg = REG_ARM32_R0;
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

            var profilerClobberMask = new regMaskTP(SRBM_PROFILER_ENTER_TRASH_ARM | SRBM_ARM32_R0);
            if ((profilerClobberMask & genRegMask(initReg)).IsNonEmpty)
            {
                initRegZeroed = false;
            }

            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            if (!_compiler.compIsProfilerHookNeeded)
            {
                return;
            }

            var instruction = _compiler.compProfilerMethHndIndirected ? INS_ld : INS_addi;
            Emitter.emitIns_R_R_Addr(
                instruction,
                EA_PTRSIZE,
                REG_PROFILER_ENTER_ARG_FUNC_ID_RISCV64,
                REG_PROFILER_ENTER_ARG_FUNC_ID_RISCV64,
                _compiler.compProfilerMethHnd);

            var riscvCallerSPOffset = unchecked(-(nint)_compiler.lvaToCallerSPRelativeOffset(0, IsFramePointerUsed));
            _ = genInstrWithConstant(
                INS_addi,
                EA_PTRSIZE,
                REG_PROFILER_ENTER_ARG_CALLER_SP_RISCV64,
                genFramePointerReg(),
                riscvCallerSPOffset,
                REG_PROFILER_ENTER_ARG_CALLER_SP_RISCV64);

            genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN);

            var profilerClobberMask = new regMaskTP(
                SRBM_PROFILER_ENTER_TRASH_RISCV64 |
                SRBM_PROFILER_ENTER_ARG_FUNC_ID_RISCV64 |
                SRBM_PROFILER_ENTER_ARG_CALLER_SP_RISCV64);
            if ((profilerClobberMask & genRegMask(initReg)).IsNonEmpty)
            {
                initRegZeroed = false;
            }

            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            genProfilingEnterCallbackLoongArch64(initReg, ref initRegZeroed);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Profiler enter callbacks are unsupported for this target.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        // Give the profiler a chance to back out of hooking this method.
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return;
        }

        if (Configuration.TARGET_X86)
        {
            // The x86 helper consumes its pushed handle and preserves all registers.
            // Keep the caller's tracked stack level unchanged across the naked helper.
            var savedStackLevel = genStackLevel;
            if (Configuration.UNIX_X86_ABI)
            {
                Emitter.emitIns_R_I(INS_sub, EA_4BYTE, TargetRuntimeMetadata.StackPointerBaseRegister, 0xC);
            }
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
            if (Configuration.UNIX_X86_ABI)
            {
                Emitter.emitIns_R_I(INS_add, EA_4BYTE, TargetRuntimeMetadata.StackPointerBaseRegister, 0x10);
            }
            SetStackLevel(savedStackLevel);
            return;
        }

        if (Configuration.WINDOWS_AMD64_ABI)
        {
            // Windows requires homing and reloading incoming register arguments around the callback.
            noway_assert(_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
            noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 4 * Configuration.REGSIZE_BYTES);

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
                    if (Configuration.FEATURE_SIMD && (storeType == TYP_SIMD8) && genIsValidIntReg(argReg))
                    {
                        storeIns = INS_mov;
                    }
                    Emitter.emitIns_S_R(storeIns, storeType.EmitSize, argReg, varNum, 0);
                }
            }
        }

        var profilerArg0 = Configuration.UNIX_AMD64_ABI ? REG_X64_R14 : TargetRuntimeMetadata.FirstIntegerArgumentRegister;
        var profilerArg1 = Configuration.UNIX_AMD64_ABI ? REG_X64_R15 : TargetRuntimeMetadata.SecondIntegerArgumentRegister;
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

        if (Configuration.UNIX_AMD64_ABI)
        {
            genEmitHelperCall(CORINFO_HELP_PROF_FCN_ENTER, 0, EA_UNKNOWN, TargetRuntimeMetadata.DefaultHelperCallTargetRegister);
        }
        else
        {
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

                if (Configuration.FEATURE_SIMD && (loadType == TYP_SIMD8) && genIsValidIntReg(argReg))
                {
                    loadIns = INS_mov;
                }
                Emitter.emitIns_R_S(loadIns, loadType.EmitSize, argReg, varNum, 0);

                if (compFeatureVarArg() && _compiler.info.compIsVarArgs && varTypeIsFloating(loadType))
                {
                    var intArgReg = _compiler.getCallArgIntRegister(argReg);
                    inst_Mov(TYP_LONG, intArgReg, argReg, canSkip: false, size: loadType.EmitActualSize);
                }
            }
        }

        // The helper call or its argument setup may have overwritten the prolog's zero register.
        var initMask = regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask);
        if ((_compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_ENTER) & initMask).IsNonEmpty
            || (initMask & regMaskTP.CreateFromRegNum(profilerArg0, profilerArg0.SingleTypeMask)).IsNonEmpty
            || (initMask & regMaskTP.CreateFromRegNum(profilerArg1, profilerArg1.SingleTypeMask)).IsNonEmpty)
        {
            initRegZeroed = false;
        }
    }
}
