// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct ICorJitInfo
{
    public struct ValueHistogram32
    {
        public uint Count;

        public ValueTableInlineArray ValueTable;

        [InlineArray(HandleHistogram32.SIZE)]
        public struct ValueTableInlineArray
        {
            public nint e0;
        }
    }
}
