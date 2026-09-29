// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH || TARGET_ARMARCH
namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
#if TARGET_ARM64
        // The ARM64 register word follows the local address and starts with
        // one shared option bit and the two-bit GC type of the second register.
        private const int ID_REG3_SHIFT = 3;
#else
        private const int ID_REG3_SHIFT = 0;
#endif

        public regNumber idReg3()
        {
            assert(!idIsSmallDsc());
            return (regNumber)((idAddr().iiaRegisterBits >> ID_REG3_SHIFT) & ((1u << REGNUM_BITS) - 1));
        }

        public void idReg3(regNumber reg)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            var mask = ((1u << REGNUM_BITS) - 1) << ID_REG3_SHIFT;
            bits = (bits & ~mask) | (((uint)reg << ID_REG3_SHIFT) & mask);
            assert(idReg3() == reg);
        }

        public regNumber idReg4()
        {
            assert(!idIsSmallDsc());
            return (regNumber)((idAddr().iiaRegisterBits >> (ID_REG3_SHIFT + REGNUM_BITS)) & ((1u << REGNUM_BITS) - 1));
        }

        public void idReg4(regNumber reg)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            var mask = ((1u << REGNUM_BITS) - 1) << (ID_REG3_SHIFT + REGNUM_BITS);
            bits = (bits & ~mask) | (((uint)reg << (ID_REG3_SHIFT + REGNUM_BITS)) & mask);
            assert(idReg4() == reg);
        }
    }
}
#endif
