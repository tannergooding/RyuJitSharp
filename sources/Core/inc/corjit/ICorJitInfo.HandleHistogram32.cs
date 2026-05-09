// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct ICorJitInfo
{
    // Data structure for a single class probe using 32-bit count.
    //
    // CLASS_FLAG, INTERFACE_FLAG and DELEGATE_FLAG are placed into the Other field in the schema.
    // If CLASS_FLAG is set the handle table consists of type handles, and otherwise method handles.
    //
    // Count is the number of times a call was made at that call site.
    //
    // SIZE is the number of entries in the table.
    //
    // SAMPLE_INTERVAL must be >= SIZE. SAMPLE_INTERVAL / SIZE
    // gives the average number of calls between table updates.
    //
    public struct HandleHistogram32
    {
        public const int SIZE = 32;

        public const int SAMPLE_INTERVAL = 64;

        public const int CLASS_FLAG = int.MinValue;

        public const int INTERFACE_FLAG = 0x40000000;

        public const int DELEGATE_FLAG = 0x20000000;

        public const int OFFSET_MASK = 0x0FFFFFF;

        public int Count;

        public HandleTableInlineArray HandleTable;

        [InlineArray(SIZE)]
        public struct HandleTableInlineArray
        {
            public unsafe nint e0;
        }
    }
}
