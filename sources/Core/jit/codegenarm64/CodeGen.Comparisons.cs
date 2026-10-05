// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCompare(GenTreeOp tree)
    {
        var targetReg = tree.RegNum;
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var op1Type = genActualType(op1.Type);
        var op2Type = genActualType(op2.Type);

        assert(!op1.IsUsedFromMemory);

        var cmpSize = (emitAttr)genTypeSize(op1Type);
        assert(genTypeSize(op1Type) == genTypeSize(op2Type));

        var emit = Emitter;
        if (varTypeIsFloating(op1Type))
        {
            assert(varTypeIsFloating(op2Type));
            assert(!op1.IsContained);
            assert(op1Type == op2Type);

            if (op2.IsFloatPositiveZero)
            {
                assert(op2.IsContained);
                emit.emitIns_R_F(INS_fcmp, cmpSize, op1.RegNum, 0.0);
            }
            else
            {
                assert(!op2.IsContained);
                emit.emitIns_R_R(INS_fcmp, cmpSize, op1.RegNum, op2.RegNum);
            }
        }
        else
        {
            assert(!varTypeIsFloating(op2Type));
            assert(!op1.IsContainedIntOrIImmed);

            var ins = tree.Oper is GT_TEST_EQ or GT_TEST_NE or GT_TEST ? INS_tst : INS_cmp;
            if (op2.IsContainedIntOrIImmed)
            {
                var intConst = op2.AsIntConCommon();
                var op1Reg = op1.RegNum;

                if (_compiler.opts.OptimizationEnabled && (ins == INS_cmp) && (targetReg != REG_NA) &&
                    (tree.Oper is GT_LT) && !tree.IsUnsigned && intConst.IsIntegralConst(0) &&
                    ((cmpSize == EA_4BYTE) || (cmpSize == EA_8BYTE)))
                {
                    emit.emitIns_R_R_I(INS_lsr, cmpSize, targetReg, op1Reg, (nint)((int)cmpSize * 8 - 1));
                    genProduceReg(tree);
                    return;
                }

                emit.emitIns_R_I(ins, cmpSize, op1Reg, intConst.IconValue);
            }
            else if (op2.IsContained)
            {
                var oper = op2.Oper;
                switch (oper)
                {
                    case GT_NEG:
                    {
                        assert(ins == INS_cmp);
                        ins = INS_cmn;

                        var negOperand = op2.AsUnOp().Op1;
                        if (negOperand.IsContained)
                        {
                            oper = negOperand.Oper;
                            switch (oper)
                            {
                                case GT_LSH:
                                case GT_RSH:
                                case GT_RSZ:
                                {
                                    var shift = negOperand.AsOp();
                                    var shiftAmount = shift.Op2;
                                    assert(shiftAmount.Oper.IsCnsIntOrI);
                                    assert(shiftAmount.IsContained);

                                    emit.emitIns_R_R_I(
                                        ins,
                                        cmpSize,
                                        op1.RegNum,
                                        shift.Op1.RegNum,
                                        unchecked((nint)shiftAmount.AsIntConCommon().IntegralValue),
                                        ShiftOpToInsOpts(oper));
                                    break;
                                }

                                case GT_CAST:
                                {
                                    var cast = negOperand.AsCast();
                                    var desc = new GenIntCastDesc(cast);
                                    assert(desc.Check is CHECK_NONE);

                                    var extOpts = INS_OPTS_NONE;
                                    switch (desc.Extend)
                                    {
                                        case ZERO_EXTEND_SMALL_INT:
                                        {
                                            extOpts = desc.ExtendSrcSize == 1 ? INS_OPTS_UXTB : INS_OPTS_UXTH;
                                            break;
                                        }

                                        case SIGN_EXTEND_SMALL_INT:
                                        {
                                            extOpts = desc.ExtendSrcSize == 1 ? INS_OPTS_SXTB : INS_OPTS_SXTH;
                                            break;
                                        }

                                        case ZERO_EXTEND_INT:
                                        {
                                            extOpts = INS_OPTS_UXTW;
                                            break;
                                        }

                                        case SIGN_EXTEND_INT:
                                        {
                                            extOpts = INS_OPTS_SXTW;
                                            break;
                                        }

                                        case COPY:
                                        {
                                            extOpts = INS_OPTS_NONE;
                                            break;
                                        }

                                        default:
                                        {
                                            unreached();
                                            break;
                                        }
                                    }

                                    emit.emitIns_R_R(ins, cmpSize, op1.RegNum, cast.CastOp.RegNum, extOpts);
                                    break;
                                }

                                default:
                                {
                                    unreached();
                                    break;
                                }
                            }
                        }
                        else
                        {
                            emit.emitIns_R_R(ins, cmpSize, op1.RegNum, negOperand.RegNum);
                        }

                        break;
                    }

                    case GT_LSH:
                    case GT_RSH:
                    case GT_RSZ:
                    {
                        var shift = op2.AsOp();
                        var shiftAmount = shift.Op2;
                        assert(shiftAmount.Oper.IsCnsIntOrI);
                        assert(shiftAmount.IsContained);

                        emit.emitIns_R_R_I(
                            ins,
                            cmpSize,
                            op1.RegNum,
                            shift.Op1.RegNum,
                            unchecked((nint)shiftAmount.AsIntConCommon().IntegralValue),
                            ShiftOpToInsOpts(oper));
                        break;
                    }

                    case GT_CAST:
                    {
                        assert(ins == INS_cmp);
                        var cast = op2.AsCast();
                        assert((uint)cmpSize >= genTypeSize(cast.CastType));
                        assert((cmpSize == EA_4BYTE) || (cmpSize == EA_8BYTE));
                        assert(op1.IsUsedFromReg);
                        assert(cast.CastOp.IsUsedFromReg);

                        var desc = new GenIntCastDesc(cast);
                        assert(desc.Check is CHECK_NONE);

                        var extOpts = INS_OPTS_NONE;
                        switch (desc.Extend)
                        {
                            case ZERO_EXTEND_SMALL_INT:
                            {
                                extOpts = desc.ExtendSrcSize == 1 ? INS_OPTS_UXTB : INS_OPTS_UXTH;
                                break;
                            }

                            case SIGN_EXTEND_SMALL_INT:
                            {
                                extOpts = desc.ExtendSrcSize == 1 ? INS_OPTS_SXTB : INS_OPTS_SXTH;
                                break;
                            }

                            case ZERO_EXTEND_INT:
                            {
                                extOpts = INS_OPTS_UXTW;
                                break;
                            }

                            case SIGN_EXTEND_INT:
                            {
                                extOpts = INS_OPTS_SXTW;
                                break;
                            }

                            default:
                            {
                                unreached();
                                break;
                            }
                        }

                        emit.emitIns_R_R(INS_cmp, cmpSize, op1.RegNum, cast.CastOp.RegNum, extOpts);
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
            else
            {
                emit.emitIns_R_R(ins, cmpSize, op1.RegNum, op2.RegNum);
            }
        }

        if (targetReg != REG_NA)
        {
            inst_SETCC(GenCondition.FromRelop(tree), tree.Type, targetReg);
            genProduceReg(tree);
        }
    }
}
#endif
