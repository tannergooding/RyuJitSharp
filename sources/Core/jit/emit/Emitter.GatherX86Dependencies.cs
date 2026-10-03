// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool IsAVX2GatherInstruction(instruction ins)
    {
        return ins is INS_vpgatherdd or INS_vpgatherdq or INS_vpgatherqd or INS_vpgatherqq
            or INS_vgatherdps or INS_vgatherdpd or INS_vgatherqps or INS_vgatherqpd;
    }
}
#endif
