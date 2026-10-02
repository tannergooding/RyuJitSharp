// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForBinary(GenTreeOp treeNode)
    {
        var oper = treeNode.Oper;
        var targetReg = treeNode.RegNum;
        var targetType = treeNode.Type;
        var emitter = Emitter;

        assert(oper is GT_ADD or GT_SUB or GT_MUL or GT_ADD_LO or GT_ADD_HI or GT_SUB_LO or GT_SUB_HI or GT_OR or GT_XOR or GT_AND or GT_AND_NOT);

        var op1 = treeNode.Op1;
        var op2 = treeNode.Op2;

        var ins = genGetInsForOper(oper, targetType);

        noway_assert(targetReg != REG_NA);

        if (oper is GT_ADD_LO or GT_SUB_LO)
        {
            assert(!op1.IsContained && !op2.IsContained);
            emitter.emitIns_R_R_R(ins, targetType.EmitSize, treeNode.RegNum, op1.RegNum, op2.RegNum, INS_FLAGS_SET);
        }
        else
        {
            var reg = emitter.emitInsTernary(ins, targetType.EmitSize, treeNode, op1, op2);
            assert(reg == targetReg);
        }

        genProduceReg(treeNode);
    }
}
#endif
