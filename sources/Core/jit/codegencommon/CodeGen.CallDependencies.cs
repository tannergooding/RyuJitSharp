// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCall(GenTreeCall call)
    {
        if (Configuration.UNIX_AMD64_ABI && call.Args.IsVarArgs)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnixAMD64VarargsAreUnsupported);
            return;
        }

        if (Configuration.TARGET_XARCH)
        {
            genCallXarch(call);
        }
        else if (Configuration.TARGET_WASM)
        {
            genCallWasm(call);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genCallRiscV64(call);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genCallLoongArch64(call);
        }
        else if (Configuration.TARGET_ARMARCH)
        {
            genCallArmArch(call);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.CallGenerationIsNotImplementedForThisTarget);
            return;
        }
    }
}
