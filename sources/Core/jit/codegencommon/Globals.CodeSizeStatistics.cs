// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DISPLAY_SIZES
namespace RyuJitSharp;

public static partial class Globals
{
    public static nuint grossVMsize;
    public static nuint grossNCsize;
    public static nuint totalNCsize;
}
#endif
