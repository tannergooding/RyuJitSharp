// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPutArgReg(GenTreeUnOp tree)
    {
#if !TARGET_XARCH && !TARGET_ARMARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Register argument generation requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(tree.Oper is GT_PUTARG_REG);
        var targetType = tree.Type;
        var targetReg = tree.RegNum;
#if !UNIX_AMD64_ABI
        assert(targetType != TYP_STRUCT);
#endif

        var operand = tree.Op1;
        _ = genConsumeReg(operand);

        inst_Mov(targetType, targetReg, operand.RegNum, canSkip: true);
        genProduceReg(tree);
#endif
    }
}
