// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Customizes the bit set to represent sets of all local vars (tracked or not) -- at least up to some maximum index.</summary>
/// <remarks>
///   <para>This index is private to the Compiler, and it is the responsibility of the compiler not to use indices &gt;= this maximum.</para>
///   <para>We rely on the fact that variables are never deleted, and therefore use the total # of locals as the epoch number (up to the maximum).</para>
/// </remarks>
public struct AllVarBitSetTraits : IBitSetTraits<Compiler>
{
    public static unsafe uint GetArrSize(Compiler env)
    {
        var elemBits = 8 * (uint)(sizeof(nuint));
        return roundUp(GetSize(env), elemBits) / elemBits;
    }

    public static uint GetEpoch(Compiler env) => GetSize(env);

    public static uint GetSize(Compiler env) => uint.Min(env.lvaCount, lclMAX_ALLSET_TRACKED);
}
