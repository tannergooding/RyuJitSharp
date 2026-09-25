// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public insUpdateModes emitInsUpdateMode(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)emitInsModeFmtTab.Length);
        return (insUpdateModes)emitInsModeFmtTab[(int)ins];
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction update modes outside xarch are not implemented.");
#endif
    }

    public insFormat emitInsModeFormat(instruction ins, insFormat baseFormat, bool useNDD = false)
    {
#if TARGET_XARCH
        assert((uint)IF_RRD + (uint)IUM_RD == (uint)IF_RRD);
        assert((uint)IF_RRD + (uint)IUM_WR == (uint)IF_RWR);
        assert((uint)IF_RRD + (uint)IUM_RW == (uint)IF_RRW);

#if TARGET_AMD64
        if (useNDD)
        {
            assert(IsApxNddEncodableInstruction(ins));
            if (ins is INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N)
            {
                return IF_RWR_RRD_SHF;
            }

            // NDD writes a separate destination instead of updating the first source.
            return baseFormat switch
            {
                IF_RRD_RRD_RRD => IF_RWR_RRD_RRD,
                IF_RRD_RRD_ARD => IF_RWR_RRD_ARD,
                IF_RRD_RRD_CNS => IF_RWR_RRD_CNS,
                IF_RRD_RRD_SRD => IF_RWR_RRD_SRD,
                IF_RRD_RRD => IF_RWR_RRD,
                _ => throw new FatalJitException("Invalid instruction format for APX NDD."),
            };
        }
#endif
        return (insFormat)unchecked((uint)baseFormat + (uint)emitInsUpdateMode(ins));
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction format selection outside xarch is not implemented.");
#endif
    }

    public static IS_INFO emitGetSchedInfo(insFormat insFmt)
    {
#if TARGET_XARCH
        if ((uint)insFmt < (uint)emitFmtToSchedInfo.Length)
        {
            return emitFmtToSchedInfo[(int)insFmt];
        }

        assert(false, "Unsupported insFmt");
        return IS_NONE;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction scheduling metadata outside xarch is not implemented.");
#endif
    }

    public static bool IsApxNddCompatibleInstruction(instruction ins)
    {
#if TARGET_XARCH
        var flags = CodeGen.instInfo[(int)ins];
        return (flags & INS_FLAGS_HasNDD) != 0;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "APX instruction classification outside xarch is not implemented.");
#endif
    }

    public bool IsApxNddEncodableInstruction(instruction ins)
    {
#if TARGET_XARCH
        if (!UsePromotedEvexEncodings)
        {
            return false;
        }

        return IsApxNddCompatibleInstruction(ins);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "APX instruction classification outside xarch is not implemented.");
#endif
    }
}
