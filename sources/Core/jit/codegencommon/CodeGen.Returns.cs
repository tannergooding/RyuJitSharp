// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genReturn(GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Return generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        var value = tree.AsUnOp().Op1;
        var type = tree.Type;
        assert((tree.Oper != GT_RETFILT) || (type is TYP_VOID or TYP_INT));
        assert((type != TYP_VOID) || (value is null));

        if (isStructReturn(tree))
        {
            genStructReturn(tree);
        }
        else if (type != TYP_VOID)
        {
            assert(value is not null);
            noway_assert(value.RegNum != REG_NA);
            // Consumption clears dead operand roots. Restore the ABI return roots below
            // before any profiler callback can observe the return value.
            _ = genConsumeReg(value);
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

        if (tree.Oper is GT_RETURN or GT_SWIFT_ERROR_RET)
        {
            genMarkReturnGCInfo();
        }
#if PROFILING_SUPPORTED
        if ((tree.Oper is GT_RETURN or GT_SWIFT_ERROR_RET) && _compiler.compIsProfilerHookNeeded)
        {
            genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);
        }
#endif
        if ((tree.Oper == GT_RETURN) && _compiler.compIsAsync)
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_ASYNC_CONTINUATION_RET);
            GCInfo.gcMarkRegPtrVal(REG_ASYNC_CONTINUATION_RET, TYP_REF);
        }
#if DEBUG
        var checkStackPointer = _compiler.opts.compStackCheckOnRet;
        if (_compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT)
        {
            checkStackPointer = false;
        }
        genStackPointerCheck(checkStackPointer, _compiler.lvaReturnSpCheck);
#endif
#endif
    }

    public void genMarkReturnGCInfo()
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Return GC information requires Windows AMD64.");
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

    public static bool isStructReturn(GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Struct-return classification requires Windows AMD64.");
#else
        noway_assert(tree.Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        if (tree.Oper is not (GT_RETURN or GT_SWIFT_ERROR_RET))
        {
            return false;
        }
        if ((tree.Type != TYP_VOID) && (tree.AsUnOp().Op1.Oper == GT_FIELD_LIST))
        {
            return true;
        }

        assert(!varTypeIsStruct(tree.Type));
        return false;
#endif
    }

    public void genStructReturn(GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Struct-return generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
        var value = tree.AsUnOp().Op1;
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
        unreached();
#endif
    }

#if DEBUG && TARGET_XARCH
    public void genStackPointerCheck(bool doStackPointerCheck, int stackPointerVar,
        nint offset = 0, regNumber tempReg = REG_NA)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-pointer checks require Windows AMD64.");
#else
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
#endif
    }
#endif
}
