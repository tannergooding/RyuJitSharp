// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
    public regNumber emitInsBinary(instruction ins, emitAttr attr, GenTree dst, GenTree src)
    {
        assert(!dst.IsContained);
        assert(!src.IsContained || src.IsContainedIntOrIImmed);

        var intConst = (GenTreeIntConCommon?)null;
        if (src.IsContainedIntOrIImmed)
        {
            intConst = src.AsIntConCommon();
        }

        if (intConst is not null)
        {
            emitIns_R_I(ins, attr, dst.RegNum, unchecked((nint)intConst.IconValue));
            return dst.RegNum;
        }

        emitIns_R_R(ins, attr, dst.RegNum, src.RegNum, INS_FLAGS_DONT_CARE);
        return dst.RegNum;
    }

    public void emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int imm,
        insFlags flags = INS_FLAGS_DONT_CARE, insOpts opt = INS_OPTS_NONE)
    {
        recordArm32InsRRI(ins, attr, reg1, reg2, imm, flags, opt);
    }

    public void emitIns_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, regNumber reg3,
        int imm, insFlags flags = INS_FLAGS_DONT_CARE, insOpts opt = INS_OPTS_NONE)
    {
        recordArm32InsRRRImm(ins, attr, reg1, reg2, reg3, imm, flags, opt);
    }
#endif

#if TARGET_ARM64
    // The caller consumes non-contained sources and produces the destination.
    public regNumber emitInsTernary(instruction ins, emitAttr attr, GenTree dst, GenTree src1, GenTree src2)
    {
        assert(!dst.IsContained);

        var intConst = (GenTreeIntConCommon?)null;
        var nonIntReg = src1;
        if (varTypeIsFloating(dst.Type))
        {
            assert(!src1.IsContained);
            assert(!src2.IsContained);
        }
        else
        {
            assert(!src2.IsContained || src2.IsContainedIntOrIImmed);

            if (src2.IsContainedIntOrIImmed)
            {
                intConst = src2.AsIntConCommon();
                nonIntReg = src1;
            }
            else if (dst.Oper.IsCommutative)
            {
                assert(!src1.IsContained || src1.IsContainedIntOrIImmed);

                if (src1.IsContainedIntOrIImmed)
                {
                    assert(!src2.IsContainedIntOrIImmed);
                    intConst = src1.AsIntConCommon();
                    nonIntReg = src2;
                }
            }
            else
            {
                assert(!src1.IsContained);
            }
        }

        var isMulOverflow = false;
        if (dst.HasOverflowCheckEx)
        {
            if (ins is INS_add or INS_adds)
            {
                ins = INS_adds;
            }
            else if (ins is INS_sub or INS_subs)
            {
                ins = INS_subs;
            }
            else if (ins is INS_mul)
            {
                isMulOverflow = true;
                assert(intConst is null);
            }
            else
            {
                assert(false, "Invalid ins for overflow check");
            }
        }

        if (intConst is not null)
        {
            emitIns_R_R_I(ins, attr, dst.RegNum, nonIntReg.RegNum, intConst.IconValue);
        }
        else if (isMulOverflow)
        {
            var extraReg = codeGen.InternalRegisters.GetSingle(dst);
            assert(extraReg != dst.RegNum);

            if (dst.AsOp().IsUnsigned)
            {
                if (attr == EA_4BYTE)
                {
                    // A 32-bit multiply's full result fits in 64 bits; the high half detects overflow.
                    emitIns_R_R_R(INS_umull, EA_8BYTE, dst.RegNum, src1.RegNum, src2.RegNum);
                    emitIns_R_R_I(INS_lsr, EA_8BYTE, extraReg, dst.RegNum, 32);
                }
                else
                {
                    assert(attr == EA_8BYTE);
                    emitIns_R_R_R(INS_umulh, attr, extraReg, src1.RegNum, src2.RegNum);
                    emitIns_R_R_R(ins, attr, dst.RegNum, src1.RegNum, src2.RegNum);
                }

                // Unsigned multiplication overflows exactly when the high half is nonzero.
                emitIns_R_I(INS_cmp, attr, extraReg, 0);
            }
            else
            {
                var bitShift = attr == EA_4BYTE ? 31 : 63;
                if (attr == EA_4BYTE)
                {
                    emitIns_R_R_R(INS_smull, EA_8BYTE, dst.RegNum, src1.RegNum, src2.RegNum);
                    emitIns_R_R_I(INS_lsr, EA_8BYTE, extraReg, dst.RegNum, 32);
                }
                else
                {
                    assert(attr == EA_8BYTE);
                    emitIns_R_R_R(INS_smulh, attr, extraReg, src1.RegNum, src2.RegNum);
                    emitIns_R_R_R(ins, attr, dst.RegNum, src1.RegNum, src2.RegNum);
                }

                // Signed multiplication overflows when the high half is not the low half's sign extension.
                emitIns_R_R_I(INS_cmp, attr, extraReg, dst.RegNum, bitShift, INS_OPTS_ASR);
            }
        }
        else
        {
            emitIns_R_R_R(ins, attr, dst.RegNum, src1.RegNum, src2.RegNum);
        }

        if (dst.HasOverflowCheckEx)
        {
            assert(!varTypeIsFloating(dst.Type));
            codeGen.genCheckOverflow(dst);
        }

        return dst.RegNum;
    }
#else
    public regNumber emitInsTernary(instruction ins, emitAttr attr, GenTree dst, GenTree src1, GenTree src2)
    {
        assert(!dst.IsContained);

        var intConst = (GenTreeIntConCommon?)null;
        var nonIntReg = src1;
        if (varTypeIsFloating(dst.Type))
        {
            assert(!src1.IsContained);
            assert(!src2.IsContained);
        }
        else
        {
            assert(!src2.IsContained || src2.IsContainedIntOrIImmed);
            if (src2.IsContainedIntOrIImmed)
            {
                intConst = src2.AsIntConCommon();
                nonIntReg = src1;
            }
            else if (dst.Oper.IsCommutative)
            {
                assert(!src1.IsContained || src1.IsContainedIntOrIImmed);
                if (src1.IsContainedIntOrIImmed)
                {
                    assert(!src2.IsContainedIntOrIImmed);
                    intConst = src1.AsIntConCommon();
                    nonIntReg = src2;
                }
            }
            else
            {
                assert(!src1.IsContained);
            }
        }

        var flags = INS_FLAGS_DONT_CARE;
        var isMulOverflow = false;
        if (dst.HasOverflowCheckEx)
        {
            if (ins is INS_add or INS_adc or INS_sub or INS_sbc)
            {
                flags = INS_FLAGS_SET;
            }
            else if (ins is INS_mul)
            {
                isMulOverflow = true;
                assert(intConst is null);
            }
            else
            {
                assert(false, "Invalid ins for overflow check");
            }
        }

        if ((dst.Flags & GTF_SET_FLAGS) != 0)
        {
            assert(ins is INS_add or INS_adc or INS_sub or INS_sbc or INS_and or INS_orr or INS_eor or INS_orn or INS_bic);
            flags = INS_FLAGS_SET;
        }

        if (intConst is not null)
        {
            emitIns_R_R_I(ins, attr, dst.RegNum, nonIntReg.RegNum,
                unchecked((int)intConst.IconValue), flags);
        }
        else if (isMulOverflow)
        {
            var extraReg = codeGen.InternalRegisters.GetSingle(dst);
            assert(extraReg != dst.RegNum);

            if (dst.AsOp().IsUnsigned)
            {
                emitIns_R_R_R_R(INS_umull, EA_4BYTE, dst.RegNum, extraReg, src1.RegNum, src2.RegNum);
                emitIns_R_I(INS_cmp, attr, extraReg, 0);
            }
            else
            {
                emitIns_R_R_R_R(INS_smull, EA_4BYTE, dst.RegNum, extraReg, src1.RegNum, src2.RegNum);
                emitIns_R_R_I(INS_cmp, attr, extraReg, dst.RegNum, 31,
                    INS_FLAGS_DONT_CARE, INS_OPTS_ASR);
            }
        }
        else
        {
            emitIns_R_R_R(ins, attr, dst.RegNum, src1.RegNum, src2.RegNum, flags);
        }

        if (dst.HasOverflowCheckEx)
        {
            assert(!varTypeIsFloating(dst.Type));

            emitJumpKind jumpKind;
            if (dst.Oper is GT_MUL)
            {
                jumpKind = EJ_ne;
            }
            else
            {
                jumpKind = dst.AsOp().IsUnsigned ? EJ_lo : EJ_vs;
                if ((jumpKind == EJ_lo) && (dst.Oper is not GT_SUB and not GT_SUB_HI))
                {
                    jumpKind = EJ_hs;
                }
            }

            codeGen.genJumpToThrowHlpBlk(jumpKind, SCK_OVERFLOW);
        }

        return dst.RegNum;
    }
#endif
}
#endif
