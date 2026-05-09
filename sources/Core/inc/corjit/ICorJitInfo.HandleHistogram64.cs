// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct ICorJitInfo
{
    public struct HandleHistogram64
    {
        public long Count;

        public HandleTableInlineArray HandleTable;

        [InlineArray(HandleHistogram32.SIZE)]
        public struct HandleTableInlineArray
        {
            public unsafe nint e0;
        }
    }
}
