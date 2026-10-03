// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && FEATURE_SIMD
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genStoreSimd12ToStack(regNumber dataReg, regNumber tmpReg)
    {
        assert(genIsValidFloatReg(dataReg));
        assert(genIsValidFloatReg(tmpReg));

        Emitter.emitIns_AR_R(INS_movsd_simd, EA_8BYTE, dataReg, REG_SPBASE, 0);
        Emitter.emitIns_R_R(INS_movhlps, EA_16BYTE, tmpReg, dataReg);
        Emitter.emitIns_AR_R(INS_movss, EA_4BYTE, tmpReg, REG_SPBASE, 8);
    }

    public void genPutArgStkSimd12(GenTreePutArgStk tree)
    {
        assert(tree.Oper is GT_PUTARG_STK);

        var data = tree.Data;
        assert(!data.IsContained);

        var dataReg = genConsumeReg(data);
        var tmpReg = _internalRegisters.GetSingle(tree);

        genStoreSimd12ToStack(dataReg, tmpReg);
    }
}
#endif
