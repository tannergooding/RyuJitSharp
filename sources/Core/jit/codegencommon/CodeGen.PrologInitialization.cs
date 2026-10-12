// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // Architectural zero registers leave initReg and its zeroed state untouched.
    public regNumber genGetZeroReg(regNumber initReg, ref bool initRegZeroed)
    {
        if (Configuration.TARGET_WASM)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Wasm has no architectural integer zero register.");
        }

        if (Configuration.TARGET_ARM64)
        {
            return REG_ARM64_ZR;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            return REG_LOONGARCH64_R0;
        }
        if (Configuration.TARGET_RISCV64)
        {
            return REG_RISCV64_R0;
        }
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        if (!initRegZeroed)
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, initReg);
            initRegZeroed = true;
        }

        return initReg;
    }

    public void genZeroInitFltRegs(regMaskTP initFltRegs, regMaskTP initDblRegs, regNumber initReg)
    {
        if (Configuration.TARGET_WASM)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Wasm has no architectural floating-point registers.");
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

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
                    if (Configuration.TARGET_ARM)
                    {
                        if (dblInitReg != REG_NA)
                        {
                            inst_RV_RV(INS_vcvt_d2f, reg, dblInitReg, TYP_FLOAT);
                        }
                        else
                        {
                            inst_Mov(TYP_FLOAT, reg, initReg, canSkip: false);
                        }
                    }
                    else if (Configuration.TARGET_XARCH)
                    {
                        // XORPS is the fastest and smallest way to initialize a XMM register to zero.
                        Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, reg, reg, reg, INS_OPTS_NONE);
                        dblInitReg = reg;
                    }
                    else if (Configuration.TARGET_ARM64)
                    {
                        // Zero the entire vector register, setting both double and float to zero.
                        genZeroInitFltRegArm64(reg);
                    }
                    else if (Configuration.TARGET_LOONGARCH64)
                    {
                        Emitter.emitIns_R_R(INS_movgr2fr_d, EA_8BYTE, reg, REG_LOONGARCH64_R0);
                    }
                    else if (Configuration.TARGET_RISCV64)
                    {
                        Emitter.emitIns_R_R(INS_fmv_w_x, EA_4BYTE, reg, REG_RISCV64_R0);
                    }
                    else
                    {
                        throw new FatalJitException(CORJIT_SKIPPED, "Floating-register zeroing is not implemented for this target.");
                    }
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
                    if (Configuration.TARGET_ARM)
                    {
                        if (fltInitReg != REG_NA)
                        {
                            inst_RV_RV(INS_vcvt_f2d, reg, fltInitReg, TYP_DOUBLE);
                        }
                        else
                        {
                            inst_RV_RV_RV(INS_vmov_i2d, reg, initReg, initReg, EA_8BYTE);
                        }
                    }
                    else if (Configuration.TARGET_XARCH)
                    {
                        Emitter.emitIns_SIMD_R_R_R(INS_xorps, EA_16BYTE, reg, reg, reg, INS_OPTS_NONE);
                        fltInitReg = reg;
                    }
                    else if (Configuration.TARGET_ARM64)
                    {
                        genZeroInitFltRegArm64(reg);
                    }
                    else if (Configuration.TARGET_LOONGARCH64)
                    {
                        Emitter.emitIns_R_R(INS_movgr2fr_d, EA_8BYTE, reg, REG_LOONGARCH64_R0);
                    }
                    else if (Configuration.TARGET_RISCV64)
                    {
                        Emitter.emitIns_R_R(INS_fmv_d_x, EA_8BYTE, reg, REG_RISCV64_R0);
                    }
                    else
                    {
                        throw new FatalJitException(CORJIT_SKIPPED, "Floating-register zeroing is not implemented for this target.");
                    }
                    dblInitReg = reg;
                }
            }
        }
    }

    public void genZeroInitFrame(int untrLclHi, int untrLclLo, regNumber initReg, ref bool initRegZeroed)
    {
        if (Configuration.TARGET_WASM)
        {
            genZeroInitFrameWasm(untrLclHi, untrLclLo, initReg, ref initRegZeroed);
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

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
                if (_compiler.lvaLocalIsOnUnknownSizeFrame(varNum))
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

                    if (Configuration.TARGET_64BIT)
                    {
                        assert((i == size) || (i + sizeof(int) == size));
                        if (i != size)
                        {
                            Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, zeroReg, varNum, i);
                            i += sizeof(int);
                        }
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
    }

    private void genZeroInitFltRegArm64(regNumber reg)
    {
#if TARGET_ARM64
        Emitter.emitIns_R_I(INS_movi, EA_16BYTE, reg, 0x00, INS_OPTS_16B);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 floating-register zeroing is unavailable.");
#endif
    }

    private void genZeroInitFrameWasm(int untrLclHi, int untrLclLo, regNumber initReg, ref bool initRegZeroed)
    {
#if TARGET_WASM
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (!UseBlockInit)
        {
            assert(InitStkLclCnt is 0);
            return;
        }

        assert(untrLclHi > untrLclLo);
        assert(untrLclLo >= 0);

        Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, checked((nint)GetFramePointerRegIndex()));
        if (untrLclLo is not 0)
        {
            Emitter.emitIns_I(INS_I_const, EA_PTRSIZE, untrLclLo);
            Emitter.emitIns(INS_I_add);
        }

        Emitter.emitIns_I(INS_i32_const, EA_4BYTE, 0);
        Emitter.emitIns_I(INS_I_const, EA_PTRSIZE, untrLclHi - untrLclLo);
        Emitter.emitIns_I(INS_memory_fill, EA_4BYTE, LINEAR_MEMORY_INDEX);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm frame initialization is unavailable for this target.");
#endif
    }

    private void genReportGenericContextArgWasm()
    {
#if TARGET_WASM
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        var reportArg = _compiler.lvaReportParamTypeArg();
        var reportThis = _compiler.lvaKeepAliveAndReportThis();
        if (!reportArg && !reportThis)
        {
            return;
        }

        var contextArg = reportArg ? _compiler.info.compTypeCtxtArg : _compiler.info.compThisArg;
        noway_assert(contextArg != BAD_VAR_NUM);

        ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(contextArg);
        assert(abiInfo.HasExactlyOneRegisterSegment);
        var register = abiInfo.Segments[0].Register;

        Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
        Emitter.emitIns_I(INS_local_get, EA_PTRSIZE,
            unchecked((nint)regNumberExtensions.WasmRegToIndex(register)));
        Emitter.emitIns_I(ins_Store(TYP_I_IMPL), EA_PTRSIZE,
            unchecked((nint)_compiler.lvaCachedGenericContextArgOffset()));
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm generic-context reporting is unavailable for this target.");
#endif
    }

    private void genStoreGenericContextArgArm32(regNumber reg, int offset)
    {
#if TARGET_ARM
        Emitter.emitIns_R_R_I(ins_Store(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(),
            offset, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 generic-context storage is unavailable.");
#endif
    }

    private void genStoreGenericContextArgArm64(regNumber reg, nint offset)
    {
#if TARGET_ARM64
        genInstrWithConstant(ins_Store(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(),
            offset, rsGetRsvdReg());
#else
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 generic-context storage is unavailable.");
#endif
    }

    private void genStoreGenericContextArgLoongArch64(regNumber reg, nint offset)
    {
#if TARGET_LOONGARCH64
        genInstrWithConstant(ins_Store(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(),
            offset, REG_LOONGARCH64_R21);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 generic-context storage is unavailable.");
#endif
    }

    private void genStoreGenericContextArgRiscV64(regNumber reg, nint offset)
    {
#if TARGET_RISCV64
        genInstrWithConstant(ins_Store(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(),
            offset, rsGetRsvdReg());
#else
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 generic-context storage is unavailable.");
#endif
    }

    private void genLoadGenericContextArgFromStack(regNumber reg, int stackOffset)
    {
#if !TARGET_WASM
        Emitter.emitIns_R_AR(ins_Load(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(), stackOffset);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Native generic-context loading is unavailable on Wasm.");
#endif
    }

    private void genStoreGenericContextArgXarch(regNumber reg, nint offset)
    {
#if TARGET_XARCH
        Emitter.emitIns_AR_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, reg, genFramePointerReg(), offset);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "xarch generic-context storage is unavailable.");
#endif
    }

    private bool genIsContextArgPrespilledForProfiling(int contextArg)
    {
#if TARGET_ARM && PROFILING_SUPPORTED
        return _compiler.compIsProfilerHookNeeded
            && _compiler.lvaIsPreSpilled(contextArg, _regSet.rsMaskPreSpillRegs(false));
#else
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 profiling pre-spill information is unavailable.");
#endif
    }

    public unsafe void genReportGenericContextArg(regNumber initReg, ref bool initRegZeroed)
    {
        if (Configuration.TARGET_WASM)
        {
            genReportGenericContextArgWasm();
            return;
        }

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

        if (!reportArg && (Configuration.TARGET_X86 || !_compiler.lvaKeepAliveAndReportThis()))
        {
            return;
        }

        var contextArg = reportArg ? _compiler.info.compTypeCtxtArg : _compiler.info.compThisArg;
        noway_assert(contextArg != BAD_VAR_NUM);
        ref         var local = ref _compiler.lvaGetDesc(contextArg);
        var isPrespilledForProfiling = false;
        if (Configuration.TARGET_ARM && Configuration.PROFILING_SUPPORTED)
        {
            isPrespilledForProfiling = genIsContextArgPrespilledForProfiling(contextArg);
        }

        ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(contextArg);
        regNumber reg;
        if (abiInfo.HasExactlyOneRegisterSegment && !isPrespilledForProfiling)
        {
            reg = abiInfo.Segments[0].Register;
        }
        else
        {
            reg = initReg;
            initRegZeroed = false;
            genLoadGenericContextArgFromStack(reg, local.StackOffset);
            _regSet.verifyRegUsed(reg);
        }

        var offset = _compiler.lvaCachedGenericContextArgOffset();
        if (Configuration.TARGET_ARM64)
        {
            genStoreGenericContextArgArm64(reg, offset);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genStoreGenericContextArgRiscV64(reg, offset);
        }
        else if (Configuration.TARGET_ARM)
        {
            genStoreGenericContextArgArm32(reg, offset);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genStoreGenericContextArgLoongArch64(reg, offset);
        }
        else if (Configuration.TARGET_XARCH)
        {
            genStoreGenericContextArgXarch(reg, offset);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Generic-context reporting is not implemented for this target.");
        }
    }
}
