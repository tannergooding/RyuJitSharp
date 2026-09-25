// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static uint emitGetInsNumFromCodePos(uint codePos) => codePos & 0xFFFF;

    public static uint emitGetInsOfsFromCodePos(uint codePos) => codePos >> 16;
}
