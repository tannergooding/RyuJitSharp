// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public regMask SRBM_ALLINT_X86 => Globals.SRBM_ALLINT_X86;

    public regMask SRBM_ALLFLOAT_X86 => Globals.SRBM_ALLFLOAT_X86;
}
