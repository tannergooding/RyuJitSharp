// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct CORINFO_SWIFT_LOWERING
{
    public bool byReference;

    public InlineArrayMaxSwiftLoweredElements<CorInfoType> loweredElements;

    public InlineArrayMaxSwiftLoweredElements<int> offsets;

    public nint numLoweredElements;
}
