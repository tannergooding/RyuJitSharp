// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        internal GCInfo.GCtype idGCrefReg2()
        {
            assert(!idIsSmallDsc());
            return (GCInfo.GCtype)((idAddr().iiaRegisterBits >> 1) & 3);
        }

        internal void idGCrefReg2(GCInfo.GCtype type)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            bits = (bits & ~(3u << 1)) | (((uint)type & 3) << 1);
        }

        public bool idReg3Scaled()
        {
            assert(!idIsSmallDsc());
            return (idAddr().iiaRegisterBits & 1) != 0;
        }

        public void idReg3Scaled(bool value)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            bits = (bits & ~1u) | (value ? 1u : 0u);
        }

        public bool idPredicateReg2Merge()
        {
            assert(!idIsSmallDsc());
            return (idAddr().iiaRegisterBits & 1) != 0;
        }

        public void idPredicateReg2Merge(bool value)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            bits = (bits & ~1u) | (value ? 1u : 0u);
        }

        public bool idVectorLength4x()
        {
            assert(!idIsSmallDsc());
            return (idAddr().iiaRegisterBits & 1) != 0;
        }

        public void idVectorLength4x(bool value)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            bits = (bits & ~1u) | (value ? 1u : 0u);
        }

        public insSvePattern idSvePattern()
        {
            assert(!idIsSmallDsc());
            return idAddr().iiaSvePattern;
        }

        public void idSvePattern(insSvePattern pattern)
        {
            assert(!idIsSmallDsc());
            idAddr().iiaSvePattern = pattern;
        }

        public insSvePrfop idSvePrfop()
        {
            assert(!idIsSmallDsc());
            return (insSvePrfop)((idAddr().iiaRegisterBits >> (ID_REG3_SHIFT + REGNUM_BITS)) & ((1u << REGNUM_BITS) - 1));
        }

        public void idSvePrfop(insSvePrfop operation)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            var mask = ((1u << REGNUM_BITS) - 1) << (ID_REG3_SHIFT + REGNUM_BITS);
            bits = (bits & ~mask) | (((uint)operation << (ID_REG3_SHIFT + REGNUM_BITS)) & mask);
        }

        public bool idHasShift()
        {
            return !idIsSmallDsc() && ((idAddr().iiaRegisterBits & 1) != 0);
        }

        public void idHasShift(bool value)
        {
            if (!idIsSmallDsc())
            {
                ref var bits = ref idAddr().iiaRegisterBits;
                bits = (bits & ~1u) | (value ? 1u : 0u);
            }
        }
    }
}
#endif
