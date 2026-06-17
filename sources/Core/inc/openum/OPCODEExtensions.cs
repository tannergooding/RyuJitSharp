// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class OPCODEExtensions
{
    extension(OPCODE opcode)
    {
#if DEBUG
        public OPCODE_FORMAT ArgKind
        {
            get
            {
                assert(s_argKinds.Length == (int)(CEE_COUNT));
                return s_argKinds[(int)(opcode)];
            }
        }

        public OpFlow FlowKind
        {
            get
            {
                assert(s_flowKinds.Length == (int)(CEE_COUNT));
                return s_flowKinds[(int)(opcode)];
            }
        }

        public string Name
        {
            get
            {
                assert(s_names.Length == (int)(CEE_COUNT));
                return s_names[(int)(opcode)];
            }
        }
#endif

        public byte Size
        {
            get
            {
                assert(s_sizes.Length == (int)(CEE_COUNT));
                return s_sizes[(int)(opcode)];
            }
        }
    }
}
