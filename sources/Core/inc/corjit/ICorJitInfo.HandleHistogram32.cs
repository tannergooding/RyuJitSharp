// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
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
    public unsafe struct HandleHistogram32
    {
        public const int SIZE = 32;

        public const int SAMPLE_INTERVAL = 64;

        public const int CLASS_FLAG = unchecked((int)0x80000000);

        public const int INTERFACE_FLAG = 0x40000000;

        public const int DELEGATE_FLAG = 0x20000000;

        public const int OFFSET_MASK = 0x0FFFFFF;

        public uint Count;

        private _HandleTable_e__FixedBuffer _handleTable;

        [UnscopedRef]
        public Span<nuint> HandleTable => _handleTable;


        [InlineArray(SIZE)]
        private struct _HandleTable_e__FixedBuffer
        {
            public nuint e0;
        }
    }
}
