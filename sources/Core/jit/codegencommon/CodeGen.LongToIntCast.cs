// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genLongToIntCast(GenTreeCast cast)
    {
        assert(cast.Oper is GT_CAST);

        var src = cast.CastOp.AsOp();
        noway_assert(src.Oper is GT_LONG);
        genConsumeRegs(src);

        var srcType = cast.IsUnsigned ? TYP_ULONG : TYP_LONG;
        var dstType = cast.CastType;
        var loSrcReg = src.Op1.RegNum;
        var hiSrcReg = src.Op2.RegNum;
        var dstReg = cast.RegNum;

        assert(dstType is TYP_INT or TYP_UINT);
        assert(genIsValidIntReg(loSrcReg));
        assert(genIsValidIntReg(hiSrcReg));
        assert(genIsValidIntReg(dstReg));

        // Checked long to int requires the upper 33 bits all zero or all one;
        // ulong to int requires them all zero. Either source to uint requires
        // the upper 32 bits zero.
        if (cast.HasOverflowCheck)
        {
            if ((srcType is TYP_LONG) && (dstType is TYP_INT))
            {
                var allOne = genCreateTempLabel();
                var success = genCreateTempLabel();

                inst_RV_RV(INS_test, loSrcReg, loSrcReg, TYP_INT, EA_4BYTE);
                inst_JMP(EJ_js, allOne);

                inst_RV_RV(INS_test, hiSrcReg, hiSrcReg, TYP_INT, EA_4BYTE);
                genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
                inst_JMP(EJ_jmp, success);

                genDefineTempLabel(allOne);
                inst_RV_IV(INS_cmp, hiSrcReg, -1, EA_4BYTE);
                genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);

                genDefineTempLabel(success);
            }
            else
            {
                if ((srcType is TYP_ULONG) && (dstType is TYP_INT))
                {
                    inst_RV_RV(INS_test, loSrcReg, loSrcReg, TYP_INT, EA_4BYTE);
                    genJumpToThrowHlpBlk(EJ_js, SCK_OVERFLOW);
                }

                inst_RV_RV(INS_test, hiSrcReg, hiSrcReg, TYP_INT, EA_4BYTE);
                genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
            }
        }

        inst_Mov(TYP_INT, dstReg, loSrcReg, canSkip: true);
        genProduceReg(cast);
    }
}
#endif
