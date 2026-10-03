// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForSwap(GenTreeOp tree)
    {
        NYI("unimplemented on LOONGARCH64 yet");
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 GT_SWAP generation is not ported.");
    }
}
#endif
