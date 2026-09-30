// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_Mov(var_types dstType, regNumber dstReg, regNumber srcReg, bool canSkip,
        emitAttr size = EA_UNKNOWN, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "WebAssembly register-copy selection is not implemented.");
    }
}
#endif
