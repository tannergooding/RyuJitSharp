// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_I(instruction ins, emitAttr attr, regNumber reg, int imm,
        insFlags flags = insFlags.INS_FLAGS_DONT_CARE
#if DEBUG
        , GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM register-immediate recording with flags is not ported.");
    }
}
#endif
