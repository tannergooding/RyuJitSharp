// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genIntCastOverflowCheck(GenTreeCast cast, in GenIntCastDesc desc, regNumber reg)
    {
        var tempReg = InternalRegisters.GetSingle(cast);

        switch (desc.Check)
        {
            case CHECK_POSITIVE:
            {
                if (desc.CheckSrcSize == 4)
                {
                    // Treat a uint above INT32_MAX as negative so the overflow branch catches it.
                    GetEmitter().emitIns_R_R(INS_sext_w, EA_4BYTE, tempReg, reg);
                    reg = tempReg;
                }

                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_blt, reg, null, REG_R0);
                break;
            }

            case CHECK_UINT_RANGE:
            {
                // Any set bit above bit 31 means the value does not fit in uint.
                GetEmitter().emitIns_R_R_I(INS_srli, EA_8BYTE, tempReg, reg, 32);
                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg, null, REG_R0);
                break;
            }

            case CHECK_POSITIVE_INT_RANGE:
            {
                // Any set bit above bit 30 means the value does not fit in positive int.
                GetEmitter().emitIns_R_R_I(INS_srli, EA_8BYTE, tempReg, reg, 31);
                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg, null, REG_R0);
                break;
            }

            case CHECK_INT_RANGE:
            {
                // A sign-extended low word must equal the original value.
                GetEmitter().emitIns_R_R(INS_sext_w, EA_4BYTE, tempReg, reg);
                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg, null, reg);
                break;
            }

            default:
            {
                assert(desc.Check == CHECK_SMALL_INT_RANGE);

                var castSize = unchecked((int)genTypeSize(cast.CastType));
                var isSrcOrDstUnsigned = desc.CheckSmallIntMin == 0;
                if (isSrcOrDstUnsigned)
                {
                    // Discard the destination bits; signed destinations must also have a clear sign bit.
                    var isDstSigned = !varTypeIsUnsigned(cast.CastType);
                    var excludeMsb = isDstSigned ? 1 : 0;
                    var typeSize = (8 * castSize) - excludeMsb;
                    GetEmitter().emitIns_R_R_I(INS_srli, EA_8BYTE, tempReg, reg, typeSize);
                    genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg, null, REG_R0);
                }
                else
                {
                    // Sign-extend the narrowed value and compare it with the original.
                    var extensionSize = (8 - castSize) * 8;
                    GetEmitter().emitIns_R_R_I(INS_slli, EA_8BYTE, tempReg, reg, extensionSize);
                    GetEmitter().emitIns_R_R_I(INS_srai, EA_8BYTE, tempReg, tempReg, extensionSize);
                    genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg, null, reg);
                }
                break;
            }
        }
    }

    private void genIntToIntCast(GenTreeCast cast)
    {
        genConsumeRegs(cast.CastOp);

        var emit = GetEmitter();
        var srcType = genActualType(cast.CastOp.Type);
        var srcReg = cast.CastOp.RegNum;
        var dstReg = cast.RegNum;
        assert(genIsValidIntReg(srcReg));
        assert(genIsValidIntReg(dstReg));

        var desc = new GenIntCastDesc(cast);
        if (desc.Check != CHECK_NONE)
        {
            genIntCastOverflowCheck(cast, in desc, srcReg);
        }

        if ((desc.Extend != COPY) || (srcReg != dstReg))
        {
            switch (desc.Extend)
            {
                case ZERO_EXTEND_SMALL_INT:
                case SIGN_EXTEND_SMALL_INT:
                {
                    var isSignExtend = desc.Extend == SIGN_EXTEND_SMALL_INT;
                    if (!isSignExtend && (desc.ExtendSrcSize == 1))
                    {
                        emit.emitIns_R_R_I(INS_andi, EA_PTRSIZE, dstReg, srcReg, 0xff);
                        break;
                    }

                    if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb))
                    {
                        var isHalf = desc.ExtendSrcSize == 2;
                        instruction extend;
                        if (isSignExtend)
                        {
                            extend = isHalf ? INS_sext_h : INS_sext_b;
                        }
                        else
                        {
                            assert(isHalf);
                            extend = INS_zext_h;
                        }

                        emit.emitIns_R_R(extend, EA_PTRSIZE, dstReg, srcReg);
                    }
                    else
                    {
                        var shiftRight = isSignExtend ? INS_srai : INS_srli;
                        var shiftAmount = unchecked((int)(64 - (desc.ExtendSrcSize * 8)));
                        emit.emitIns_R_R_I(INS_slli, EA_PTRSIZE, dstReg, srcReg, shiftAmount);
                        emit.emitIns_R_R_I(shiftRight, EA_PTRSIZE, dstReg, dstReg, shiftAmount);
                    }
                    break;
                }

                case ZERO_EXTEND_INT:
                {
                    if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Zba))
                    {
                        emit.emitIns_R_R_R(INS_add_uw, EA_PTRSIZE, dstReg, srcReg, REG_R0);
                    }
                    else
                    {
                        emit.emitIns_R_R_I(INS_slli, EA_PTRSIZE, dstReg, srcReg, 32);
                        emit.emitIns_R_R_I(INS_srli, EA_PTRSIZE, dstReg, dstReg, 32);
                    }
                    break;
                }

                case SIGN_EXTEND_INT:
                {
                    emit.emitIns_R_R(INS_sext_w, EA_4BYTE, dstReg, srcReg);
                    break;
                }

                default:
                {
                    assert(desc.Extend == COPY);
                    if (srcType == TYP_INT)
                    {
                        emit.emitIns_R_R(INS_sext_w, EA_4BYTE, dstReg, srcReg);
                    }
                    else
                    {
                        emit.emitIns_R_R(INS_mov, EA_PTRSIZE, dstReg, srcReg);
                    }
                    break;
                }
            }
        }

        genProduceReg(cast);
    }
}
#endif
