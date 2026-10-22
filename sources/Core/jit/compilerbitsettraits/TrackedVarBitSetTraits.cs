// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Customizes the bit set to represent sets of tracked local vars.</summary>
/// <remarks>The size of the bitset is determined by the # of tracked locals (up to some internal maximum), and the Compiler* tracks the tracked local epochs</remarks>
public struct TrackedVarBitSetTraits : IBitSetTraits<Compiler>, IBitSetOpCounterTraits<Compiler>
{
    public static int GetArrSize(Compiler env) => env.lvaTrackedCountInSizeTUnits;

    public static int GetEpoch(Compiler env) => env.CurLVEpoch;

    public static int GetSize(Compiler env) => env.lvaTrackedCount;

    public static BitSetSupport.BitSetOpCounter? GetOpCounter(Compiler env)
    {
        if (!Configuration.VARSET_COUNTOPS)
        {
            return null;
        }

        global::RyuJitSharp.BitSetSupport.BitSetOpCounter __jitExceptionReturnResult285;
        var __jitExceptionCoalesceLeft286 = Compiler._varsetOpCounter;
        if (__jitExceptionCoalesceLeft286 != null)
        {
            __jitExceptionReturnResult285 = __jitExceptionCoalesceLeft286;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.VarsetOperationCounterIsNotInitialized);
            __jitExceptionReturnResult285 = default;
        }
        return __jitExceptionReturnResult285;
    }
}
