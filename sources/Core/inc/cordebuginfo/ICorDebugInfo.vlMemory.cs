// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_MEMORY
    public unsafe struct vlMemory
    {
        /// <summary>Pointer to the in-process location of the value.</summary>
        public void* rpValue;
    }
}
