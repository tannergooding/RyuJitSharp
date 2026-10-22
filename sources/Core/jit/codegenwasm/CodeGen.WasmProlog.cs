// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private const int LINEAR_MEMORY_INDEX = 0;

    private static class WasmInstructions
    {
        internal static readonly instruction INS_I_const =
            Configuration.TARGET_64BIT ? INS_i64_const : INS_i32_const;

        internal static readonly instruction INS_I_add =
            Configuration.TARGET_64BIT ? INS_i64_add : INS_i32_add;

        internal static readonly instruction INS_I_or =
            Configuration.TARGET_64BIT ? INS_i64_or : INS_i32_or;

        internal static readonly instruction INS_I_shl =
            Configuration.TARGET_64BIT ? INS_i64_shl : INS_i32_shl;

        internal static readonly instruction INS_I_load =
            Configuration.TARGET_64BIT ? INS_i64_load : INS_i32_load;

        internal static readonly instruction INS_I_store =
            Configuration.TARGET_64BIT ? INS_i64_store : INS_i32_store;

        internal static readonly instruction INS_I_and =
            Configuration.TARGET_64BIT ? INS_i64_and : INS_i32_and;

        internal static readonly instruction INS_I_eqz =
            Configuration.TARGET_64BIT ? INS_i64_eqz : INS_i32_eqz;

        internal static readonly instruction INS_I_mul =
            Configuration.TARGET_64BIT ? INS_i64_mul : INS_i32_mul;

        internal static readonly instruction INS_I_sub =
            Configuration.TARGET_64BIT ? INS_i64_sub : INS_i32_sub;

        internal static readonly instruction INS_I_le_u =
            Configuration.TARGET_64BIT ? INS_i64_le_u : INS_i32_le_u;

        internal static readonly instruction INS_I_ge_u =
            Configuration.TARGET_64BIT ? INS_i64_ge_u : INS_i32_ge_u;

        internal static readonly instruction INS_I_gt_u =
            Configuration.TARGET_64BIT ? INS_i64_gt_u : INS_i32_gt_u;
    }

    public void ensureCurrentFuncIsUnwindable()
    {
        ref var func = ref _compiler.funCurrentFunc();
        func.ensureUnwindableFrame(_compiler);
    }

    public uint GetStackPointerRegIndex()
    {
        var spReg = GetStackPointerReg(_compiler.funCurrentFuncIdx());
        assert(spReg is not REG_WASM_NA);
        return regNumberExtensions.WasmRegToIndex(spReg);
    }

    public uint GetFramePointerRegIndex()
    {
        var fpReg = GetFramePointerReg(_compiler.funCurrentFuncIdx());
        assert(fpReg is not REG_WASM_NA);
        return regNumberExtensions.WasmRegToIndex(fpReg);
    }

    private void genBeginFnPrologWasm()
    {
        Emitter.emitIns(INS_code_size);

        ref var func = ref _compiler.funGetFunc(ROOT_FUNC_IDX);
        var localDeclarations = func.funWasmLocalDecls;
        if (localDeclarations is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.WasmLocalDeclarationsAreMissing);
            return;
        }
        assert(_compiler.funCurrentFuncIdx() == ROOT_FUNC_IDX);

        Emitter.emitIns_I(INS_local_cnt, EA_8BYTE, localDeclarations.Count);

        uint localIndex = 0;
        foreach (var declaration in localDeclarations)
        {
            Emitter.emitIns_I_Ty(INS_local_decl, declaration.Count, declaration.Type, unchecked((int)localIndex));
            localIndex = unchecked(localIndex + declaration.Count);
        }
    }

    public void genEnregisterOSRArgsAndLocalsWasm(regNumber initReg, ref bool initRegZeroed)
    {
        unreached();
    }

    public void genPushCalleeSavedRegistersWasm(regNumber initReg, ref bool initRegZeroed)
    {
        // Wasm has no machine registers that need saving in the prolog.
    }

    public unsafe void genAllocLclFrameWasm(
        uint frameSize, regNumber initReg, ref bool initRegZeroed, regMaskTP maskArgRegsLiveIn)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        var spReg = GetStackPointerReg(_compiler.funCurrentFuncIdx());
        if (spReg is REG_WASM_NA)
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
            Emitter.emitIns_I(WasmInstructions.INS_I_const, EA_PTRSIZE, unchecked((nint)frameSize));
            Emitter.emitIns(WasmInstructions.INS_I_sub);
            Emitter.emitIns_I(INS_local_set, EA_PTRSIZE, unchecked((nint)spLocalIndex));
        }

        var fpReg = GetFramePointerReg(_compiler.funCurrentFuncIdx());
        if ((fpReg is not REG_WASM_NA) && (fpReg != spReg))
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
            if (_compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_WASM_MANAGED_HELPER))
            {
                Emitter.emitIns_I(INS_i32_const_frameidentity, EA_PTRSIZE, 0);
            }
            else if (_compiler.lvaWasmPortableEntryPointArg != BAD_VAR_NUM)
            {
                ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(
                    _compiler.lvaWasmPortableEntryPointArg);
                var pepIndex = regNumberExtensions.WasmRegToIndex(abiInfo.Segments[0].Register);
                Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)pepIndex));
            }
            else
            {
                Emitter.emitFuncletAddressConstant((nint)0);
                Emitter.emitIns_I(WasmInstructions.INS_I_const, EA_PTRSIZE, 1);
                Emitter.emitIns(WasmInstructions.INS_I_shl);
                Emitter.emitIns_I(WasmInstructions.INS_I_const, EA_PTRSIZE, 1);
                Emitter.emitIns(WasmInstructions.INS_I_or);
            }

            Emitter.emitIns_S(ins_Store(TYP_I_IMPL), EA_PTRSIZE, _compiler.lvaWasmFunctionIndex, 0);

            if ((_compiler.funCurrentFuncIdx() == ROOT_FUNC_IDX) && (_compiler.lvaWasmResumeIP != BAD_VAR_NUM))
            {
                Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
                Emitter.emitIns_I(WasmInstructions.INS_I_const, EA_4BYTE, 0);
                Emitter.emitIns_S(ins_Store(TYP_INT), EA_4BYTE, _compiler.lvaWasmResumeIP, 0);
            }
        }
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

        switch ((toType, fromType))
        {
            case (TYP_INT, TYP_FLOAT):
            {
                return INS_i32_reinterpret_f32;
            }

            case (TYP_FLOAT, TYP_INT):
            {
                return INS_f32_reinterpret_i32;
            }

            case (TYP_LONG, TYP_DOUBLE):
            {
                return INS_i64_reinterpret_f64;
            }

            case (TYP_DOUBLE, TYP_LONG):
            {
                return INS_f64_reinterpret_i64;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.UnsupportedWasmBitcast);
                return default!;
            }
        }
    }

    internal var_types genParamStackTypeWasm(in LclVarDsc descriptor, in AbiPassingSegment segment)
    {
        assert(segment.IsPassedInRegister);

        switch (descriptor.Type)
        {
            case TYP_BYREF:
            case TYP_REF:
            {
                assert((segment.Offset == 0) && (segment.Size == Configuration.TARGET_POINTER_SIZE));
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
                if (((segment.Offset % Configuration.TARGET_POINTER_SIZE) == 0) &&
                    (segment.Size == Configuration.TARGET_POINTER_SIZE))
                {
                    return layout.GetGCPtrType(segment.Offset / Configuration.TARGET_POINTER_SIZE);
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
        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** In genHomeRegisterParams()\n");
        }
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

    private unsafe void genFnEpilogWasm(BasicBlock block)
    {
        var nextBlock = block.Next;

        if (Configuration.DEBUG)
        {
            if (_verbose)
            {
                jitprintf("*************** In genFnEpilog()\n");
            }

            if (_compiler.opts.dspCode)
            {
                jitprintf("\n__epilog:\n");
            }
        }
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
            assert(stackPointer is not REG_WASM_NA);

            var frameBase = (framePointer is not REG_WASM_NA) ? framePointer : stackPointer;
            Emitter.emitIns_I(INS_local_get, EA_PTRSIZE,
                unchecked((nint)regNumberExtensions.WasmRegToIndex(frameBase)));
            if (genTotalFrameSize != 0)
            {
                Emitter.emitIns_I(WasmInstructions.INS_I_const, EA_PTRSIZE, unchecked((nint)genTotalFrameSize));
                Emitter.emitIns(WasmInstructions.INS_I_add);
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
        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }

        Emitter.emitIns(INS_code_size);

        var funcletIndex = _compiler.funCurrentFuncIdx();
        assert(funcletIndex > 0);
        ref var func = ref _compiler.funGetFunc(funcletIndex);
        var localDeclarations = func.funWasmLocalDecls;
        if (localDeclarations is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.WasmFuncletLocalDeclarationsAreMissing);
            return;
        }
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

        var frameSize = roundUp(2 * Configuration.TARGET_POINTER_SIZE, Configuration.STACK_ALIGN);
        _compiler.unwindAllocStack(unchecked((uint)frameSize));

        var stackPointerIndex = unchecked((nint)GetStackPointerRegIndex());
        Emitter.emitIns_I(INS_local_get, EA_PTRSIZE, stackPointerIndex);
        Emitter.emitIns_I(WasmInstructions.INS_I_const, EA_PTRSIZE, frameSize);
        Emitter.emitIns(WasmInstructions.INS_I_sub);
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
