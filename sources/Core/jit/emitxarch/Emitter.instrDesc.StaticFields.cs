// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        public unsafe partial struct idAddrUnion
        {
            // Field handles also encode offsets into the JIT constant-data area.
            [FieldOffset(0)]
            public CORINFO_FIELD_HANDLE iiaFieldHnd;

            public readonly bool iiaIsJitDataOffset()
            {
                return Compiler.eeIsJitDataOffs(iiaFieldHnd);
            }

            public readonly int iiaGetJitDataOffset()
            {
                assert(iiaIsJitDataOffset());
                return Compiler.eeGetJitDataOffs(iiaFieldHnd);
            }
        }
    }
}
