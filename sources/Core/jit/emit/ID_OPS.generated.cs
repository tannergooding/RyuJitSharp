// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.ID_OPS;

namespace RyuJitSharp;

public enum ID_OPS
{
    ID_OP_NONE,
    ID_OP_SCNS,
    ID_OP_CNS,
    ID_OP_DSP,
    ID_OP_DSP_CNS,
    ID_OP_AMD,
    ID_OP_AMD_CNS,
    ID_OP_JMP,
    ID_OP_LBL,
    ID_OP_CALL,
    ID_OP_SPEC,
}