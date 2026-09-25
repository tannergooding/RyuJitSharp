// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        // Offsets are relative to the native descriptor buffer, after the optional debug-info
        // pointer. The managed object moves into group ownership without changing this layout.
        internal insGroup? StorageGroup;
        internal nuint StorageOffset;
        internal nuint StorageSize;
        internal int StorageIndex;
    }
}
