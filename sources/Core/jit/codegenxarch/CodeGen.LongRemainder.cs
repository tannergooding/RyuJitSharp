// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLongUMod(GenTreeOp node)
    {
        assert(node.Oper is GT_UMOD);
        assert(node.Type is TYP_INT);

        var dividend = node.Op1.AsOp();
        assert(dividend.Oper is GT_LONG);
        assert(varTypeIsLong(dividend.Type));

        genConsumeOperands(node);

        var dividendLo = dividend.Op1;
        var dividendHi = dividend.Op2;
        assert(dividendLo.IsUsedFromReg);
        assert(dividendHi.IsUsedFromReg);

        var divisor = node.Op2;
        var constant = divisor.SkipCopyOrReload;
        assert(constant.Oper is GT_CNS_INT);
        assert(constant.IsUsedFromReg);
        assert(constant.AsIntConCommon().IconValue >= 2);
        assert(constant.AsIntConCommon().IconValue <= 0x3FFFFFFF);

        genCopyRegIfNeeded(dividendLo, REG_EAX);
        genCopyRegIfNeeded(dividendHi, REG_EDX);

        var noOverflow = genCreateTempLabel();
        // Reduce the high word first when necessary: (high * 2^32 + low) % d
        // is ((high % d) * 2^32 + low) % d.
        inst_RV_RV(INS_cmp, REG_EDX, divisor.RegNum);
        inst_JMP(EJ_jb, noOverflow);

        var tempReg = _internalRegisters.GetSingle(node);
        inst_Mov(TYP_INT, tempReg, REG_EAX, canSkip: false);
        inst_Mov(TYP_INT, REG_EAX, REG_EDX, canSkip: false);
        instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_EDX);
        inst_RV(INS_div, divisor.RegNum, TYP_INT);
        inst_Mov(TYP_INT, REG_EAX, tempReg, canSkip: false);

        genDefineTempLabel(noOverflow);
        inst_RV(INS_div, divisor.RegNum, TYP_INT);

        inst_Mov(TYP_INT, node.RegNum, REG_RDX, canSkip: true);
        genProduceReg(node);
    }
}
#endif
