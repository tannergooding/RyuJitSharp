// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genJmpPlaceArgs(GenTree jmp)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "JMP argument placement requires a native target.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(jmp.Oper is GT_JMP);
        assert(_compiler.compJmpOpUsed);

        // Spill all register homes before reloading ABI registers, avoiding register cycles.
        // Do not change descriptor homes: other basic blocks still expect the original allocation.
        for (var varNum = 0; varNum < _compiler.info.compArgsCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            assert(!varDsc.lvPromoted);
            if (varDsc.RegNum == REG_STK)
            {
                continue;
            }

            var storeType = varDsc.GetStackSlotHomeType();
            Emitter.emitIns_S_R(ins_Store(storeType), storeType.EmitSize, varDsc.RegNum, varNum, 0);
            var tempMask = genGetRegMask(in varDsc);
            _regSet.RemoveMaskVars(tempMask);
            _gcInfo.gcMarkRegSetNpt(tempMask);
            if (_compiler.lvaIsGCTracked(in varDsc))
            {
#if DEBUG
                JITDUMP($"\t\t\t\t\t\t\tVar V{varNum:D2} " +
                    (VarSetOps.IsMember(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex)
                        ? "continuing live\n" : "becoming live\n"));
#endif
                VarSetOps.AddElemD(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex);
            }
        }

#if PROFILING_SUPPORTED
        // All argument registers are free during the profiler callback.
        genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);
#endif
        for (var varNum = 0; varNum < _compiler.info.compArgsCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            var abiInfo = _compiler.lvaGetParameterAbiInfo(varNum);
            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (segment.IsPassedOnStack)
                {
                    continue;
                }

                var stackType = genParamStackType(in varDsc, in segment);
                Emitter.emitIns_R_S(ins_Load(stackType), stackType.EmitSize, segment.Register, varNum, segment.Offset);
                var registerMask = segment.Register.SingleTypeMask;
#if TARGET_ARM
                if (genIsValidFloatReg(segment.Register) && (segment.Size == 8))
                {
                    registerMask |= (segment.Register + 1).SingleTypeMask;
                }
#endif
                _regSet.AddMaskVars(regMaskTP.CreateFromRegNum(segment.Register, registerMask));
                _gcInfo.gcMarkRegPtrVal(segment.Register, stackType);
            }

            if (_compiler.lvaIsGCTracked(in varDsc))
            {
#if DEBUG
                JITDUMP($"\t\t\t\t\t\t\tVar V{varNum:D2} " +
                    (VarSetOps.IsMember(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex)
                        ? "becoming dead\n" : "continuing dead\n"));
#endif
                VarSetOps.RemoveElemD(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex);
            }
        }

        if (compFeatureVarArg() && _compiler.info.compIsVarArgs)
        {
            genJmpPlaceVarArgs();
        }
#endif
    }

    public void genJmpPlaceVarArgs()
    {
        assert(_compiler.info.compIsVarArgs);
#if TARGET_LOONGARCH64
        genJmpPlaceVarArgsLoongArch64();
#elif TARGET_X86
        // All x86 varargs are already on the stack.
#elif WINDOWS_AMD64_ABI
        Emitter.RequireSupportedInstructionRecording();
        var potentialArgs = SRBM_ARG_REGS;

        for (var varNum = 0; varNum < _compiler.info.compArgsCount; varNum++)
        {
            var abiInfo = _compiler.lvaGetParameterAbiInfo(varNum);
            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (segment.IsPassedOnStack)
                {
                    continue;
                }

                var segmentType = segment.GetRegisterType();
                if (varTypeIsFloating(segmentType))
                {
                    var intArgReg = _compiler.getCallArgIntRegister(segment.Register);
                    inst_Mov(TYP_LONG, intArgReg, segment.Register, canSkip: false, size: segmentType.EmitActualSize);
                    potentialArgs &= ~intArgReg.SingleTypeMask;
                }
                else
                {
                    potentialArgs &= ~segment.RegisterMask;
                }
            }
        }

        if (potentialArgs == SRBM_NONE)
        {
            return;
        }

        // The prolog homed unknown varargs; restoring those bits can expose untracked GC references.
        Emitter.emitDisableGC();
        do
        {
            var potentialArg = (regNumber)BitOperations.TrailingZeroCount((ulong)potentialArgs);
            potentialArgs ^= potentialArg.SingleTypeMask;
            var potentialArgFloat = _compiler.getCallArgFloatRegister(potentialArg);
            var result = AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(potentialArg, out var offset);
            assert(result);
            offset = unchecked(offset - (IsFramePointerUsed ? genCallerSPtoFPdelta : genCallerSPtoInitialSPdelta));

            Emitter.emitIns_R_AR(INS_mov, EA_8BYTE, potentialArg, genFramePointerReg(), offset);
            inst_Mov(TYP_DOUBLE, potentialArgFloat, potentialArg, canSkip: false, size: TYP_I_IMPL.EmitActualSize);
        }
        while (potentialArgs != SRBM_NONE);

        Emitter.emitEnableGC();
#elif TARGET_ARM64
        var potentialArgs = SRBM_ARG_REGS;

        for (var varNum = 0; varNum < _compiler.info.compArgsCount; varNum++)
        {
            var abiInfo = _compiler.lvaGetParameterAbiInfo(varNum);
            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (!segment.IsPassedOnStack)
                {
                    potentialArgs &= ~segment.RegisterMask;
                }
            }
        }

        if (potentialArgs == SRBM_NONE)
        {
            return;
        }

        Emitter.emitDisableGC();
        do
        {
            var reg = (regNumber)BitOperations.TrailingZeroCount((ulong)potentialArgs);
            potentialArgs &= ~reg.SingleTypeMask;

            var regIndex = (int)reg - (int)REG_ARG_0;
            assert((regIndex >= 0) && (regIndex < MAX_REG_ARG));
            var loadOffset = unchecked((MAX_REG_ARG - regIndex) * -TARGET_POINTER_SIZE);
            loadOffset -= IsFramePointerUsed ? genCallerSPtoFPdelta : genCallerSPtoInitialSPdelta;

            Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, reg, genFramePointerReg(), loadOffset);
        }
        while (potentialArgs != SRBM_NONE);

        Emitter.emitEnableGC();
#elif TARGET_AMD64
        unreached();
        throw new FatalJitException(CORJIT_SKIPPED, "SysV AMD64 JMP varargs are not supported.");
#elif TARGET_RISCV64
        NYI_RISCV64("Varargs not supported");
#else
        throw new FatalJitException(CORJIT_SKIPPED, "JMP varargs placement is not yet ported for this target.");
#endif
    }

    public var_types genParamStackType(in LclVarDsc descriptor, in AbiPassingSegment segment)
    {
        assert(segment.IsPassedInRegister);
        switch (descriptor.Type)
        {
            case TYP_BYREF:
            case TYP_REF:
            {
                assert((segment.Offset == 0) && (segment.Size == TARGET_POINTER_SIZE));
                return descriptor.Type;
            }

            case TYP_STRUCT:
            {
                if (genIsValidFloatReg(segment.Register))
                {
                    return segment.GetRegisterType();
                }

                var layout = descriptor.Layout;
                assert(layout is not null);
                assert((uint)segment.Offset < layout.Size);
                if (((segment.Offset % TARGET_POINTER_SIZE) == 0) && (segment.Size == TARGET_POINTER_SIZE))
                {
                    return layout.GetGCPtrType(segment.Offset / TARGET_POINTER_SIZE);
                }

                if (_compiler.info.compCallConv == CorInfoCallConvExtension.Swift)
                {
                    return segment.GetRegisterType();
                }

#if TARGET_ARM64
                // Struct stack slots are rounded to pointer size, allowing paired stores.
                return TYP_I_IMPL;
#elif TARGET_XARCH
                // xarch uses the smallest instruction encoding, rounding small integer segments up.
                return segment.GetRegisterType().ActualType;
#else
                // Packed fields and adjacent stack slots must not be overwritten by wider stores.
                return segment.GetRegisterType();
#endif
            }

            default:
            {
                return segment.GetRegisterType().ActualType;
            }
        }
    }
}
