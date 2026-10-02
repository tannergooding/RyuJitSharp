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

    public uint GetFramePointerRegIndex()
    {
        var fpReg = GetFramePointerReg(_compiler.funCurrentFuncIdx());
        assert(fpReg is not REG_NA);
        return regNumberExtensions.WasmRegToIndex(fpReg);
    }

    public void genEnregisterOSRArgsAndLocals(regNumber initReg, ref bool initRegZeroed)
    {
        unreached();
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
