// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForMulLong(GenTreeOp mul)
    {
        assert(mul.Oper is GT_MUL_LONG);
        genConsumeOperands(mul);

        var op1 = mul.Op1;
        var op2 = mul.Op2;
        var ins = mul.IsUnsigned ? INS_umull : INS_smull;

#if TARGET_ARM
        Emitter.emitIns_R_R_R_R(ins, EA_4BYTE, mul.RegNum, mul.AsMultiRegOp().OtherReg,
            op1.RegNum, op2.RegNum);
#else
        Emitter.emitIns_R_R_R(ins, EA_8BYTE, mul.RegNum, op1.RegNum, op2.RegNum);
#endif

        genProduceReg(mul);
    }
}
#endif
