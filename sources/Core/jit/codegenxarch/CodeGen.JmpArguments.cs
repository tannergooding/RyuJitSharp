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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "JMP argument placement requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
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
            var tempMask = regMaskTP.CreateFromRegNum(varDsc.RegNum, varDsc.lvRegMask);
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
                // Windows AMD64 segments occupy one register; RegisterMask is ABI-bank-relative.
                _regSet.AddMaskVars(regMaskTP.CreateFromRegNum(segment.Register, segment.Register.SingleTypeMask));
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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "JMP varargs placement requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(_compiler.info.compIsVarArgs);
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
#endif
    }

    public var_types genParamStackType(in LclVarDsc descriptor, in AbiPassingSegment segment)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Parameter stack type selection requires Windows AMD64.");
#else
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

                // xarch uses the smallest instruction encoding, rounding small integer segments up.
                return segment.GetRegisterType().ActualType;
            }

            default:
            {
                return segment.GetRegisterType().ActualType;
            }
        }
#endif
    }
}
