// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        public regNumber idReg3()
        {
            assert(!idIsSmallDsc());
            return (regNumber)(idAddr().iiaRegisterBits & ((1u << REGNUM_BITS) - 1));
        }

        public void idReg3(regNumber reg)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            bits = (bits & ~((1u << REGNUM_BITS) - 1)) | ((uint)reg & ((1u << REGNUM_BITS) - 1));
            assert(idReg3() == reg);
        }

        public regNumber idReg4()
        {
            assert(!idIsSmallDsc());
            return (regNumber)((idAddr().iiaRegisterBits >> REGNUM_BITS) & ((1u << REGNUM_BITS) - 1));
        }

        public void idReg4(regNumber reg)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            var mask = ((1u << REGNUM_BITS) - 1) << REGNUM_BITS;
            bits = (bits & ~mask) | (((uint)reg << REGNUM_BITS) & mask);
            assert(idReg4() == reg);
        }
    }
}
#endif
