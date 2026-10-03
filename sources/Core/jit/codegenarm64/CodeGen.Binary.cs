// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForBinary(GenTreeOp treeNode)
    {
        var oper = treeNode.Oper;
        var targetReg = treeNode.RegNum;
        var targetType = treeNode.Type;
        var emit = Emitter;

        assert(oper is GT_ADD or GT_SUB or GT_MUL or GT_DIV or GT_UDIV or GT_AND or GT_AND_NOT or
            GT_OR or GT_OR_NOT or GT_XOR or GT_XOR_NOT);

        var op1 = treeNode.Op1;
        var op2 = treeNode.Op2;

        // The arithmetic node must be sitting in a register (since it's not contained).
        assert(targetReg != REG_NA);

        if ((op2.Oper is GT_MUL) && op2.IsContained)
        {
            assert(varTypeIsIntegral(targetType) || targetType == TYP_BYREF);
            assert((treeNode.Flags & GTF_SET_FLAGS) == 0);

            var a = op1;
            var b = op2.AsOp().Op1;
            var c = op2.AsOp().Op2;

            instruction ins;
            switch (oper)
            {
                case GT_ADD:
                {
                    ins = INS_madd;
                    break;
                }

                case GT_SUB:
                {
                    ins = INS_msub;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            // MADD/MSUB take the product operands before the addend.
            emit.emitIns_R_R_R_R(ins, emitActualTypeSize(treeNode), targetReg, b.RegNum, c.RegNum, a.RegNum);
            genProduceReg(treeNode);
            return;
        }
        else if ((op2.Oper is GT_LSH or GT_RSH or GT_RSZ) && op2.IsContained)
        {
            assert(varTypeIsIntegral(targetType) || targetType == TYP_BYREF);

            var a = op1;
            var b = op2.AsOp().Op1;
            var c = op2.AsOp().Op2;

            // The shifted operand's amount must remain an immediate.
            assert(c.IsContained && c.IsCnsIntOrI);

            var ins = genGetInsForOper(treeNode.Oper, targetType);
            var opt = INS_OPTS_NONE;

            if ((treeNode.Flags & GTF_SET_FLAGS) != 0)
            {
                switch (oper)
                {
                    case GT_ADD:
                    {
                        ins = INS_adds;
                        break;
                    }

                    case GT_SUB:
                    {
                        ins = INS_subs;
                        break;
                    }

                    case GT_AND:
                    {
                        ins = INS_ands;
                        break;
                    }

                    case GT_AND_NOT:
                    {
                        ins = INS_bics;
                        break;
                    }

                    default:
                    {
                        noway_assert(false, "Unexpected BinaryOp with GTF_SET_FLAGS set.");
                        break;
                    }
                }
            }

            opt = ShiftOpToInsOpts(op2.Oper);
            emit.emitIns_R_R_R_I(ins, emitActualTypeSize(treeNode), targetReg, a.RegNum, b.RegNum,
                c.AsIntConCommon().IconValue, opt);

            genProduceReg(treeNode);
            return;
        }
        else if ((op2.Oper is GT_ROR) && op2.IsContained)
        {
            // ROR is only contained under integral bitwise parents.
            assert(varTypeIsIntegral(targetType));

            var a = op1;
            var b = op2.AsOp().Op1;
            var c = op2.AsOp().Op2;

            assert(c.IsContained && c.IsCnsIntOrI);

            var ins = genGetInsForOper(treeNode.Oper, targetType);
            var opt = INS_OPTS_NONE;

            if ((treeNode.Flags & GTF_SET_FLAGS) != 0)
            {
                switch (oper)
                {
                    case GT_AND:
                    {
                        ins = INS_ands;
                        break;
                    }

                    default:
                    {
                        noway_assert(false, "Unexpected BinaryOp with GTF_SET_FLAGS set.");
                        break;
                    }
                }
            }

            assert(op2.Oper is GT_ROR);
            opt = INS_OPTS_ROR;
            emit.emitIns_R_R_R_I(ins, emitActualTypeSize(treeNode), targetReg, a.RegNum, b.RegNum,
                c.AsIntConCommon().IconValue, opt);

            genProduceReg(treeNode);
            return;
        }
        else if ((op2.Oper is GT_CAST) && op2.IsContained)
        {
            assert(varTypeIsIntegral(targetType) || targetType == TYP_BYREF);

            var a = op1;
            var cast = op2.AsCast();
            var b = cast.CastOp;
            var ins = genGetInsForOper(treeNode.Oper, targetType);
            var opt = INS_OPTS_NONE;

            if ((treeNode.Flags & GTF_SET_FLAGS) != 0)
            {
                switch (oper)
                {
                    case GT_ADD:
                    {
                        ins = INS_adds;
                        break;
                    }

                    case GT_SUB:
                    {
                        ins = INS_subs;
                        break;
                    }

                    default:
                    {
                        noway_assert(false, "Unexpected BinaryOp with GTF_SET_FLAGS set.");
                        break;
                    }
                }
            }

            var isZeroExtending = cast.IsZeroExtending();
            if (varTypeIsByte(cast.CastToType))
            {
                opt = isZeroExtending ? INS_OPTS_UXTB : INS_OPTS_SXTB;
            }
            else if (varTypeIsShort(cast.CastToType))
            {
                opt = isZeroExtending ? INS_OPTS_UXTH : INS_OPTS_SXTH;
            }
            else
            {
                assert(cast.CastToType == TYP_LONG && genActualTypeIsInt(b));
                opt = isZeroExtending ? INS_OPTS_UXTW : INS_OPTS_SXTW;
            }

            emit.emitIns_R_R_R(ins, emitActualTypeSize(treeNode), targetReg, a.RegNum, b.RegNum, opt);
            genProduceReg(treeNode);
            return;
        }
        else if (_compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI) && TargetOS.IsWindows &&
            op2.Oper.IsCnsIntOrI && op2.AsIntCon().IsIconHandle(GTF_ICON_SECREL_OFFSET))
        {
            // Windows NativeAOT represents section-relative TLS offsets as a paired ADD relocation.
            assert(op2.AsIntCon().ImmedValNeedsReloc(_compiler));

            var attr = emitActualTypeSize(targetType);
            attr = EA_SET_FLG(attr, EA_CNS_RELOC_FLG | EA_CNS_SEC_RELOC);

            emit.emitIns_Add_Add_Tls_Reloc(attr, targetReg, op1.RegNum, op2.AsIntCon().IconValue);
            return;
        }

        var binaryIns = genGetInsForOper(treeNode.Oper, targetType);

        if ((treeNode.Flags & GTF_SET_FLAGS) != 0)
        {
            switch (oper)
            {
                case GT_ADD:
                {
                    binaryIns = INS_adds;
                    break;
                }

                case GT_SUB:
                {
                    binaryIns = INS_subs;
                    break;
                }

                case GT_AND:
                {
                    binaryIns = INS_ands;
                    break;
                }

                case GT_AND_NOT:
                {
                    binaryIns = INS_bics;
                    break;
                }

                default:
                {
                    noway_assert(false, "Unexpected BinaryOp with GTF_SET_FLAGS set.");
                    break;
                }
            }
        }

        var resultReg = emit.emitInsTernary(binaryIns, emitActualTypeSize(treeNode), treeNode, op1, op2);
        assert(resultReg == targetReg);
        genProduceReg(treeNode);
    }
}
#endif
