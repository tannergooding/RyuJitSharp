// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
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

        assert(oper is GT_ADD or GT_SUB or GT_MUL or GT_AND or GT_AND_NOT or GT_OR or GT_OR_NOT
            or GT_XOR or GT_XOR_NOT or GT_BIT_SET or GT_BIT_CLEAR or GT_BIT_INVERT);

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
        var ins = INS_ebreak;

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

                default:
                {
                    NO_WAY("Unhandled oper in genGetInsForOper() - float");
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
                            ins = INS_addi;
                        }
                        else
                        {
                            assert(attr == EA_4BYTE);
                            ins = INS_addiw;
                        }
                    }
                    else
                    {
                        if (attr is EA_8BYTE or EA_BYREF)
                        {
                            ins = INS_add;
                        }
                        else
                        {
                            assert(attr == EA_4BYTE);
                            ins = INS_addw;
                        }
                    }
                    break;
                }

                case GT_SUB:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_sub;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_subw;
                    }
                    break;
                }

                case GT_MOD:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_rem;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_remw;
                    }
                    break;
                }

                case GT_DIV:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_div;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_divw;
                    }
                    break;
                }

                case GT_UMOD:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_remu;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_remuw;
                    }
                    break;
                }

                case GT_UDIV:
                {
                    if (attr is EA_8BYTE or EA_BYREF)
                    {
                        ins = INS_divu;
                    }
                    else
                    {
                        assert(attr == EA_4BYTE);
                        ins = INS_divuw;
                    }
                    break;
                }

                case GT_MUL:
                {
                    if ((attr is EA_8BYTE or EA_BYREF))
                    {
                        var operands = treeNode.AsOp();
                        ins = genActualTypeIsInt(operands.Op1.Type) && genActualTypeIsInt(operands.Op2.Type)
                            ? INS_mulw
                            : INS_mul;
                    }
                    else
                    {
                        ins = INS_mulw;
                    }
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
                    assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb));
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

                case GT_OR_NOT:
                {
                    assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb));
                    assert(!isImmed(treeNode));
                    ins = INS_orn;
                    break;
                }

                case GT_LSH:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        ins = attr == EA_4BYTE ? INS_slliw : INS_slli;
                    }
                    else
                    {
                        ins = attr == EA_4BYTE ? INS_sllw : INS_sll;
                    }
                    break;
                }

                case GT_RSZ:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        ins = attr == EA_4BYTE ? INS_srliw : INS_srli;
                    }
                    else
                    {
                        ins = attr == EA_4BYTE ? INS_srlw : INS_srl;
                    }
                    break;
                }

                case GT_RSH:
                {
                    isImm = isImmed(treeNode);
                    if (isImm)
                    {
                        ins = attr == EA_4BYTE ? INS_sraiw : INS_srai;
                    }
                    else
                    {
                        ins = attr == EA_4BYTE ? INS_sraw : INS_sra;
                    }
                    break;
                }

                case GT_ROR:
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "GT_ROR-----unimplemented/unused on RISCV64 yet----");
                }

                case GT_XOR:
                {
                    isImm = isImmed(treeNode);
                    ins = isImm ? INS_xori : INS_xor;
                    break;
                }

                case GT_SH1ADD:
                {
                    ins = INS_sh1add;
                    break;
                }

                case GT_SH2ADD:
                {
                    ins = INS_sh2add;
                    break;
                }

                case GT_SH3ADD:
                {
                    ins = INS_sh3add;
                    break;
                }

                case GT_SH1ADD_UW:
                {
                    ins = INS_sh1add_uw;
                    break;
                }

                case GT_SH2ADD_UW:
                {
                    ins = INS_sh2add_uw;
                    break;
                }

                case GT_SH3ADD_UW:
                {
                    ins = INS_sh3add_uw;
                    break;
                }

                case GT_XOR_NOT:
                {
                    assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb));
                    assert(!isImmed(treeNode));
                    ins = INS_xnor;
                    break;
                }

                case GT_BIT_SET:
                {
                    assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbs));
                    ins = isImmed(treeNode) ? INS_bseti : INS_bset;
                    break;
                }

                case GT_BIT_CLEAR:
                {
                    assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbs));
                    ins = isImmed(treeNode) ? INS_bclri : INS_bclr;
                    break;
                }

                case GT_BIT_INVERT:
                {
                    assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbs));
                    ins = isImmed(treeNode) ? INS_binvi : INS_binv;
                    break;
                }

                default:
                {
                    NO_WAY("Unhandled oper in genGetInsForOper() - integer");
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
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V ternary instruction recording is not ported.");
    }
}
#endif
