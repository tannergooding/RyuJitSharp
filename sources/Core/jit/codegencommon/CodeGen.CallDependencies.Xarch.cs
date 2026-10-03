// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private const regNumber REG_VIRTUAL_STUB_TARGET = REG_EAX;
    private const regNumber REG_PINVOKE_TCB = REG_ESI;
}
#endif
