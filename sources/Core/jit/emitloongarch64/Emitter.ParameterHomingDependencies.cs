// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    public static bool isValidSimm12(nint value)
    {
        return (-2048 <= value) && (value < 2048);
    }

    public void emitIns_I_la(emitAttr attr, regNumber reg, nint immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 address constant recording is not ported.");
    }
}
#endif
