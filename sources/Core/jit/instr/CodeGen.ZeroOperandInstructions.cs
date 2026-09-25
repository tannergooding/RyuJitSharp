// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen(instruction ins)
    {
#if TARGET_XARCH
        Emitter.emitIns(ins);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Zero-operand code generation outside xarch is not implemented.");
#endif
    }
}
