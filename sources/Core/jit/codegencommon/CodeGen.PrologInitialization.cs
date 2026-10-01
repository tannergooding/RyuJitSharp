// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if !TARGET_WASM
    // Architectural zero registers leave initReg and its zeroed state untouched.
    public regNumber genGetZeroReg(regNumber initReg, ref bool initRegZeroed)
    {
#if TARGET_ARM64
        return REG_ZR;
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
        return REG_R0;
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
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
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        // The first float/double reg initialized to 0 can initialize the remaining registers.
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
#if TARGET_ARM
                    if (dblInitReg != REG_NA)
                    {
                        inst_RV_RV(INS_vcvt_d2f, reg, dblInitReg, TYP_FLOAT);
                    }
                    else
                    {
                        inst_Mov(TYP_FLOAT, reg, initReg, canSkip: false);
                    }
#elif TARGET_XARCH
                    // XORPS is the fastest and smallest way to initialize a XMM register to zero.
                    Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, reg, reg, reg, INS_OPTS_NONE);
                    dblInitReg = reg;
#elif TARGET_ARM64
                    // Zero the entire vector register, setting both double and float to zero.
                    Emitter.emitIns_R_I(INS_movi, EA_16BYTE, reg, 0x00, INS_OPTS_16B);
#elif TARGET_LOONGARCH64
                    Emitter.emitIns_R_R(INS_movgr2fr_d, EA_8BYTE, reg, REG_R0);
#elif TARGET_RISCV64
                    Emitter.emitIns_R_R(INS_fmv_w_x, EA_4BYTE, reg, REG_R0);
#else
#error Unsupported or unset target architecture
#endif
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
#if TARGET_ARM
                    if (fltInitReg != REG_NA)
                    {
                        inst_RV_RV(INS_vcvt_f2d, reg, fltInitReg, TYP_DOUBLE);
                    }
                    else
                    {
                        inst_RV_RV_RV(INS_vmov_i2d, reg, initReg, initReg, EA_8BYTE);
                    }
#elif TARGET_XARCH
                    Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, reg, reg, reg, INS_OPTS_NONE);
                    fltInitReg = reg;
#elif TARGET_ARM64
                    Emitter.emitIns_R_I(INS_movi, EA_16BYTE, reg, 0x00, INS_OPTS_16B);
#elif TARGET_LOONGARCH64
                    Emitter.emitIns_R_R(INS_movgr2fr_d, EA_8BYTE, reg, REG_R0);
#elif TARGET_RISCV64
                    Emitter.emitIns_R_R(INS_fmv_d_x, EA_8BYTE, reg, REG_R0);
#else
#error Unsupported or unset target architecture
#endif
                    dblInitReg = reg;
                }
            }
        }
    }

    public void genZeroInitFrame(int untrLclHi, int untrLclLo, regNumber initReg, ref bool initRegZeroed)
    {
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
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
                    // This local belongs on the UnknownSizeFrame, which handles zeroing instead.
                    continue;
                }

                noway_assert(varTypeIsGC(local.Type) || (local.Type == TYP_STRUCT)
                    || _compiler.info.compInitMem || _compiler.opts.compDbgCode);
                if ((local.Type == TYP_STRUCT) && !_compiler.info.compInitMem
                    && (local.lvExactSize >= TARGET_POINTER_SIZE))
                {
                    // Only initialize the GC slots of a struct when initlocals was not requested.
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
                    // Zero the whole home rounded up to a single stack slot size.
                    var size = roundUp(_compiler.lvaLclStackHomeSize(varNum), sizeof(int));
                    var i = 0;
                    for (; i + REGSIZE_BYTES <= size; i += REGSIZE_BYTES)
                    {
                        Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, zeroReg, varNum, i);
                    }

#if TARGET_64BIT
                    assert((i == size) || (i + sizeof(int) == size));
                    if (i != size)
                    {
                        Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, varNum, i);
                        i += sizeof(int);
                    }
#endif
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
    }
#endif

    public unsafe void genReportGenericContextArg(regNumber initReg, ref bool initRegZeroed)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog generic-context reporting requires AMD64.");
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
