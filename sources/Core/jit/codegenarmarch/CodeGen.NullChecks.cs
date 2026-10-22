// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForNullCheckArmArch(GenTreeIndir tree)
    {
        if (Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.GTNULLCHECKIsNotSupportedForARM32UseGTIND);
            return;
        }

        assert(tree.Oper is GT_NULLCHECK);
        genConsumeRegs(tree.Op1);

        genEmitNullCheckArm64(tree);
    }
}
