// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    public uint emitInsSizeSV(instrDesc id, ulong code, int var, int dsp)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 stack-variable instruction sizing is not ported.");

    public uint emitInsSizeSV(instrDesc id, ulong code, int var, int dsp, int val)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 stack-variable immediate sizing is not ported.");
}
#endif
