// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreBlk(GenTreeBlk node)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForStoreBlkXarch(node);
        }
        else if (Configuration.TARGET_WASM)
        {
            genCodeForStoreBlkWasm(node);
        }
        else if (Configuration.TARGET_RISCV64 || Configuration.TARGET_LOONGARCH64)
        {
            genCodeForStoreBlkLoadStore64(node);
        }
        else if (Configuration.TARGET_ARMARCH)
        {
            genCodeForStoreBlkArmArch(node);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Block memory generation is not implemented for this target.");
        }
    }

    public void genCodeForMemmove(GenTreeBlk node)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForMemmoveXarch(node);
        }
        else if (Configuration.TARGET_ARM64)
        {
            genCodeForMemmoveArm64(node);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Unrolled memmove is not implemented for this target.");
        }
    }

    public void genCodeForInitBlkLoop(GenTreeBlk node)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForInitBlkLoopXarch(node);
        }
        else if (Configuration.TARGET_WASM)
        {
            genCodeForInitBlkLoopWasm(node);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genCodeForInitBlkLoopRiscV64(node);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForInitBlkLoopLoongArch64(node);
        }
        else if (Configuration.TARGET_ARMARCH)
        {
            genCodeForInitBlkLoopArmArch(node);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Loop block initialization is not implemented for this target.");
        }
    }

    public void genCodeForInitBlkUnroll(GenTreeBlk node)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForInitBlkUnrollXarch(node);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genCodeForInitBlkUnrollRiscV64(node);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForInitBlkUnrollLoongArch64(node);
        }
        else if (Configuration.TARGET_ARMARCH)
        {
            genCodeForInitBlkUnrollArmArch(node);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Unrolled block initialization is not implemented for this target.");
        }
    }

    public void genCodeForCpBlkUnroll(GenTreeBlk node)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForCpBlkUnrollXarch(node);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genCodeForCpBlkUnrollRiscV64(node);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForCpBlkUnrollLoongArch64(node);
        }
        else if (Configuration.TARGET_ARMARCH)
        {
            genCodeForCpBlkUnrollArmArch(node);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Unrolled block copy is not implemented for this target.");
        }
    }
}
