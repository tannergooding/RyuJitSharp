// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForBinary(GenTreeOp treeNode)
    {
        var oper = treeNode.Oper;
        var targetReg = treeNode.RegNum;

        assert(oper is GT_ADD or GT_SUB or GT_MUL or GT_AND or GT_AND_NOT or GT_OR or GT_XOR);

        var op1 = treeNode.Op1;
        var op2 = treeNode.Op2;
        var ins = genGetInsForOper(treeNode);

        assert(targetReg != REG_NA);

        var resultReg = genEmitTernaryInstruction(ins, treeNode.Type.EmitActualSize, treeNode, op1, op2);
        assert(resultReg == targetReg);

        genProduceReg(treeNode);
    }

    public instruction genGetInsForOper(GenTree treeNode)
    {
        var type = treeNode.Type;
        var oper = treeNode.Oper;
        var attr = treeNode.Type.EmitActualSize;
        var isImm = false;
        var ins = INS_break;

        if (varTypeIsFloating(type))
        {
            switch (oper)
            {
                case GT_ADD:
                {
                    ins = attr == EA_4BYTE ? INS_fadd_s : INS_fadd_d;
                    break;
                }

                case GT_SUB:
                {
                    ins = attr == EA_4BYTE ? INS_fsub_s : INS_fsub_d;
                    break;
                }

                case GT_MUL:
                {
                    ins = attr == EA_4BYTE ? INS_fmul_s : INS_fmul_d;
                    break;
                }

                case GT_DIV:
                {
                    ins = attr == EA_4BYTE ? INS_fdiv_s : INS_fdiv_d;
                    break;
                }

                case GT_NEG:
                {
                    ins = attr == EA_4BYTE ? INS_fneg_s : INS_fneg_d;
                    break;
                }

                default:
                {
                    NYI("Unhandled oper in genGetInsForOper() - float");
                    unreached();
                    break;
                }
            }
        }
        else
        {
            switch (oper)
            {
                case GT_ADD:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        if (attr is EA_8BYTE or EA_BYREF)
                        {
                            ins = INS_addi_d;
                        }
                        else
                        {
                            assert(attr == EA_4BYTE);
                            ins = INS_addi_w;
                        }
                    }
                    else
                    {
                        if (attr is EA_8BYTE or EA_BYREF)
                        {
                            ins = INS_add_d;
                        }
                        else
                        {
                            assert(attr == EA_4BYTE);
                            ins = INS_add_w;
                        }
                    }
                    break;
                }

                case GT_SUB:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_sub_d;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_sub_w;
                    }
                    break;
                }

                case GT_MOD:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_mod_d;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_mod_w;
                    }
                    break;
                }

                case GT_DIV:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_div_d;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_div_w;
                    }
                    break;
                }

                case GT_UMOD:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_mod_du;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_mod_wu;
                    }
                    break;
                }

                case GT_UDIV:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_div_du;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_div_wu;
                    }
                    break;
                }

                case GT_MUL:
                {
                    ins = (attr is EA_8BYTE or EA_BYREF) ? INS_mul_d : INS_mul_w;
                    break;
                }

                case GT_NEG:
                {
                    if (attr == EA_8BYTE)
                    {
                        ins = INS_dneg;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_neg;
                    }
                    break;
                }

                case GT_NOT:
                {
                    ins = INS_not;
                    break;
                }

                case GT_AND:
                {
                    isImm = isImmed(treeNode);
                    ins = isImm ? INS_andi : INS_and;
                    break;
                }

                case GT_AND_NOT:
                {
                    assert(!isImmed(treeNode));
                    ins = INS_andn;
                    break;
                }

                case GT_OR:
                {
                    isImm = isImmed(treeNode);
                    ins = isImm ? INS_ori : INS_or;
                    break;
                }

                case GT_LSH:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        ins = attr == EA_4BYTE ? INS_slli_w : INS_slli_d;
                    }
                    else
                    {
                        ins = attr == EA_4BYTE ? INS_sll_w : INS_sll_d;
                    }
                    break;
                }

                case GT_RSZ:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        ins = attr == EA_4BYTE ? INS_srli_w : INS_srli_d;
                    }
                    else
                    {
                        ins = attr == EA_4BYTE ? INS_srl_w : INS_srl_d;
                    }
                    break;
                }

                case GT_RSH:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        ins = attr == EA_4BYTE ? INS_srai_w : INS_srai_d;
                    }
                    else
                    {
                        ins = attr == EA_4BYTE ? INS_sra_w : INS_sra_d;
                    }
                    break;
                }

                case GT_ROR:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        ins = attr == EA_4BYTE ? INS_rotri_w : INS_rotri_d;
                    }
                    else
                    {
                        ins = attr == EA_4BYTE ? INS_rotr_w : INS_rotr_d;
                    }
                    break;
                }

                case GT_XOR:
                {
                    isImm = isImmed(treeNode);
                    ins = isImm ? INS_xori : INS_xor;
                    break;
                }

                default:
                {
                    NYI("Unhandled oper in genGetInsForOper() - integer");
                    unreached();
                    break;
                }
            }
        }

        return ins;
    }

    private static bool isImmed(GenTree treeNode)
    {
        assert(treeNode.Oper.IsBinary);

        return treeNode.AsOp().Op2.IsContainedIntOrIImmed;
    }

    private regNumber genEmitTernaryInstruction(instruction ins, emitAttr attr, GenTree destination,
        GenTree source1, GenTree source2)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 ternary instruction recording is not ported.");
    }
}
#endif
