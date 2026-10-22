// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genStoreSimd12ToStack(regNumber dataReg, regNumber tmpReg)
    {
        EnsureX86Simd12Support();

        assert(genIsValidFloatReg(dataReg));
        assert(genIsValidFloatReg(tmpReg));

        Emitter.emitIns_AR_R(INS_movsd_simd, EA_8BYTE, dataReg, TargetRuntimeMetadata.StackPointerBaseRegister, 0);
        Emitter.emitIns_R_R(INS_movhlps, EA_16BYTE, tmpReg, dataReg);
        Emitter.emitIns_AR_R(INS_movss, EA_4BYTE, tmpReg, TargetRuntimeMetadata.StackPointerBaseRegister, 8);
    }

    public void genPutArgStkSimd12(GenTreePutArgStk tree)
    {
        EnsureX86Simd12Support();

        assert(tree.Oper is GT_PUTARG_STK);

        var data = tree.Data;
        assert(!data.IsContained);

        var dataReg = genConsumeReg(data);
        var tmpReg = _internalRegisters.GetSingle(tree);

        genStoreSimd12ToStack(dataReg, tmpReg);
    }

    private static void EnsureX86Simd12Support()
    {
        if (!Configuration.TARGET_X86 || !Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.X86SIMD12StackArgumentsAreUnsupportedForThisTarget
    );
            return;
        }
    }
}
