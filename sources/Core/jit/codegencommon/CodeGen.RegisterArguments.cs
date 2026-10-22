// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPutArgReg(GenTreeUnOp tree)
    {
        if (Configuration.TARGET_LOONGARCH64)
        {
            genPutArgRegLoongArch64(tree);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genPutArgRegRiscV64(tree);
        }
        else if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARMARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RegisterArgumentGenerationIsNotPortedForThisTarget
    );
            return;
        }
        else
        {
            if (Configuration.TARGET_AMD64)
            {
                Emitter.RequireSupportedInstructionRecording();
            }

            assert(tree.Oper is GT_PUTARG_REG);
            var targetType = tree.Type;
            var targetReg = tree.RegNum;
            if (!Configuration.UNIX_AMD64_ABI)
            {
                assert(targetType != TYP_STRUCT);
            }

            var operand = tree.Op1;
            _ = genConsumeReg(operand);

            inst_Mov(targetType, targetReg, operand.RegNum, canSkip: true);
            genProduceReg(tree);
        }
    }
}
