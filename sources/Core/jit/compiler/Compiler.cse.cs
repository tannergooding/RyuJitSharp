// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>BitVec trait information only used by the optCSE_canSwap() method, for the  CSE_defMask and CSE_useMask.</summary>
    protected BitVecTraits? cseMaskTraits;

    // BitVec trait information for computing CSE availability using the CSE_DataFlow algorithm.
    // Two bits are allocated per CSE candidate to compute CSE availability
    // plus an extra bit to handle the initial unvisited case.
    // (See CSE_DataFlow::EndMerge for an explanation of why this is necessary.)
    //
    // The two bits per CSE candidate have the following meanings:
    //     11 - The CSE is available, and is also available when considering calls as killing availability.
    //     10 - The CSE is available, but is not available when considering calls as killing availability.
    //     00 - The CSE is not available
    //     01 - An illegal combination
    //
    protected BitVecTraits? cseLivenessTraits;

    /// <summary>Computed once - A mask that is used to kill available CSEs at callsites</summary>
    protected unsafe EXPSET_TP cseCallKillsMask;

    /// <summary>Computed once - A mask that is used to kill available BYREF CSEs at async suspension points</summary>
    protected unsafe EXPSET_TP cseAsyncKillsMask;
}
