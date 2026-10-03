// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_XARCH && !TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCall(GenTreeCall call)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Call generation requires xarch.");
    }
}
#endif
