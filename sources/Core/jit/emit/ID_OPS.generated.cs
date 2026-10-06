// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.ID_OPS;

namespace RyuJitSharp;

public enum ID_OPS
{
#if TARGET_ARM
    ID_OP_NONE,                             // no additional arguments
    ID_OP_SCNS,                             // small const  operand (21-bits or less, no reloc)
    ID_OP_JMP,                              // local jump
    ID_OP_LBL,                              // label operand
    ID_OP_CALL,                             // direct method call
    ID_OP_SPEC,                             // special handling required
#elif TARGET_ARM64
    ID_OP_NONE, // no additional arguments
    ID_OP_SCNS, // small const  operand (21-bits or less, no reloc)
    ID_OP_JMP,  // local jump
    ID_OP_CALL, // method call
    ID_OP_SPEC, // special handling required
#elif TARGET_WASM
    ID_OP_NONE,
#elif TARGET_LOONGARCH64
    ID_OP_NONE, // no additional arguments
#else
    ID_OP_NONE,                             // no additional arguments
    ID_OP_SCNS,                             // small const  operand (21-bits or less, no reloc)
    ID_OP_CNS,                              // constant     operand
    ID_OP_DSP,                              // displacement operand
    ID_OP_DSP_CNS,                          // displacement + constant
    ID_OP_AMD,                              // addrmode with dsp
    ID_OP_AMD_CNS,                          // addrmode with dsp + constant
    ID_OP_JMP,                              // local jump
    ID_OP_LBL,                              // label operand
    ID_OP_CALL,                             // direct method call
    ID_OP_SPEC,                             // special handling required
#endif
}