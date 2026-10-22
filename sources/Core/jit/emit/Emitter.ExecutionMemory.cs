// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    internal static insFormat getMemoryOperation(instrDesc id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.XarchExecutionMemoryClassificationIsUnavailableOnThisTarget
    );
            return default!;
        }

        var ins = id.idIns();
        var insFmt = id.idInsFmt();

        if (ins == INS_lea)
        {
            // LEA computes an address without accessing the memory named by its format.
            return IF_NONE;
        }

        return ExtractMemoryFormat(insFmt);
    }

    internal static insFormat ExtractMemoryFormat(insFormat insFmt)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.XarchExecutionMemoryClassificationIsUnavailableOnThisTarget
    );
            return default!;
        }

        var isInfo = emitGetSchedInfo(insFmt);
        var mask = isInfo & (IS_GM_RD | IS_GM_RW | IS_GM_WR);
        if (mask != 0)
        {
            var result = (insFormat)((uint)IF_XARCH_MRD + ((uint)mask >> 13));
            assert(result is IF_XARCH_MRD or IF_XARCH_MWR or IF_XARCH_MRW);
            return result;
        }

        mask = isInfo & (IS_SF_RD | IS_SF_RW | IS_SF_WR);
        if (mask != 0)
        {
            var result = (insFormat)((uint)IF_XARCH_SRD + ((uint)mask >> 16));
            assert(result is IF_XARCH_SRD or IF_XARCH_SWR or IF_XARCH_SRW);
            return result;
        }

        mask = isInfo & (IS_AM_RD | IS_AM_RW | IS_AM_WR);
        if (mask != 0)
        {
            var result = (insFormat)((uint)IF_XARCH_ARD + ((uint)mask >> 19));
            assert(result is IF_XARCH_ARD or IF_XARCH_AWR or IF_XARCH_ARW);
            return result;
        }

        return IF_NONE;
    }
}
