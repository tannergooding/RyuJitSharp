// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    public abstract partial class instrDesc
    {
        public bool idIsCallRegPtr()
        {
            assert(!IsSimdInstruction(idIns()));
            return (_idCustomBits & 8) != 0;
        }

        public void idSetIsCallRegPtr()
        {
            assert(!IsSimdInstruction(idIns()));
            _idCustomBits |= 8;
        }

        public bool idIsTlsGD()
        {
            assert(!IsSimdInstruction(idIns()));
            return (_idCustomBits & 2) != 0;
        }

        public void idSetTlsGD()
        {
            assert(!IsSimdInstruction(idIns()));
            _idCustomBits |= 2;
        }
    }
#endif
}
