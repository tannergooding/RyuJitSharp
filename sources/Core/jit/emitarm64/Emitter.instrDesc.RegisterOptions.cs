// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        internal GCInfo.GCtype idGCrefReg2()
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            return (GCInfo.GCtype)((idAddr().iiaRegisterBits >> 1) & 3);
        }

        internal void idGCrefReg2(GCInfo.GCtype type)
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            var bits = idAddr().iiaRegisterBits;
            bits = (bits & ~(3u << 1)) | (((uint)type & 3) << 1);
            idAddr().iiaRegisterBits = bits;
        }

        public bool idReg3Scaled()
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            return (idAddr().iiaRegisterBits & 1) != 0;
        }

        public void idReg3Scaled(bool value)
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            var bits = idAddr().iiaRegisterBits;
            bits = (bits & ~1u) | (value ? 1u : 0u);
            idAddr().iiaRegisterBits = bits;
        }

        public bool idPredicateReg2Merge()
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            return (idAddr().iiaRegisterBits & 1) != 0;
        }

        public void idPredicateReg2Merge(bool value)
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            var bits = idAddr().iiaRegisterBits;
            bits = (bits & ~1u) | (value ? 1u : 0u);
            idAddr().iiaRegisterBits = bits;
        }

        public bool idVectorLength4x()
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            return (idAddr().iiaRegisterBits & 1) != 0;
        }

        public void idVectorLength4x(bool value)
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            var bits = idAddr().iiaRegisterBits;
            bits = (bits & ~1u) | (value ? 1u : 0u);
            idAddr().iiaRegisterBits = bits;
        }

        public insSvePattern idSvePattern()
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            return idAddr().iiaSvePattern;
        }

        public void idSvePattern(insSvePattern pattern)
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            idAddr().iiaSvePattern = pattern;
        }

        public insSvePrfop idSvePrfop()
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            return (insSvePrfop)((idAddr().iiaRegisterBits >> (ID_REG3_SHIFT + REGNUM_BITS_ARM64)) & ((1u << REGNUM_BITS_ARM64) - 1));
        }

        public void idSvePrfop(insSvePrfop operation)
        {
            Emitter.RequireArm64Target();
            assert(!idIsSmallDsc());
            var bits = idAddr().iiaRegisterBits;
            var mask = ((1u << REGNUM_BITS_ARM64) - 1) << (ID_REG3_SHIFT + REGNUM_BITS_ARM64);
            bits = (bits & ~mask) | (((uint)operation << (ID_REG3_SHIFT + REGNUM_BITS_ARM64)) & mask);
            idAddr().iiaRegisterBits = bits;
        }
    }
}
