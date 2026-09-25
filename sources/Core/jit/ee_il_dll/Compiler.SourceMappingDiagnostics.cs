// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public partial class Compiler
{
    public static void eeDispILOffs(IL_OFFSET offset)
    {
        jitprintf($"0x{offset:X4}");
    }
}
#endif
