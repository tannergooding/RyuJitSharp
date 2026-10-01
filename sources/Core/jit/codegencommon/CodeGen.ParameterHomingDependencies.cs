// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genLoadLocalIntoReg(regNumber reg, int localNumber)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm incoming stack argument load is not ported.");
    }
}
#endif
