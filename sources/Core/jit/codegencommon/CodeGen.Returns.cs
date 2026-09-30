// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_X86 || TARGET_ARM
    public void genLongReturn(GenTree tree)
    {
        assert(tree.Oper is GT_RETURN or GT_RETFILT);
        assert(tree.Type == TYP_LONG);

        var pair = tree.AsUnOp().Op1;
        assert(pair.Oper == GT_LONG);
        var low = pair.AsOp().Op1;
        var high = pair.AsOp().Op2;
        assert((low.RegNum != REG_NA) && (high.RegNum != REG_NA));

        _ = genConsumeReg(low);
        _ = genConsumeReg(high);

        inst_Mov(tree.Type, REG_LNGRET_LO, low.RegNum, canSkip: true, TYP_INT.EmitActualSize);
        inst_Mov(tree.Type, REG_LNGRET_HI, high.RegNum, canSkip: true, TYP_INT.EmitActualSize);
    }
#endif

    public void genReturn(GenTree tree)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        var value = tree.Oper == GT_SWIFT_ERROR_RET ? tree.AsOp().Op2 : tree.AsUnOp().Op1;
        var type = tree.Type;
        assert((tree.Oper != GT_RETFILT) || (type is TYP_VOID or TYP_INT));
        assert((type != TYP_VOID) || (value is null));

#if TARGET_X86 || TARGET_ARM
        if (type == TYP_LONG)
        {
            genLongReturn(tree);
        }
        else
#endif
        if (isStructReturn(tree))
        {
            genStructReturn(tree);
        }
        else if (type != TYP_VOID)
        {
            assert(value is not null);
#if HAS_FIXED_REGISTER_SET
            noway_assert(value.RegNum != REG_NA);
#endif
            // Consumption clears dead operand roots. Restore the ABI return roots below
            // before any profiler callback can observe the return value.
            _ = genConsumeReg(value);

#if HAS_FIXED_REGISTER_SET
#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            genSimpleReturn(tree);
#else
#if TARGET_X86
            if (varTypeUsesFloatReg(type))
            {
                genFloatReturn(tree);
            }
            else
#elif TARGET_ARM
            if (varTypeUsesFloatReg(type) && (_compiler.opts.compUseSoftFP || _compiler.info.compIsVarArgs))
            {
                if (type == TYP_FLOAT)
                {
                    Emitter.emitIns_Mov(INS_vmov_f2i, EA_4BYTE, REG_INTRET, value.RegNum, canSkip: false);
                }
                else
                {
                    assert(type == TYP_DOUBLE);
                    Emitter.emitIns_R_R_R(INS_vmov_d2i, EA_8BYTE, REG_INTRET, REG_R1, value.RegNum,
                        insFlags.INS_FLAGS_DONT_CARE);
                }
            }
            else
#endif
            {
                regNumber retReg;
                if (varTypeUsesIntReg(type))
                {
                    retReg = REG_INTRET;
                }
                else
                {
                    assert(varTypeUsesFloatReg(type));
                    retReg = REG_FLOATRET;
                }
                inst_Mov_Extend(type, srcInReg: true, retReg, value.RegNum, canSkip: true, EA_UNKNOWN);
            }
#endif
#endif
        }

#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
        if (tree.Oper is GT_RETURN or GT_SWIFT_ERROR_RET)
        {
            genMarkReturnGCInfo();
        }
#endif
#if PROFILING_SUPPORTED
        if ((tree.Oper is GT_RETURN or GT_SWIFT_ERROR_RET) && _compiler.compIsProfilerHookNeeded)
        {
            genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);
        }
#endif
        if ((tree.Oper == GT_RETURN) && _compiler.compIsAsync)
        {
#if TARGET_WASM
            genClearAsyncContinuationGlobal();
#else
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_ASYNC_CONTINUATION_RET);
            GCInfo.gcMarkRegPtrVal(REG_ASYNC_CONTINUATION_RET, TYP_REF);
#endif
        }
#if DEBUG && TARGET_XARCH
        var checkStackPointer = _compiler.opts.compStackCheckOnRet;
        if (_compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT)
        {
            checkStackPointer = false;
        }
        genStackPointerCheck(checkStackPointer, _compiler.lvaReturnSpCheck);
#endif
    }

    public void genMarkReturnGCInfo()
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Return GC information requires AMD64.");
#else
        var descriptor = _compiler.compRetTypeDesc;
        if (_compiler.compMethodReturnsRetBufAddr)
        {
            GCInfo.gcMarkRegPtrVal(REG_INTRET, TYP_BYREF);
        }
        else
        {
            var count = descriptor.ReturnRegCount;
            for (byte i = 0; i < count; i++)
            {
                GCInfo.gcMarkRegPtrVal(descriptor.GetAbiReturnReg(i, _compiler.info.compCallConv),
                    descriptor.GetReturnRegType(i));
            }
        }
#endif
    }

#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
    public void genSimpleReturn(GenTree tree)
    {
#if TARGET_ARM64
        assert(tree.Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        var value = tree.Oper == GT_SWIFT_ERROR_RET ? tree.AsOp().Op2 : tree.AsUnOp().Op1;
#else
        assert(tree.Oper is GT_RETURN or GT_RETFILT);
        var value = tree.AsUnOp().Op1;
#endif
        var type = tree.Type;
        assert(type is not (TYP_STRUCT or TYP_VOID));

        var returnReg = varTypeUsesFloatArgReg(type) ? REG_FLOATRET : REG_INTRET;
        var movRequired = value.RegNum != returnReg;
        if (!movRequired && (value.Oper == GT_LCL_VAR))
        {
            ref var local = ref _compiler.lvaGetDesc(value.AsLclVarCommon().LclNum);
            if (local.lvIsRegCandidate && ((value.Flags & GTF_SPILLED) == 0))
            {
                if (value.Type.ActualType.Size < local.Type.ActualType.Size)
                {
                    movRequired = true;
                }
            }
        }

        var size = type.EmitActualSize;
#if TARGET_ARM64
        Emitter.emitIns_Mov(INS_mov, size, returnReg, value.RegNum, canSkip: !movRequired);
#else
        if (movRequired)
        {
#if TARGET_LOONGARCH64
            if (varTypeUsesFloatArgReg(type))
            {
                var ins = size == EA_4BYTE ? INS_fmov_s : INS_fmov_d;
                Emitter.emitIns_R_R(ins, size, returnReg, value.RegNum);
            }
            else
            {
                var ins = size == EA_4BYTE ? INS_slli_w : INS_ori;
                Emitter.emitIns_R_R_I(ins, size, returnReg, value.RegNum, 0);
            }
#else
            if (varTypeUsesFloatArgReg(type))
            {
                var ins = size == EA_4BYTE ? INS_fsgnj_s : INS_fsgnj_d;
                Emitter.emitIns_R_R_R(ins, size, returnReg, value.RegNum, value.RegNum);
            }
            else
            {
                var ins = size == EA_4BYTE ? INS_sext_w : INS_mov;
                Emitter.emitIns_R_R(ins, size, returnReg, value.RegNum);
            }
#endif
        }
#endif
    }
#endif

#if TARGET_WASM
    public void genClearAsyncContinuationGlobal()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm async continuation clearing is not implemented.");
    }
#endif

    public bool isStructReturn(GenTree tree)
    {
        noway_assert(tree.Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        if (tree.Oper is not (GT_RETURN or GT_SWIFT_ERROR_RET))
        {
            return false;
        }
        var value = tree.Oper == GT_SWIFT_ERROR_RET ? tree.AsOp().Op2 : tree.AsUnOp().Op1;
        if ((tree.Type != TYP_VOID) && (value.Oper == GT_FIELD_LIST))
        {
            return true;
        }

#if TARGET_AMD64 && !UNIX_AMD64_ABI
        assert(!varTypeIsStruct(tree.Type));
        return false;
#else
        return varTypeIsStruct(tree.Type) && (_compiler.info.compRetNativeType == TYP_STRUCT);
#endif
    }

    public void genStructReturn(GenTree tree)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "WebAssembly struct-return generation is not implemented.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
        var value = tree.Oper == GT_SWIFT_ERROR_RET ? tree.AsOp().Op2 : tree.AsUnOp().Op1;
        var actualValue = value.SkipCopyOrReload;
        var descriptor = _compiler.compRetTypeDesc;
        assert(descriptor.ReturnRegCount <= MAX_RET_REG_COUNT);

        if (value.Oper == GT_FIELD_LIST)
        {
            byte index = 0;
            foreach (var use in value.AsFieldList().Uses)
            {
                var sourceReg = genConsumeReg(use.Node);
                var destReg = descriptor.GetAbiReturnReg(index, _compiler.info.compCallConv);
                var type = descriptor.GetReturnRegType(index);
                inst_Mov(type, destReg, sourceReg, canSkip: true, type.EmitActualSize);
                index++;
            }
            return;
        }

        genConsumeRegs(value);

#if FEATURE_MULTIREG_RET
        if (genIsRegCandidateLocal(actualValue))
        {
            assert(varTypeIsSimd(_compiler.lvaGetDesc(actualValue.AsLclVarCommon().LclNum).GetRegisterType()));
            assert(!actualValue.AsLclVar().IsMultiReg);
#if FEATURE_SIMD
            genSIMDSplitReturn(value, descriptor);
#endif
        }
        else if ((actualValue.Oper == GT_LCL_VAR) && !actualValue.AsLclVar().IsMultiReg)
        {
            var local = actualValue.AsLclVar();
            assert(_compiler.lvaGetDesc(local.LclNum).lvIsMultiRegRet);
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            var type = descriptor.GetReturnRegType(0);
            var offset = unchecked((uint)descriptor.GetReturnFieldOffset(0));
            var register = descriptor.GetAbiReturnReg(0, _compiler.info.compCallConv);

            Emitter.emitIns_R_S(ins_Load(type), type.EmitSize, register, local.LclNum, unchecked((int)offset));
            if (descriptor.ReturnRegCount > 1)
            {
                assert(descriptor.ReturnRegCount == 2);
                assert(unchecked(offset + (uint)type.Size) <=
                    unchecked((uint)descriptor.GetReturnFieldOffset(1)));
                type = descriptor.GetReturnRegType(1);
                offset = unchecked((uint)descriptor.GetReturnFieldOffset(1));
                register = descriptor.GetAbiReturnReg(1, _compiler.info.compCallConv);

                Emitter.emitIns_R_S(ins_Load(type), type.EmitSize, register, local.LclNum, unchecked((int)offset));
            }
#else
#if SWIFT_SUPPORT
            ReadOnlySpan<int> offsets = default;
            if (_compiler.info.compCallConv == CorInfoCallConvExtension.Swift)
            {
                unsafe
                {
                    var retType = _compiler.info.compMethodInfo->args.retTypeClass;
                    ref readonly var lowering = ref _compiler.GetSwiftLowering(retType);
                    assert(!lowering.byReference && (descriptor.ReturnRegCount == lowering.numLoweredElements));
                    offsets = lowering.offsets;
                }
            }
#endif
            var offset = 0;
            for (byte i = 0; i < descriptor.ReturnRegCount; i++)
            {
                var type = descriptor.GetReturnRegType(i);
                var register = descriptor.GetAbiReturnReg(i, _compiler.info.compCallConv);
#if SWIFT_SUPPORT
                if (!offsets.IsEmpty)
                {
                    offset = offsets[i];
                }
#endif
                Emitter.emitIns_R_S(ins_Load(type), type.EmitSize, register, local.LclNum, offset);
                offset += type.Size;
            }
#endif
        }
        else
        {
            for (byte i = 0; i < descriptor.ReturnRegCount; i++)
            {
                var type = descriptor.GetReturnRegType(i);
                var register = descriptor.GetAbiReturnReg(i, _compiler.info.compCallConv);
                var fromReg = value.GetRegByIndex(i);
                if ((fromReg == REG_NA) && (value.Oper == GT_COPY))
                {
                    fromReg = actualValue.GetRegByIndex(i);
                }

                if (fromReg == REG_NA)
                {
                    var local = actualValue.AsLclVar();
                    ref var localDesc = ref _compiler.lvaGetDesc(local.LclNum);
                    assert(localDesc.lvPromoted);
                    var fieldNumber = localDesc.lvFieldLclStart + i;
                    assert(_compiler.lvaGetDesc(fieldNumber).lvOnFrame);
                    Emitter.emitIns_R_S(ins_Load(type), type.EmitSize, register, fieldNumber, 0);
                }
                else
                {
                    inst_Mov(type, register, fromReg, canSkip: true);
                }
            }
        }
#else
        unreached();
#endif
#endif
    }

    public void genSIMDSplitReturn(GenTree source, ReturnTypeDesc descriptor)
    {
#if !FEATURE_SIMD || (!TARGET_XARCH && !TARGET_ARMARCH)
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD split returns require xarch SIMD support.");
#else
        assert(varTypeIsSimd(source.Type) && source.IsUsedFromReg);
        var sourceReg = source.RegNum;
#if TARGET_ARMARCH
        var count = descriptor.ReturnRegCount;
        // Extract source lane zero first. If source aliases a destination, writing its lane zero
        // cannot destroy any source lanes needed by later iterations.
        for (byte index = 0; index < count; index++)
        {
            var type = descriptor.GetReturnRegType(index);
            var register = descriptor.GetAbiReturnReg(index, _compiler.info.compCallConv);
            if (varTypeIsFloating(type))
            {
                Emitter.emitIns_R_R_I_I(INS_mov, type.EmitSize, register, sourceReg, 0, (nint)index);
            }
            else
            {
                Emitter.emitIns_R_R_I(INS_mov, type.EmitSize, register, sourceReg, (nint)index);
            }
        }
#else
        var firstReg = descriptor.GetAbiReturnReg(0, _compiler.info.compCallConv);
        var secondReg = descriptor.GetAbiReturnReg(1, _compiler.info.compCallConv);
        assert((firstReg != REG_NA) && (secondReg != REG_NA) && (sourceReg != REG_NA));
#if TARGET_AMD64
        assert(source.Type == TYP_SIMD16 && genIsValidFloatReg(sourceReg));
        assert(genIsValidFloatReg(firstReg) && (firstReg != secondReg));

        inst_Mov(TYP_SIMD16, firstReg, sourceReg, canSkip: true);
        Emitter.emitIns_SIMD_R_R_R(INS_movhlps, EA_16BYTE, secondReg, secondReg, sourceReg, INS_OPTS_NONE);
#else
        assert(genIsValidFloatReg(sourceReg));
        assert(source.Type == TYP_SIMD8);
        assert(!genIsValidFloatReg(firstReg));
        assert((firstReg == REG_EAX) && (secondReg == REG_EDX));

        inst_Mov(TYP_INT, firstReg, sourceReg, canSkip: false);
        inst_RV_TT_IV(INS_pextrd, EA_4BYTE, secondReg, source, 1, INS_OPTS_NONE);
#endif
#endif
#endif
    }

#if SWIFT_SUPPORT
    public void genSwiftErrorReturn(GenTree tree)
    {
        assert(tree.Oper == GT_SWIFT_ERROR_RET);
        var swiftError = tree.AsOp().Op1;
        var sourceReg = genConsumeReg(swiftError);
        inst_Mov(swiftError.Type, REG_SWIFT_ERROR, sourceReg, canSkip: true, EA_PTRSIZE);
        genReturn(tree);
    }
#endif

#if DEBUG && TARGET_XARCH
    public void genStackPointerCheck(bool doStackPointerCheck, int stackPointerVar,
        nint offset = 0, regNumber tempReg = REG_NA)
    {
        if (doStackPointerCheck)
        {
            Emitter.RequireSupportedInstructionRecording();
            assert(stackPointerVar != BAD_VAR_NUM);
            var local = _compiler.lvaGetDesc(stackPointerVar);
            assert(local.lvDoNotEnregister);
            assert(local.lvOnFrame);
            if (offset != 0)
            {
                assert(tempReg != REG_NA);
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, tempReg, REG_SPBASE, canSkip: false);
                Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, tempReg, offset);
                Emitter.emitIns_S_R(INS_cmp, EA_PTRSIZE, tempReg, stackPointerVar, 0);
            }
            else
            {
                Emitter.emitIns_S_R(INS_cmp, EA_PTRSIZE, REG_SPBASE, stackPointerVar, 0);
            }

            var label = genCreateTempLabel();
            Emitter.emitIns_J(INS_je, label);
            instGen(INS_int3);
            genDefineTempLabel(label);
        }
    }
#endif
}
