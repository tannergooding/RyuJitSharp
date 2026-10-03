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
            ref readonly var abiInfo = ref _compiler.lvaGetParameterABIInfo(_compiler.lvaWasmSpArg);
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
}
#endif
