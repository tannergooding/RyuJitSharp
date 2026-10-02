// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForNegNot(GenTreeUnOp tree)
    {
        assert(tree.Oper is GT_NEG or GT_NOT);

        var targetType = tree.Type;
        assert((tree.Oper is not GT_NOT) || !varTypeIsFloating(targetType));

        var targetReg = tree.RegNum;
        var ins = genGetInsForOper(tree.Oper, targetType);

        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);

        if (ins is INS_vneg)
        {
            Emitter.emitIns_R_R(ins, targetType.EmitSize, targetReg, operandReg);
        }
        else
        {
            Emitter.emitIns_R_R_I(ins, targetType.EmitSize, targetReg, operandReg, 0, INS_FLAGS_SET);
        }

        genProduceReg(tree);
    }
}
#endif
