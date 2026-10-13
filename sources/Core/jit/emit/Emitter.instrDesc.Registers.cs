// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        // The ARM64 register word follows the local address and starts with
        // one shared option bit and the two-bit GC type of the second register.
        private static int ID_REG3_SHIFT => Configuration.TARGET_ARM64 ? 3 : 0;

        private static int RegisterBits => Configuration.REGNUM_BITS;

        public regNumber idReg3()
        {
            assert(!idIsSmallDsc());
            return (regNumber)((idAddr().iiaRegisterBits >> ID_REG3_SHIFT) & ((1u << RegisterBits) - 1));
        }

        public void idReg3(regNumber reg)
        {
            assert(!idIsSmallDsc());
            var bits = idAddr().iiaRegisterBits;
            var mask = ((1u << RegisterBits) - 1) << ID_REG3_SHIFT;
            bits = (bits & ~mask) | (((uint)reg << ID_REG3_SHIFT) & mask);
            idAddr().iiaRegisterBits = bits;
            assert(idReg3() == reg);
        }

        public regNumber idReg4()
        {
            assert(!idIsSmallDsc());
            return (regNumber)((idAddr().iiaRegisterBits >> (ID_REG3_SHIFT + RegisterBits)) & ((1u << RegisterBits) - 1));
        }

        public void idReg4(regNumber reg)
        {
            assert(!idIsSmallDsc());
            var bits = idAddr().iiaRegisterBits;
            var mask = ((1u << RegisterBits) - 1) << (ID_REG3_SHIFT + RegisterBits);
            bits = (bits & ~mask) | (((uint)reg << (ID_REG3_SHIFT + RegisterBits)) & mask);
            idAddr().iiaRegisterBits = bits;
            assert(idReg4() == reg);
        }

        public bool idHasShift()
        {
            return !idIsSmallDsc() && ((idAddr().iiaRegisterBits & 1) != 0);
        }

        public void idHasShift(bool value)
        {
            if (!idIsSmallDsc())
            {
                var bits = idAddr().iiaRegisterBits;
                bits = (bits & ~1u) | (value ? 1u : 0u);
                idAddr().iiaRegisterBits = bits;
            }
        }
    }
}
