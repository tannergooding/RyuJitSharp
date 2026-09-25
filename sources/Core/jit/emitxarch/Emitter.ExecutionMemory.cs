// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    internal static insFormat getMemoryOperation(instrDesc id)
    {
        // LEA computes an address without accessing the memory named by its format.
        return id.idIns() == INS_lea ? IF_NONE : ExtractMemoryFormat(id.idInsFmt());
    }

    internal static insFormat ExtractMemoryFormat(insFormat insFmt)
    {
        var isInfo = emitGetSchedInfo(insFmt);
        var mask = isInfo & (IS_GM_RD | IS_GM_RW | IS_GM_WR);
        if (mask != 0)
        {
            var result = (insFormat)((uint)IF_MRD + ((uint)mask >> 13));
            assert(result is IF_MRD or IF_MWR or IF_MRW);
            return result;
        }

        mask = isInfo & (IS_SF_RD | IS_SF_RW | IS_SF_WR);
        if (mask != 0)
        {
            var result = (insFormat)((uint)IF_SRD + ((uint)mask >> 16));
            assert(result is IF_SRD or IF_SWR or IF_SRW);
            return result;
        }

        mask = isInfo & (IS_AM_RD | IS_AM_RW | IS_AM_WR);
        if (mask != 0)
        {
            var result = (insFormat)((uint)IF_ARD + ((uint)mask >> 19));
            assert(result is IF_ARD or IF_AWR or IF_ARW);
            return result;
        }

        return IF_NONE;
    }
#else
    internal static insFormat getMemoryOperation(instrDesc id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Xarch execution memory classification is unavailable on this target.");
    }

    internal static insFormat ExtractMemoryFormat(insFormat insFmt)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Xarch execution memory classification is unavailable on this target.");
    }
#endif
}
