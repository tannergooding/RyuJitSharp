// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForShift(GenTree tree)
    {
        var size = tree.Type.EmitActualSize;

        assert(tree.RegNum != REG_NA);

        genConsumeOperands(tree.AsOp());

        var operand = tree.AsOp().Op1;
        var shiftBy = tree.AsOp().Op2;
        var immWidth = Emitter.getBitWidth(size);

        if (tree.Oper is GT_ROR or GT_ROL)
        {
            if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb))
            {
                var is4 = size == EA_4BYTE;
                var isRightRotate = tree.Oper is GT_ROR;

                if (!shiftBy.Oper.IsCnsIntOrI)
                {
                    var ins = isRightRotate
                        ? is4 ? INS_rorw : INS_ror
                        : is4 ? INS_rolw : INS_rol;

                    GetEmitter().emitIns_R_R_R(ins, size, tree.RegNum, operand.RegNum, shiftBy.RegNum);
                }
                else
                {
                    var shiftByImm = unchecked((uint)shiftBy.AsIntCon().IconValue);
                    assert(shiftByImm < immWidth);

                    if (!isRightRotate)
                    {
                        shiftByImm = immWidth - shiftByImm;
                    }

                    var ins = is4 ? INS_roriw : INS_rori;
                    GetEmitter().emitIns_R_R_I(ins, size, tree.RegNum, operand.RegNum,
                        unchecked((int)shiftByImm));
                }
            }
            else
            {
                var tempReg = InternalRegisters.GetSingle(tree);

                if (!shiftBy.Oper.IsCnsIntOrI)
                {
                    var shiftRight = tree.Oper is GT_ROR ? shiftBy.RegNum : tempReg;
                    var shiftLeft = tree.Oper is GT_ROR ? tempReg : shiftBy.RegNum;

                    GetEmitter().emitIns_R_R_I(INS_addi, size, tempReg, REG_R0, unchecked((int)immWidth));
                    GetEmitter().emitIns_R_R_R(INS_sub, size, tempReg, tempReg, shiftBy.RegNum);

                    if (size == EA_8BYTE)
                    {
                        GetEmitter().emitIns_R_R_R(INS_srl, size, REG_RA, operand.RegNum, shiftRight);
                        GetEmitter().emitIns_R_R_R(INS_sll, size, tempReg, operand.RegNum, shiftLeft);
                    }
                    else
                    {
                        GetEmitter().emitIns_R_R_R(INS_srlw, size, REG_RA, operand.RegNum, shiftRight);
                        GetEmitter().emitIns_R_R_R(INS_sllw, size, tempReg, operand.RegNum, shiftLeft);
                    }
                }
                else
                {
                    var shiftByImm = unchecked((uint)shiftBy.AsIntCon().IconValue);
                    if ((shiftByImm >= 32) && (shiftByImm < 64))
                    {
                        immWidth = 64;
                    }

                    var shiftRight = tree.Oper is GT_ROR ? shiftByImm : immWidth - shiftByImm;
                    var shiftLeft = tree.Oper is GT_ROR ? immWidth - shiftByImm : shiftByImm;

                    if (((shiftByImm >= 32) && (shiftByImm < 64)) || (size == EA_8BYTE))
                    {
                        GetEmitter().emitIns_R_R_I(INS_srli, size, REG_RA, operand.RegNum,
                            unchecked((int)shiftRight));
                        GetEmitter().emitIns_R_R_I(INS_slli, size, tempReg, operand.RegNum,
                            unchecked((int)shiftLeft));
                    }
                    else
                    {
                        GetEmitter().emitIns_R_R_I(INS_srliw, size, REG_RA, operand.RegNum,
                            unchecked((int)shiftRight));
                        GetEmitter().emitIns_R_R_I(INS_slliw, size, tempReg, operand.RegNum,
                            unchecked((int)shiftLeft));
                    }
                }

                GetEmitter().emitIns_R_R_R(INS_or, size, tree.RegNum, REG_RA, tempReg);
            }
        }
        else if (!shiftBy.Oper.IsCnsIntOrI)
        {
            var ins = genGetInsForOper(tree);
            GetEmitter().emitIns_R_R_R(ins, size, tree.RegNum, operand.RegNum, shiftBy.RegNum);
        }
        else
        {
            assert(isImmed(tree));

            var ins = genGetInsForOper(tree);
            var shiftByImm = unchecked((uint)shiftBy.AsIntCon().IconValue);
            shiftByImm &= (immWidth - 1);

            if ((ins is INS_slliw) && (shiftByImm >= 32))
            {
                ins = INS_slli;
            }
            else if ((ins is INS_slli) && (shiftByImm >= 32) && (shiftByImm < 64))
            {
                ins = INS_slli;
            }
            else if ((ins is INS_srai) && (shiftByImm >= 32) && (shiftByImm < 64))
            {
                ins = INS_srai;
            }
            else if ((ins is INS_srli) && (shiftByImm >= 32) && (shiftByImm < 64))
            {
                ins = INS_srli;
            }

            GetEmitter().emitIns_R_R_I(ins, size, tree.RegNum, operand.RegNum, unchecked((int)shiftByImm));
        }

        genProduceReg(tree);
    }
}
#endif
