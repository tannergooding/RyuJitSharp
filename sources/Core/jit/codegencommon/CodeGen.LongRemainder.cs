// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLongUMod(GenTreeOp node)
    {
        if (!Configuration.TARGET_X86)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Long unsigned remainder generation requires X86.");
        }

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

        genCopyRegIfNeeded(dividendLo, REG_X86_EAX);
        genCopyRegIfNeeded(dividendHi, REG_X86_EDX);

        var noOverflow = genCreateTempLabel();
        // Reduce the high word first when necessary: (high * 2^32 + low) % d
        // is ((high % d) * 2^32 + low) % d.
        inst_RV_RV(INS_cmp, REG_X86_EDX, divisor.RegNum);
        inst_JMP(EJ_jb, noOverflow);

        var tempReg = _internalRegisters.GetSingle(node);
        inst_Mov(TYP_INT, tempReg, REG_X86_EAX, canSkip: false);
        inst_Mov(TYP_INT, REG_X86_EAX, REG_X86_EDX, canSkip: false);
        instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_X86_EDX);
        inst_RV(INS_div, divisor.RegNum, TYP_INT);
        inst_Mov(TYP_INT, REG_X86_EAX, tempReg, canSkip: false);

        genDefineTempLabel(noOverflow);
        inst_RV(INS_div, divisor.RegNum, TYP_INT);

        inst_Mov(TYP_INT, node.RegNum, REG_X86_EDX, canSkip: true);
        genProduceReg(node);
    }
}
