// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private idAddrUnion _idAddrUnion;

        public struct idAddrUnion
        {
            public emitLclVarAddr iiaLclVar;
        }

        public ref idAddrUnion idAddr()
        {
            assert(!idIsSmallDsc());
            return ref _idAddrUnion;
        }
    }
}
