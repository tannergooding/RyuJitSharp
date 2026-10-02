// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForBswap(GenTree tree)
    {
        assert(tree.Oper is GT_BSWAP or GT_BSWAP16);

        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        var operand = tree.AsUnOp().Op1;
        assert(operand.IsUsedFromReg);
        var operandReg = genConsumeReg(operand);

        if (tree.Oper is GT_BSWAP)
        {
            inst_RV_RV(INS_rev, targetReg, operandReg, targetType);
        }
        else
        {
            inst_RV_RV(INS_rev16, targetReg, operandReg, targetType);

            if (!genCanOmitNormalizationForBswap16(tree))
            {
                Emitter.emitIns_Mov(INS_uxth, EA_4BYTE, targetReg, targetReg, canSkip: false);
            }
        }

        genProduceReg(tree);
    }
}
#endif
