// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public regMask SRBM_ALLINT =>
        SRBM_EBX | SRBM_ESI | SRBM_EDI | SRBM_EAX | SRBM_ECX | SRBM_EDX;

    public regMask SRBM_ALLFLOAT =>
        SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM3 |
        SRBM_XMM4 | SRBM_XMM5 | SRBM_XMM6 | SRBM_XMM7;
}
#endif
