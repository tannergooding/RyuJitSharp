// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTreeFieldList
{
    public sealed class Use
    {
        private GenTree _node;

        private Use? _next;

        // We can save space on 32 bit hosts by storing the offset as ushort.
        // Struct promotion only accepts structs which are much smaller than that - 128 bytes = max 4 fields * max SIMD vector size (32 bytes).
        private ushort _offset;

        private var_types _type;

        public Use(GenTree node, ushort offset, var_types type)
        {
            _node = node;
            _next = null;
            _offset = offset;
            _type = type;
        }

        public Use? Next
        {
            get
            {
                return _next;
            }

            set
            {
                _next = value;
            }
        }

#nullable disable
        public ref Use NextRef => ref _next;
#nullable restore

        public GenTree Node
        {
            get
            {
                return _node;
            }

            set
            {
                assert(value is not null);
                _node = value;
            }
        }

#nullable disable
        public ref GenTree NodeRef => ref _node;
#nullable restore

        public ushort Offset => _offset;

        public var_types Type
        {
            get
            {
                return _type;
            }

            set
            {
                _type = value;
            }
        }
    }
}
