// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private const int LINEAR_MEMORY_INDEX = 0;

#if TARGET_64BIT
    private const instruction INS_I_const = INS_i64_const;
    private const instruction INS_I_add = INS_i64_add;
#else
    private const instruction INS_I_const = INS_i32_const;
    private const instruction INS_I_add = INS_i32_add;
#endif

    public void ensureCurrentFuncIsUnwindable()
    {
        ref var func = ref _compiler.funCurrentFunc();
        func.ensureUnwindableFrame(_compiler);
    }

    public uint GetStackPointerRegIndex()
    {
        var spReg = GetStackPointerReg(_compiler.funCurrentFuncIdx());
        assert(spReg is not REG_NA);
        return regNumberExtensions.WasmRegToIndex(spReg);
    }

    public uint GetFramePointerRegIndex()
    {
        var fpReg = GetFramePointerReg(_compiler.funCurrentFuncIdx());
        assert(fpReg is not REG_NA);
        return regNumberExtensions.WasmRegToIndex(fpReg);
    }

    private void genBeginFnPrologWasm()
    {
        Emitter.emitIns(INS_code_size);

        ref var func = ref _compiler.funGetFunc(ROOT_FUNC_IDX);
        var localDeclarations = func.funWasmLocalDecls
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Wasm local declarations are missing.");
        assert(_compiler.funCurrentFuncIdx() == ROOT_FUNC_IDX);

        Emitter.emitIns_I(INS_local_cnt, EA_8BYTE, localDeclarations.Count);

        uint localIndex = 0;
        foreach (var declaration in localDeclarations)
        {
            Emitter.emitIns_I_Ty(INS_local_decl, declaration.Count, declaration.Type, unchecked((int)localIndex));
            localIndex = unchecked(localIndex + declaration.Count);
        }
    }

    public void genEnregisterOSRArgsAndLocals(regNumber initReg, ref bool initRegZeroed)
    {
        unreached();
    }

    public void genPushCalleeSavedRegisters(regNumber initReg, ref bool initRegZeroed)
    {
        // Wasm has no machine registers that need saving in the prolog.
    }

    public unsafe void genAllocLclFrame(
        uint frameSize, regNumber initReg, ref bool initRegZeroed, regMaskTP maskArgRegsLiveIn)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        var spReg = GetStackPointerReg(_compiler.funCurrentFuncIdx());
        if (spReg is REG_NA)
        {
            assert(!IsFramePointerUsed);
            return;
        }

        _compiler.unwindAllocStack(frameSize);

        var spLocalIndex = regNumberExtensions.WasmRegToIndex(spReg);
        var initialSpLocalIndex = spLocalIndex;
        if (!_compiler.lvaGetDesc(_compiler.lvaWasmSpArg).lvIsParam)
        {
            var stackPointer = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().stackPointer);
            Emitter.emitIns_I(INS_global_get, EA_HANDLE_CNS_RELOC, stackPointer);
            Emitter.emitIns_I(INS_local_set, EA_PTRSIZE, unchecked((nint)initialSpLocalIndex));
        }
        else
        {
            ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(_compiler.lvaWasmSpArg);
            initialSpLocalIndex = regNumberExtensions.WasmRegToIndex(abiInfo.Segments[0].Register);
        }

        assert(initialSpLocalIndex == spLocalIndex);
        if (frameSize != 0)
        {
            Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)initialSpLocalIndex));
            Emitter.emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)frameSize));
            Emitter.emitIns(INS_I_sub);
            Emitter.emitIns_I(INS_local_set, EA_PTRSIZE, unchecked((nint)spLocalIndex));
        }

        var fpReg = GetFramePointerReg(_compiler.funCurrentFuncIdx());
        if ((fpReg is not REG_NA) && (fpReg != spReg))
        {
            Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)spLocalIndex));
            Emitter.emitIns_I(
                INS_local_set, EA_PTRSIZE, unchecked((nint)regNumberExtensions.WasmRegToIndex(fpReg)));
        }

        ref var rootFunc = ref _compiler.funGetFunc(ROOT_FUNC_IDX);
        if (rootFunc.needsUnwindableFrame)
        {
            assert(_compiler.lvaWasmVirtualIP != BAD_VAR_NUM);
            assert(_compiler.lvaWasmFunctionIndex != BAD_VAR_NUM);

            Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
            Emitter.emitFuncletAddressConstant((nint)0);
            Emitter.emitIns_S(ins_Store(TYP_I_IMPL), EA_PTRSIZE, _compiler.lvaWasmFunctionIndex, 0);

            if ((_compiler.funCurrentFuncIdx() == ROOT_FUNC_IDX) && (_compiler.lvaWasmResumeIP != BAD_VAR_NUM))
            {
                Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
                Emitter.emitIns_I(INS_I_const, EA_4BYTE, 0);
                Emitter.emitIns_S(ins_Store(TYP_INT), EA_4BYTE, _compiler.lvaWasmResumeIP, 0);
            }
        }
    }

    public void genZeroInitFrame(int untrLclHi, int untrLclLo, regNumber initReg, ref bool initRegZeroed)
    {
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
    }

    internal static instruction WasmBitCastInstruction(var_types toType, var_types fromType)
    {
        toType = toType.ActualType;
        fromType = fromType.ActualType;

        if (toType is TYP_REF or TYP_BYREF)
        {
            toType = TYP_I_IMPL;
        }
        if (fromType is TYP_REF or TYP_BYREF)
        {
            fromType = TYP_I_IMPL;
        }

        if (toType == fromType)
        {
            return INS_none;
        }

        return (toType, fromType) switch {
            (TYP_INT, TYP_FLOAT) => INS_i32_reinterpret_f32,
            (TYP_FLOAT, TYP_INT) => INS_f32_reinterpret_i32,
            (TYP_LONG, TYP_DOUBLE) => INS_i64_reinterpret_f64,
            (TYP_DOUBLE, TYP_LONG) => INS_f64_reinterpret_i64,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, "Unsupported Wasm bitcast."),
        };
    }

    internal var_types genParamStackTypeWasm(in LclVarDsc descriptor, in AbiPassingSegment segment)
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
                assert((segment.Offset >= 0) && ((uint)segment.Offset < layout.Size));
                if (((segment.Offset % TARGET_POINTER_SIZE) == 0) && (segment.Size == TARGET_POINTER_SIZE))
                {
                    return layout.GetGCPtrType(segment.Offset / TARGET_POINTER_SIZE);
                }

                if (_compiler.info.compCallConv == CorInfoCallConvExtension.Swift)
                {
                    return segment.GetRegisterType();
                }

                return segment.GetRegisterType();
            }

            default:
            {
                return segment.GetRegisterType().ActualType;
            }
        }
    }

    private void genHomeRegisterParamsWasm()
    {
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genHomeRegisterParams()\n");
        }
#endif
        // Wasm assigns homed values to newly declared locals, so register cycles cannot occur.
        void SpillParameter(int localNumber, int offset, int parameterLocalNumber, in AbiPassingSegment segment)
        {
            assert(segment.IsPassedInRegister);

            ref var local = ref _compiler.lvaGetDesc(localNumber);
            ref var parameter = ref _compiler.lvaGetDesc(parameterLocalNumber);
            var sourceType = genParamStackTypeWasm(in parameter, in segment);

            if (local.lvTracked)
            {
                assert(_compiler.fgFirstBB is not null);
                // On-frame GC locals are reported as untracked, so their homes must still be initialized.
                if (!VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, local._varIndex)
                    && !(local.lvOnFrame && local.HasGCPtr))
                {
                    return;
                }
            }

            if (local.lvOnFrame && (!local.lvIsInReg || local.IsLiveInOutOfHandler))
            {
                var storeType = sourceType;
                if ((local.Type != TYP_STRUCT) && (local.Type.ActualType.Size < storeType.Size))
                {
                    storeType = local.Type.ActualType;
                }

                Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
                Emitter.emitIns_I(INS_local_get, storeType.EmitActualSize,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(segment.Register)));
                Emitter.emitIns_S(ins_Store(storeType), storeType.EmitActualSize, localNumber, offset);
            }

            if (local.lvIsInReg)
            {
                var sourceReg = segment.Register;
                var targetReg = local.RegNum;
                if (targetReg != sourceReg)
                {
                    var targetType = local.GetRegisterType();
                    assert(regNumberExtensions.WasmRegToType(sourceReg)
                        == regNumberExtensions.ActualTypeToWasmValueType(sourceType));
                    assert(regNumberExtensions.WasmRegToType(targetReg)
                        == regNumberExtensions.ActualTypeToWasmValueType(targetType));
                    assert(((sourceType == TYP_FLOAT) && (targetType == TYP_INT))
                        || ((sourceType == TYP_DOUBLE) && (targetType == TYP_LONG)));

                    Emitter.emitIns_I(INS_local_get, sourceType.EmitActualSize,
                        unchecked((nint)regNumberExtensions.WasmRegToIndex(sourceReg)));
                    Emitter.emitIns(WasmBitCastInstruction(targetType, sourceType));
                    Emitter.emitIns_I(INS_local_set, targetType.EmitActualSize,
                        unchecked((nint)regNumberExtensions.WasmRegToIndex(targetReg)));
                }
            }
        }

        for (var localNumber = 0; localNumber < _compiler.info.compArgsCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(localNumber);

            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (!segment.IsPassedInRegister)
                {
                    continue;
                }

                var mapping = _compiler.FindParameterRegisterLocalMappingByRegister(segment.Register);
                var spillToBaseLocal = true;
                if (mapping is ParameterRegisterLocalMapping mapped)
                {
                    SpillParameter(mapped.LclNum, unchecked((int)mapped.Offset), localNumber, in segment);
                    if (local.lvPromoted)
                    {
                        spillToBaseLocal = false;
                    }
                }

                if (spillToBaseLocal)
                {
                    SpillParameter(localNumber, segment.Offset, localNumber, in segment);
                }
            }
        }
    }

    private void genReportGenericContextArgWasm()
    {
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
    }

    private unsafe void genFnEpilogWasm(BasicBlock block)
    {
        var nextBlock = block.Next;

#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFnEpilog()\n");
        }
        if (_compiler.opts.dspCode)
        {
            jitprintf("\n__epilog:\n");
        }
#endif
        var jmpEpilog = block.HasFlag(BBF_HAS_JMP);
        if (jmpEpilog)
        {
            if ((nextBlock is null) || _compiler.bbIsFuncletBeg(nextBlock))
            {
                instGen(INS_end);
            }

            return;
        }

        if (_compiler.opts.IsReversePInvoke)
        {
            assert(_compiler.funCurrentFuncIdx() == ROOT_FUNC_IDX);
            var framePointer = GetFramePointerReg(ROOT_FUNC_IDX);
            var stackPointer = GetStackPointerReg(ROOT_FUNC_IDX);
            assert(stackPointer is not REG_NA);

            var frameBase = (framePointer is not REG_NA) ? framePointer : stackPointer;
            Emitter.emitIns_I(INS_local_get, EA_PTRSIZE,
                unchecked((nint)regNumberExtensions.WasmRegToIndex(frameBase)));
            if (genTotalFrameSize != 0)
            {
                Emitter.emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)genTotalFrameSize));
                Emitter.emitIns(INS_I_add);
            }

            var stackPointerGlobal = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().stackPointer);
            Emitter.emitIns_I(INS_global_set, EA_HANDLE_CNS_RELOC, stackPointerGlobal);
        }

        // A final block without an epilog still needs an INS_end to close its Wasm function body.
        if ((nextBlock is null) || _compiler.bbIsFuncletBeg(nextBlock))
        {
            instGen(INS_end);
        }
        else
        {
            instGen(INS_return);
        }
    }

    private void genFuncletPrologWasm(BasicBlock block)
    {
        assert(_compiler.bbIsFuncletBeg(block));
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }
#endif

        Emitter.emitIns(INS_code_size);

        var funcletIndex = _compiler.funCurrentFuncIdx();
        assert(funcletIndex > 0);
        ref var func = ref _compiler.funGetFunc(funcletIndex);
        var localDeclarations = func.funWasmLocalDecls
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Wasm funclet local declarations are missing.");
        Emitter.emitIns_I(INS_local_cnt, EA_8BYTE, localDeclarations.Count);

        uint localsCount = 0;
        foreach (var declaration in localDeclarations)
        {
            Emitter.emitIns_I_Ty(
                INS_local_decl, declaration.Count, declaration.Type, unchecked((int)localsCount));
            localsCount = unchecked(localsCount + declaration.Count);
        }

        if (!func.needsUnwindableFrame)
        {
            return;
        }

        var frameSize = roundUp(2 * TARGET_POINTER_SIZE, STACK_ALIGN);
        _compiler.unwindAllocStack(unchecked((uint)frameSize));

        var stackPointerIndex = unchecked((nint)GetStackPointerRegIndex());
        Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, stackPointerIndex);
        Emitter.emitIns_I(INS_I_const, EA_PTRSIZE, frameSize);
        Emitter.emitIns(INS_I_sub);
        Emitter.emitIns_I(INS_local_set, EA_PTRSIZE, stackPointerIndex);

        Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, stackPointerIndex);
        Emitter.emitFuncletAddressConstant(unchecked((nint)funcletIndex));
        Emitter.emitIns_I(ins_Store(TYP_I_IMPL), EA_PTRSIZE, 0);
    }

    private void genFuncletEpilogWasm(BasicBlock block)
    {
        var nextBlock = block.Next;
        if ((nextBlock is null) || _compiler.bbIsFuncletBeg(nextBlock))
        {
            instGen(INS_end);
        }
        else
        {
            instGen(INS_return);
        }
    }
}
#endif
