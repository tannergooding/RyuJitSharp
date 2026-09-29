// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    public static insOpts optGetSveInsOpt(emitAttr elemsize)
    {
        switch (elemsize)
        {
            case EA_1BYTE:
            {
                return INS_OPTS_SCALABLE_B;
            }

            case EA_2BYTE:
            {
                return INS_OPTS_SCALABLE_H;
            }

            case EA_4BYTE:
            {
                return INS_OPTS_SCALABLE_S;
            }

            case EA_8BYTE:
            {
                return INS_OPTS_SCALABLE_D;
            }

            case EA_16BYTE:
            {
                return INS_OPTS_SCALABLE_Q;
            }

            default:
            {
                assert(false, "Invalid emitAttr for sve vector register");
                return INS_OPTS_NONE;
            }
        }
    }

    public void emitIns_R_PATTERN(instruction ins, emitAttr attr, regNumber reg1, insOpts opt,
        insSvePattern pattern = insSvePattern.SVE_PATTERN_ALL)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE register-pattern instruction recording is not ported.");
}
#endif
