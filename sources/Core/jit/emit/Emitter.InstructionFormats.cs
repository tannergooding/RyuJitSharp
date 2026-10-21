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
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Instruction update modes outside xarch are not implemented.");
            return default!;
        }

        var nativeOrdinal = InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins);
        var updateModeFormats = emitInsModeFmtTab;
        assert((uint)nativeOrdinal < (uint)updateModeFormats.Length);
        return (insUpdateModes)updateModeFormats[nativeOrdinal];
    }

    public insFormat emitInsModeFormat(instruction ins, insFormat baseFormat, bool useNDD = false)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Instruction format selection outside xarch is not implemented.");
            return default!;
        }

        assert((uint)IF_XARCH_RRD + (uint)IUM_RD == (uint)IF_XARCH_RRD);
        assert((uint)IF_XARCH_RRD + (uint)IUM_WR == (uint)IF_XARCH_RWR);
        assert((uint)IF_XARCH_RRD + (uint)IUM_RW == (uint)IF_XARCH_RRW);

        if (Configuration.TARGET_64BIT && useNDD)
        {
            assert(IsApxNddEncodableInstruction(ins));
            if (ins is INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N)
            {
                return IF_XARCH_RWR_RRD_SHF;
            }

            // NDD writes a separate destination instead of updating the first source.
            switch (baseFormat)
            {
                case IF_XARCH_RRD_RRD_RRD:
                {
                    return IF_XARCH_RWR_RRD_RRD;
                }

                case IF_XARCH_RRD_RRD_ARD:
                {
                    return IF_XARCH_RWR_RRD_ARD;
                }

                case IF_XARCH_RRD_RRD_CNS:
                {
                    return IF_XARCH_RWR_RRD_CNS;
                }

                case IF_XARCH_RRD_RRD_SRD:
                {
                    return IF_XARCH_RWR_RRD_SRD;
                }

                case IF_XARCH_RRD_RRD:
                {
                    return IF_XARCH_RWR_RRD;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage212
                    );
                    return default!;
                }
            }
        }

        return (insFormat)unchecked((uint)baseFormat + (uint)emitInsUpdateMode(ins));
    }

    public static IS_INFO emitGetSchedInfo(insFormat insFmt)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Instruction scheduling metadata outside xarch is not implemented.");
            return default!;
        }

        if ((uint)insFmt < (uint)emitFmtToSchedInfo.Length)
        {
            return emitFmtToSchedInfo[(int)insFmt];
        }

        assert(false, "Unsupported insFmt");
        return IS_NONE;
    }

    public static bool IsApxNddCompatibleInstruction(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "APX instruction classification outside xarch is not implemented.");
            return default!;
        }

        var nativeOrdinal = InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins);
        var nddFlags = Configuration.TARGET_64BIT ? s_amd64InstructionNddFlags : s_x86InstructionNddFlags;
        assert((uint)nativeOrdinal < (uint)nddFlags.Length);
        return nddFlags[nativeOrdinal] != 0;
    }

    public bool IsApxNddEncodableInstruction(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "APX instruction classification outside xarch is not implemented.");
            return default!;
        }

        if (!Configuration.TARGET_64BIT)
        {
            return false;
        }

        if (!UsePromotedEvexEncodings)
        {
            return false;
        }

        return IsApxNddCompatibleInstruction(ins);
    }
}
