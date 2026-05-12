// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.SpecialCodeKind;

namespace RyuJitSharp;

// The SpecialCodeKind enum is used to indicate the type of special (unique)
// target block that will be targeted by an instruction.
// These are used by:
//   GenTreeBoundsChk nodes (SCK_RNGCHK_FAIL, SCK_ARG_EXCPN, SCK_ARG_RNG_EXCPN)
//     - these nodes have a field (gtThrowKind) to indicate which kind
//   GenTreeOps nodes, for which codegen will generate the branch
//     - it will use the appropriate kind based on the opcode, though it's not
//       clear why SCK_OVERFLOW == SCK_ARITH_EXCPN
public enum SpecialCodeKind
{
    SCK_NONE,

    // target when range check fails
    SCK_RNGCHK_FAIL,                

    // target for divide by zero (Not used on X86/X64)
    SCK_DIV_BY_ZERO,                

    // target on arithmetic exception
    SCK_ARITH_EXCPN,                

    // target on overflow
    SCK_OVERFLOW = SCK_ARITH_EXCPN, 

    // target on ArgumentException (currently used only for simd intrinsics)
    SCK_ARG_EXCPN,                  

    // target on ArgumentOutOfRangeException (currently used only for simd intrinsics)
    SCK_ARG_RNG_EXCPN,              

    // target for fail fast exception
    SCK_FAIL_FAST,

    // target for NullReferenceException (Wasm)
    SCK_NULL_CHECK,
    
    SCK_COUNT
}
