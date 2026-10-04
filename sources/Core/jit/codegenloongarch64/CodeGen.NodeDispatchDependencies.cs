// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genSetRegToConst(regNumber targetReg, var_types targetType, GenTree tree)
    {
        switch (tree.Oper)
        {
            case GT_CNS_INT:
            {
                var constant = tree.AsIntCon();
                var value = constant.IconValue;
                var attr = targetType.EmitActualSize;

                if (constant.ImmedValNeedsReloc(_compiler))
                {
                    attr = EA_SET_FLG(attr, EA_CNS_RELOC_FLG);
                }

                if (targetType is TYP_BYREF)
                {
                    attr = EA_SET_FLG(attr, EA_BYREF_FLG);
                }

#if DEBUG
                instGen_Set_Reg_To_Imm(attr, targetReg, value, INS_FLAGS_DONT_CARE,
                    targetHandle: unchecked((nuint)constant.TargetHandle), gtFlags: constant.Flags);
#else
                instGen_Set_Reg_To_Imm(attr, targetReg, value);
#endif
                _regSet.verifyRegUsed(targetReg);
                break;
            }
            case GT_CNS_DBL:
            {
                var emit = GetEmitter();
                var size = tree.Type.EmitActualSize;
                var value = tree.AsDblCon().DconVal;

                if (BitConverter.DoubleToInt64Bits(value) == 0)
                {
                    emit.emitIns_R_R(INS_movgr2fr_d, EA_8BYTE, targetReg, REG_R0);
                }
                else
                {
                    var handle = emit.emitFltOrDblConst(value, size);
                    assert(targetReg >= REG_F0);

                    var ins = size == EA_4BYTE ? INS_fld_s : INS_fld_d;
                    emit.emitIns_R_C(ins, size, targetReg, REG_NA, handle, 0);
                }
                break;
            }
            default:
            {
                unreached();
                return;
            }
        }
    }

    private void genCodeForShift(GenTree tree)
    {
        var ins = genGetInsForOper(tree);
        var size = tree.Type.EmitActualSize;

        assert(tree.RegNum != REG_NA);
        genConsumeOperands(tree.AsOp());

        var operand = tree.AsOp().Op1;
        var shiftBy = tree.AsOp().Op2;
        if (!shiftBy.Oper.IsCnsIntOrI)
        {
            GetEmitter().emitIns_R_R_R(ins, size, tree.RegNum, operand.RegNum, shiftBy.RegNum);
        }
        else
        {
            var shiftByImm = unchecked((uint)shiftBy.AsIntCon().IconValue);
            var immWidth = unchecked((uint)EA_SIZE(size) * 8);
            shiftByImm &= immWidth - 1;

            if ((ins == INS_slli_w) && (shiftByImm >= 32))
            {
                ins = INS_slli_d;
            }
            else if ((ins == INS_slli_d) && (shiftByImm >= 32) && (shiftByImm < 64))
            {
                ins = INS_slli_d;
            }
            else if ((ins == INS_srai_d) && (shiftByImm >= 32) && (shiftByImm < 64))
            {
                ins = INS_srai_d;
            }
            else if ((ins == INS_srli_d) && (shiftByImm >= 32) && (shiftByImm < 64))
            {
                ins = INS_srli_d;
            }
            else if ((ins == INS_rotri_d) && (shiftByImm >= 32) && (shiftByImm < 64))
            {
                ins = INS_rotri_d;
            }

            GetEmitter().emitIns_R_R_I(ins, size, tree.RegNum, operand.RegNum, unchecked((nint)shiftByImm));
        }

        genProduceReg(tree);
    }

    private void genCodeForLclAddr(GenTreeLclFld tree)
    {
        assert(tree.Oper is GT_LCL_ADDR);

        var targetType = tree.Type;
        var size = targetType.EmitSize;
        var targetReg = tree.RegNum;
        noway_assert(targetType is TYP_BYREF or TYP_I_IMPL);

        GetEmitter().emitIns_R_S(INS_lea, size, targetReg, tree.LclNum, tree.LclOffs);
        genProduceReg(tree);
    }

    private void genCodeForLclFld(GenTreeLclFld tree)
    {
        assert(tree.Oper is GT_LCL_FLD);

        var targetType = tree.Type;
        if (targetType is TYP_STRUCT)
        {
            NYI_IF(true, "GT_LCL_FLD: struct load local field not supported");
            throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 struct local-field loads are not supported.");
        }

        var targetReg = tree.RegNum;
        assert(targetReg != REG_NA);

        var size = targetType.EmitSize;
        var offset = tree.LclOffs;
        var varNum = tree.LclNum;
        assert((uint)varNum < (uint)_compiler.lvaCount);

        GetEmitter().emitIns_R_S(ins_Load(targetType), size, targetReg, varNum, offset);
        genProduceReg(tree);
    }

    private void genLeaInstruction(GenTreeAddrMode tree)
    {
        genConsumeOperands(tree);

        var emit = GetEmitter();
        var size = tree.Type.EmitSize;
        var offset = tree.Offset;

        if (tree.HasBaseAddress && tree.HasIndex)
        {
            var baseAddress = tree.BaseAddress;
            var index = tree.Index;
            var scale = tree.Scale;

            assert(uint.IsPow2(scale));
            scale = unchecked((byte)System.Numerics.BitOperations.TrailingZeroCount((uint)scale));
            assert(scale <= 4);

            if (offset == 0)
            {
                genScaledAdd(size, tree.RegNum, baseAddress.RegNum, index.RegNum, scale);
            }
            else
            {
                var useLargeOffsetSequence = Interruptible && (size == EA_BYREF);
                if (!useLargeOffsetSequence && Emitter.isValidSimm12(offset))
                {
                    genScaledAdd(size, tree.RegNum, baseAddress.RegNum, index.RegNum, scale);
                    var ins = size == EA_4BYTE ? INS_addi_w : INS_addi_d;
                    emit.emitIns_R_R_I(ins, size, tree.RegNum, tree.RegNum, offset);
                }
                else
                {
                    var tempReg = _internalRegisters.GetSingle(tree);
                    noway_assert(tempReg != index.RegNum);
                    noway_assert(tempReg != baseAddress.RegNum);

                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, tempReg, offset);
                    genScaledAdd(EA_PTRSIZE, tempReg, tempReg, index.RegNum, scale);

                    var ins = size == EA_4BYTE ? INS_add_w : INS_add_d;
                    emit.emitIns_R_R_R(ins, size, tree.RegNum, tempReg, baseAddress.RegNum);
                }
            }
        }
        else if (tree.HasBaseAddress)
        {
            var baseAddress = tree.BaseAddress;
            if (Emitter.isValidSimm12(offset))
            {
                if (offset != 0)
                {
                    emit.emitIns_R_R_I(INS_addi_d, size, tree.RegNum, baseAddress.RegNum, offset);
                }
                else if (tree.RegNum != baseAddress.RegNum)
                {
                    emit.emitIns_R_R_I(INS_ori, size, tree.RegNum, baseAddress.RegNum, 0);
                }
            }
            else
            {
                var tempReg = _internalRegisters.GetSingle(tree);
                emit.emitIns_I_la(EA_PTRSIZE, tempReg, offset);
                emit.emitIns_R_R_R(INS_add_d, size, tree.RegNum, baseAddress.RegNum, tempReg);
            }
        }
        else if (tree.HasIndex)
        {
            assert(false, "A baseless address computation is not supported on LoongArch64.");
        }

        genProduceReg(tree);
    }

    private void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
        var baseAddress = tree.Arr;
        var index = tree.Index;

        _ = genConsumeReg(baseAddress);
        _ = genConsumeReg(index);

        _gcInfo.gcMarkRegPtrVal(baseAddress.RegNum, baseAddress.Type);
        assert(!varTypeIsGC(index.Type));
        assert(index.IsUsedFromReg);

        if (tree.IsBoundsChecked)
        {
            GetEmitter().emitIns_R_R_I(INS_ld_w, EA_4BYTE, REG_R21, baseAddress.RegNum, tree.LenOffset);
            genJumpToThrowHlpBlk_la(SCK_RNGCHK_FAIL, INS_bgeu, index.RegNum, null, REG_R21);
        }

        var attr = tree.Type.EmitActualSize;
        var elementSize = unchecked((uint)tree.ElemSize);
        if (uint.IsPow2(elementSize))
        {
            var scale = System.Numerics.BitOperations.TrailingZeroCount(elementSize);

            if (elementSize <= 64)
            {
                genScaledAdd(attr, tree.RegNum, baseAddress.RegNum, index.RegNum, scale);
            }
            else
            {
                GetEmitter().emitIns_I_la(EA_PTRSIZE, REG_R21, scale);

                var shiftIns = attr == EA_4BYTE ? INS_sll_w : INS_sll_d;
                var addIns = attr == EA_4BYTE ? INS_add_w : INS_add_d;
                GetEmitter().emitIns_R_R_R(shiftIns, attr, REG_R21, index.RegNum, REG_R21);
                GetEmitter().emitIns_R_R_R(addIns, attr, tree.RegNum, REG_R21, baseAddress.RegNum);
            }
        }
        else
        {
            GetEmitter().emitIns_I_la(EA_4BYTE, REG_R21, unchecked((nint)elementSize));

            var multiplyIns = attr == EA_4BYTE ? INS_mul_w : INS_mul_d;
            var addIns = attr == EA_4BYTE ? INS_add_w : INS_add_d;
            GetEmitter().emitIns_R_R_R(multiplyIns, EA_PTRSIZE, REG_R21, index.RegNum, REG_R21);
            GetEmitter().emitIns_R_R_R(addIns, attr, tree.RegNum, REG_R21, baseAddress.RegNum);
        }

        GetEmitter().emitIns_R_R_I(INS_addi_d, attr, tree.RegNum, tree.RegNum, tree.ElemOffset);

        _gcInfo.gcMarkRegSetNpt(baseAddress.RegMask);
        genProduceReg(tree);
    }

    private void genScaledAdd(emitAttr attr, regNumber targetReg, regNumber baseReg, regNumber indexReg, int scale)
    {
        var emit = GetEmitter();
        if (scale == 0)
        {
            var ins = attr == EA_4BYTE ? INS_add_w : INS_add_d;
            emit.emitIns_R_R_R(ins, attr, targetReg, baseReg, indexReg);
        }
        else if (scale <= 4)
        {
            var ins = attr == EA_4BYTE ? INS_alsl_w : INS_alsl_d;
            emit.emitIns_R_R_R_I(ins, attr, targetReg, indexReg, baseReg, scale - 1);
        }
        else
        {
            var shiftIns = attr == EA_4BYTE ? INS_slli_w : INS_slli_d;
            var addIns = attr == EA_4BYTE ? INS_add_w : INS_add_d;

            emit.emitIns_R_R_I(shiftIns, attr, REG_R21, indexReg, scale);
            emit.emitIns_R_R_R(addIns, attr, targetReg, baseReg, REG_R21);
        }
    }

    private void genIntCastOverflowCheck(GenTreeCast cast, in GenIntCastDesc desc, regNumber reg)
    {
        assert(REG_R21 != reg);

        switch (desc.Check)
        {
            case CHECK_POSITIVE:
            {
                if (desc.CheckSrcSize == 4)
                {
                    // Sign-extend 32-bit sources so unsigned values above INT32_MAX fail the signed check.
                    GetEmitter().emitIns_R_R_I(INS_slli_w, EA_4BYTE, REG_R21, reg, 0);
                    reg = REG_R21;
                }

                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_blt, reg);
                break;
            }

            case CHECK_UINT_RANGE:
            {
                // The value fits in uint only if its upper 32 bits are zero.
                GetEmitter().emitIns_R_R_I(INS_srli_d, EA_8BYTE, REG_R21, reg, 32);
                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, REG_R21);
                break;
            }

            case CHECK_POSITIVE_INT_RANGE:
            {
                // The value fits in int only if its upper 33 bits are zero.
                GetEmitter().emitIns_R_R_I(INS_srli_d, EA_8BYTE, REG_R21, reg, 31);
                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, REG_R21);
                break;
            }

            case CHECK_INT_RANGE:
            {
                // Compare the input against the sign-extended low 32 bits.
                GetEmitter().emitIns_R_R_I(INS_slli_w, EA_4BYTE, REG_R21, reg, 0);
                genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, reg, null, REG_R21);
                break;
            }

            default:
            {
                assert(desc.Check == CHECK_SMALL_INT_RANGE);
                var castSize = cast.CastType.Size;
                var isSrcOrDstUnsigned = desc.CheckSmallIntMin == 0;

                if (isSrcOrDstUnsigned)
                {
                    // Reject set bits above the destination value, including its sign bit when signed.
                    var isDstSigned = !varTypeIsUnsigned(cast.CastType);
                    var excludeMsb = isDstSigned ? 1u : 0u;
                    var typeSize = (8 * castSize) - excludeMsb;
                    GetEmitter().emitIns_R_R_I(INS_srli_d, EA_8BYTE, REG_R21, reg,
                        unchecked((nint)typeSize));
                    genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, REG_R21);
                }
                else
                {
                    // Sign-extend the destination width, then compare it with the source value.
                    var extensionSize = (8 - castSize) * 8;
                    GetEmitter().emitIns_R_R_I(INS_slli_d, EA_8BYTE, REG_R21, reg,
                        unchecked((nint)extensionSize));
                    GetEmitter().emitIns_R_R_I(INS_srai_d, EA_8BYTE, REG_R21, REG_R21,
                        unchecked((nint)extensionSize));

                    if (desc.CheckSrcSize == 4)
                    {
                        GetEmitter().emitIns_R_R_I(INS_slli_w, EA_4BYTE, REG_RA, reg, 0);
                        reg = REG_RA;
                    }

                    genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, REG_R21, null, reg);
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
        const int pos = 0;

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
                {
                    var mostSignificantBit = desc.ExtendSrcSize == 1 ? pos + 7 : pos + 15;
                    emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_PTRSIZE, dstReg, srcReg,
                        mostSignificantBit, pos);
                    break;
                }

                case SIGN_EXTEND_SMALL_INT:
                {
                    var ins = desc.ExtendSrcSize == 1 ? INS_ext_w_b : INS_ext_w_h;
                    emit.emitIns_R_R(ins, EA_PTRSIZE, dstReg, srcReg);
                    break;
                }

                case ZERO_EXTEND_INT:
                {
                    emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_PTRSIZE, dstReg, srcReg, pos + 31, pos);
                    break;
                }

                case SIGN_EXTEND_INT:
                {
                    emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, dstReg, srcReg, 0);
                    break;
                }

                default:
                {
                    assert(desc.Extend == COPY);
                    if (srcType == TYP_INT)
                    {
                        emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, dstReg, srcReg, 0);
                    }
                    else
                    {
                        emit.emitIns_R_R_I(INS_ori, EA_PTRSIZE, dstReg, srcReg, 0);
                    }

                    break;
                }
            }
        }

        genProduceReg(cast);
    }

    private void genIntrinsic(GenTreeIntrinsic tree)
    {
        var op1 = tree.Op1;
        var op2 = tree.Op2;

        switch (tree.IntrinsicName)
        {
            case NI_PRIMITIVE_SaturateToInt8:
            case NI_PRIMITIVE_SaturateToInt16:
            case NI_PRIMITIVE_SaturateToUInt8:
            case NI_PRIMITIVE_SaturateToUInt16:
            {
                nint minVal;
                nint maxVal;
                switch (tree.IntrinsicName)
                {
                    case NI_PRIMITIVE_SaturateToInt8:
                    {
                        minVal = sbyte.MinValue;
                        maxVal = sbyte.MaxValue;
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToInt16:
                    {
                        minVal = short.MinValue;
                        maxVal = short.MaxValue;
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToUInt8:
                    {
                        minVal = 0;
                        maxVal = byte.MaxValue;
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToUInt16:
                    {
                        minVal = 0;
                        maxVal = ushort.MaxValue;
                        break;
                    }

                    default:
                    {
                        unreached();
                        return;
                    }
                }

                genConsumeOperands(tree.AsOp());
                var dst = tree.RegNum;
                var src = op1.RegNum;
                var tmpReg = _internalRegisters.GetSingle(tree);
                var emit = GetEmitter();

                // slli.w sign-extends bits[31:0], so subsequent 64-bit comparisons use a normalized value.
                emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, dst, src, 0);

                var skipLo = genCreateTempLabel();
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, minVal);
                emit.emitIns_J_cond_la(INS_bge, skipLo, dst, tmpReg);
                emit.emitIns_R_R(INS_mov, EA_PTRSIZE, dst, tmpReg);
                genDefineTempLabel(skipLo);

                var skipHi = genCreateTempLabel();
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, maxVal);
                emit.emitIns_J_cond_la(INS_bge, skipHi, tmpReg, dst);
                emit.emitIns_R_R(INS_mov, EA_PTRSIZE, dst, tmpReg);
                genDefineTempLabel(skipHi);

                genProduceReg(tree);
                return;
            }

            default:
            {
                break;
            }
        }

        var attr = tree.Type.EmitActualSize;
        instruction instr;

        // All remaining intrinsics are binary floating-point operations.
        assert(op2 is not null);
        switch (tree.IntrinsicName)
        {
            case NI_System_Math_MaxNative:
            {
                instr = attr == EA_4BYTE ? INS_fmax_s : INS_fmax_d;
                break;
            }

            case NI_System_Math_MinNative:
            {
                instr = attr == EA_4BYTE ? INS_fmin_s : INS_fmin_d;
                break;
            }

            default:
            {
                NO_WAY("Unknown intrinsic");
                throw new FatalJitException(CORJIT_INTERNALERROR, "Unknown intrinsic reached unreachable code.");
            }
        }

        genConsumeOperands(tree.AsOp());
        GetEmitter().emitIns_R_R_R(instr, attr, tree.RegNum, op1.RegNum, op2.RegNum);
        genProduceReg(tree);
    }

#if FEATURE_HW_INTRINSICS
    private void genHWIntrinsic(GenTreeHWIntrinsic tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 hardware-intrinsic generation is not ported.");
    }
#endif

    private void genCodeForNullCheck(GenTreeIndir tree)
    {
        assert(tree.Oper is GT_NULLCHECK);
        genConsumeRegs(tree.Op1);

        GetEmitter().emitInsLoadStoreOp(ins_Load(tree.Type), tree.Type.EmitActualSize, REG_R0, tree);
    }

    private void genRangeCheck(GenTree tree)
    {
        noway_assert(tree.Oper is GT_BOUNDS_CHECK);
        var boundsCheck = tree.AsBoundsChk();
        var arrayLength = boundsCheck.ArrayLength;
        var arrayIndex = boundsCheck.Index;

        GenTree src1;
        GenTree src2;
        regNumber reg1;
        regNumber reg2;

        genConsumeRegs(arrayIndex);
        genConsumeRegs(arrayLength);

        var emit = GetEmitter();
        if (arrayIndex.IsContainedIntOrIImmed)
        {
            src1 = arrayLength;
            src2 = arrayIndex;
            reg1 = REG_R21;
            reg2 = src1.RegNum;

            var imm = src2.AsIntConCommon().IconValue;
            if (imm == nint.MaxValue)
            {
                emit.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_R21, REG_R0, -1);
                emit.emitIns_R_R_I(INS_srli_d, EA_PTRSIZE, REG_R21, REG_R21, 1);
            }
            else
            {
                emit.emitIns_I_la(EA_PTRSIZE, REG_R21, imm);
            }
        }
        else
        {
            src1 = arrayIndex;
            src2 = arrayLength;
            reg1 = src1.RegNum;

            if (src2.IsContainedIntOrIImmed)
            {
                reg2 = REG_R21;
                var imm = src2.AsIntConCommon().IconValue;
                emit.emitIns_I_la(EA_PTRSIZE, REG_R21, imm);
            }
            else
            {
                if (genActualType(src1.Type) is TYP_INT)
                {
                    emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, REG_R21, reg1, 0);
                    reg1 = REG_R21;
                }

                reg2 = src2.RegNum;
            }
        }

#if DEBUG
        var boundsCheckType = genActualType(src2.Type);
        var src1Type = genActualType(src1.Type);
        assert(boundsCheckType is TYP_INT or TYP_LONG);
        assert(src1Type is TYP_INT or TYP_LONG);
#endif

        genJumpToThrowHlpBlk_la(boundsCheck.ThrowKind, INS_bgeu, reg1, null, reg2);
    }
}
#endif
