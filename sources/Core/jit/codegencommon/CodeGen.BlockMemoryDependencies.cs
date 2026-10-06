// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if !TARGET_XARCH && !TARGET_WASM && !TARGET_ARM && !TARGET_ARM64
    public void genCodeForStoreBlk(GenTreeBlk node)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Block memory generation requires Windows AMD64.");
    }
#endif

#if !TARGET_XARCH
    public void genCodeForMemmove(GenTreeBlk node)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Unrolled memmove requires Windows AMD64.");
    }
#endif

#if !TARGET_XARCH && !TARGET_WASM && !TARGET_ARM && !TARGET_ARM64
    public void genCodeForInitBlkLoop(GenTreeBlk node)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Loop block initialization requires Windows AMD64.");
    }
#endif

#if !TARGET_XARCH && !TARGET_LOONGARCH64 && !TARGET_RISCV64
    public void genCodeForInitBlkUnroll(GenTreeBlk node)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Unrolled block initialization requires Windows AMD64.");
    }
#endif

#if !TARGET_XARCH
    public void genCodeForCpBlkUnroll(GenTreeBlk node)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Unrolled block copy requires Windows AMD64.");
    }
#endif
}
