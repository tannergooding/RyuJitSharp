// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public regNumber genGetZeroReg(regNumber initReg, ref bool initRegZeroed)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog zero-register initialization requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        if (!initRegZeroed)
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, initReg);
            initRegZeroed = true;
        }

        return initReg;
#endif
    }

    public void genZeroInitFltRegs(regMaskTP initFltRegs, regMaskTP initDblRegs, regNumber initReg)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog floating-register initialization requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        var fltInitReg = REG_NA;
        var dblInitReg = REG_NA;

        for (var reg = REG_FP_FIRST; reg <= REG_FP_LAST; reg++)
        {
            var mask = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((mask & initFltRegs) != RBM_NONE)
            {
                if (fltInitReg != REG_NA)
                {
                    inst_Mov(TYP_FLOAT, reg, fltInitReg, canSkip: false);
                }
                else
                {
                    Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, reg, reg, reg, INS_OPTS_NONE);
                    dblInitReg = reg;
                    fltInitReg = reg;
                }
            }
            else if ((mask & initDblRegs) != RBM_NONE)
            {
                if (dblInitReg != REG_NA)
                {
                    inst_Mov(TYP_DOUBLE, reg, dblInitReg, canSkip: false);
                }
                else
                {
                    Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, reg, reg, reg, INS_OPTS_NONE);
                    fltInitReg = reg;
                    dblInitReg = reg;
                }
            }
        }
#endif
    }

    public void genZeroInitFrame(int untrLclHi, int untrLclLo, regNumber initReg, ref bool initRegZeroed)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog stack initialization requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (UseBlockInit)
        {
            genZeroInitFrameUsingBlockInit(untrLclHi, untrLclLo, initReg, ref initRegZeroed);
        }
        else if (InitStkLclCnt > 0)
        {
            assert((regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask) & _calleeRegArgMaskLiveIn).IsEmpty);
            for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
            {
                ref var local = ref _compiler.lvaGetDesc(varNum);
                if (!local.lvMustInit || (local.lvIsInReg && !local.IsLiveInOutOfHandler))
                {
                    continue;
                }

                noway_assert(local.lvOnFrame);
                if (_compiler.lvaIsUnknownSizeLocal(varNum))
                {
                    continue;
                }

                noway_assert(varTypeIsGC(local.Type) || (local.Type == TYP_STRUCT)
                    || _compiler.info.compInitMem || _compiler.opts.compDbgCode);
                if ((local.Type == TYP_STRUCT) && !_compiler.info.compInitMem
                    && (local.lvExactSize >= TARGET_POINTER_SIZE))
                {
                    var slots = _compiler.lvaLclStackHomeSize(varNum) / REGSIZE_BYTES;
                    var layout = local.Layout;
                    assert(layout is not null);
                    for (var i = 0; i < slots; i++)
                    {
                        if (layout.IsGCPtr(i))
                        {
                            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE,
                                genGetZeroReg(initReg, ref initRegZeroed), varNum, i * REGSIZE_BYTES);
                        }
                    }
                }
                else
                {
                    var zeroReg = genGetZeroReg(initReg, ref initRegZeroed);
                    var size = roundUp(_compiler.lvaLclStackHomeSize(varNum), sizeof(int));
                    var i = 0;
                    for (; i + REGSIZE_BYTES <= size; i += REGSIZE_BYTES)
                    {
                        Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, zeroReg, varNum, i);
                    }

                    assert((i == size) || (i + sizeof(int) == size));
                    if (i != size)
                    {
                        Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, varNum, i);
                        i += sizeof(int);
                    }
                    assert(i == size);
                }
            }

#if DEBUG
            assert(_regSet.tmpGetAllFree());
#endif
            for (var temp = _regSet.tmpListBeg(); temp is not null; temp = _regSet.tmpListNxt(temp))
            {
                if (varTypeIsGC(temp.tdTempType))
                {
                    inst_ST_RV(ins_Store(TYP_I_IMPL), temp, 0,
                        genGetZeroReg(initReg, ref initRegZeroed), TYP_I_IMPL);
                }
            }
        }
#endif
    }

    public unsafe void genReportGenericContextArg(regNumber initReg, ref bool initRegZeroed)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog generic-context reporting requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        var reportArg = _compiler.lvaReportParamTypeArg();
        if (_compiler.opts.IsOSR)
        {
            var patchpoint = _compiler.info.compPatchpointInfo;
            assert(patchpoint is not null);
            if (reportArg)
            {
                assert(patchpoint->HasGenericContextArgOffset);
                JITDUMP("OSR method will use Tier0 frame slot for generics context arg.\n");
            }
            else if (_compiler.lvaKeepAliveAndReportThis())
            {
                assert(patchpoint->HasKeptAliveThis);
                JITDUMP("OSR method will use Tier0 frame slot for generics context `this`.\n");
            }

            return;
        }

        if (!reportArg && !_compiler.lvaKeepAliveAndReportThis())
        {
            return;
        }

        var contextArg = reportArg ? _compiler.info.compTypeCtxtArg : _compiler.info.compThisArg;
        noway_assert(contextArg != BAD_VAR_NUM);
        ref var local = ref _compiler.lvaGetDesc(contextArg);
        ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(contextArg);
        regNumber reg;
        if (abiInfo.HasExactlyOneRegisterSegment)
        {
            reg = abiInfo.Segments[0].Register;
        }
        else
        {
            reg = initReg;
            initRegZeroed = false;
            Emitter.emitIns_R_AR(ins_Load(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(), local.StackOffset);
            _regSet.verifyRegUsed(reg);
        }

        Emitter.emitIns_AR_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(),
            _compiler.lvaCachedGenericContextArgOffset());
#endif
    }
}
