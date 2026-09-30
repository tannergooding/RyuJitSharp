// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, insFlags flags)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 three-register instruction recording is not implemented.");
    }
}
#endif
