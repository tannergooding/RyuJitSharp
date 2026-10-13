// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForShiftArmArch(GenTree tree)
    {
        var targetType = tree.Type;
        var oper = tree.Oper;
        var ins = genGetInsForOper(oper, targetType);
        var size = targetType.EmitActualSize;
        var dstReg = tree.RegNum;

        assert(dstReg != regNumberExtensions.NotAvailable);

        genConsumeOperands(tree.AsOp());

        var operand = tree.AsOp().Op1;
        var shiftBy = tree.AsOp().Op2;
        if (!shiftBy.Oper.IsCnsIntOrI)
        {
            Emitter.emitIns_R_R_R(ins, size, dstReg, operand.RegNum, shiftBy.RegNum);
        }
        else
        {
            assert((int)size <= (int)EA_8BYTE);
            var immWidth = unchecked((uint)size * (uint)BITS_PER_BYTE);
            var shiftByImm = unchecked((uint)shiftBy.AsIntCon().IconValue) & (immWidth - 1);
            if (Configuration.TARGET_ARM)
            {
                Emitter.emitIns_R_R_I(
                    ins, size, dstReg, operand.RegNum, unchecked((int)shiftByImm), INS_FLAGS_DONT_CARE);
            }
            else
            {
                Emitter.emitIns_R_R_I(ins, size, dstReg, operand.RegNum, unchecked((nint)shiftByImm));
            }
        }

        genProduceReg(tree);
    }
}
