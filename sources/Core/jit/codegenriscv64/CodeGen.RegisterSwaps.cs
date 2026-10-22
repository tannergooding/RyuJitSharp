// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForSwapRiscV64(GenTreeOp tree)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genCodeForSwap-----unimplemented/unused on RISCV64 yet----");
        }
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.RISCV64GTSWAPGenerationIsNotPorted);
        return;
    }
}
