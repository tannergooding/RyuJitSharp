// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Customizes the bit set to represent sets of tracked local vars.</summary>
/// <remarks>The size of the bitset is determined by the # of tracked locals (up to some internal maximum), and the Compiler* tracks the tracked local epochs</remarks>
public struct TrackedVarBitSetTraits : IBitSetTraits<Compiler>
{
    public static int GetArrSize(Compiler env) => env.lvaTrackedCount;

    public static int GetEpoch(Compiler env) => env.CurLVEpoch;

    public static int GetSize(Compiler env) => env.lvaTrackedCountInSizeTUnits;
}
