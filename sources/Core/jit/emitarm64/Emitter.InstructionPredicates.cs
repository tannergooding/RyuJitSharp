// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.insOpts;

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool isValidScalableDatasize(emitAttr size)
    {
        return (size & EA_SCALABLE) == EA_SCALABLE;
    }

    public static bool isValidVectorElemsizeSveFloat(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE);
    }

    public static bool isValidVectorElemsizeWidening(emitAttr size)
    {
        return (size == EA_4BYTE) || (size == EA_2BYTE) || (size == EA_1BYTE);
    }

    public static bool isMaskReg(regNumber reg)
    {
        return isPredicateRegister(reg);
    }

    public static bool insOptsVectorImmShift(insOpts opt)
    {
        return (opt == INS_OPTS_LSL) || (opt == INS_OPTS_MSL);
    }

    public static bool insOptsLSR(insOpts opt)
    {
        return opt == INS_OPTS_LSR;
    }

    public static bool insOptsASR(insOpts opt)
    {
        return opt == INS_OPTS_ASR;
    }

    public static bool insOptsROR(insOpts opt)
    {
        return opt == INS_OPTS_ROR;
    }

    public static bool insOpts64BitExtend(insOpts opt)
    {
        return (opt == INS_OPTS_UXTX) || (opt == INS_OPTS_SXTX);
    }
}
#endif
