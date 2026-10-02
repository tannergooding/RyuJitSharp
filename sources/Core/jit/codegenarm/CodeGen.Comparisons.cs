// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCompare(GenTreeOp tree)
    {
        // TODO-ARM-CQ: Check if we can use the currently set flags.
        // TODO-ARM-CQ: Check for the case where we can simply transfer the carry bit to a register
        //         (signed < or >= where targetReg != REG_NA)
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var op1Type = op1.Type;
        var op2Type = op2.Type;

        assert(!varTypeIsLong(op1Type));
        assert(!varTypeIsLong(op2Type));

        var targetReg = tree.RegNum;
        var emitter = Emitter;

        if (varTypeIsFloating(op1Type))
        {
            assert(op1Type == op2Type);
            _ = emitter.emitInsBinary(INS_vcmp, op1Type.EmitSize, op1, op2);
            emitter.emitIns_R(INS_vmrs, EA_4BYTE, REG_R15);
        }
        else
        {
            assert(!varTypeIsFloating(op2Type));
            var cmpType = (op1Type == op2Type) ? op1Type : TYP_INT;
            _ = emitter.emitInsBinary(INS_cmp, cmpType.EmitSize, op1, op2);
        }

        if (targetReg != REG_NA)
        {
            inst_SETCC(GenCondition.FromRelop(tree), tree.Type, targetReg);
            genProduceReg(tree);
        }
    }
}
#endif
