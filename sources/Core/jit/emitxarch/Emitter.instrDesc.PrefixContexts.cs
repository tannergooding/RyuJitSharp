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
        private byte _idCustomBits;

        public bool idIsNoGC()
        {
            assert(!IsSimdInstruction(idIns()));
            return (_idCustomBits & 4) != 0;
        }

        public void idSetIsNoGC(bool value)
        {
            assert(!IsSimdInstruction(idIns()));
            _idCustomBits = (byte)((_idCustomBits & ~4) | (value ? 4 : 0));
        }

        public bool idIsEvexbContextSet() => (_idCustomBits & 0x30) != 0;

        public uint idGetEvexbContext() => (uint)(_idCustomBits >> 4) & 3;

        public void idSetEvexBroadcastBit()
        {
            assert(!idIsEvexbContextSet());
            _idCustomBits |= 0x10;
        }

        public void idSetEvexCompressedDisplacementBit()
        {
            assert((_idCustomBits & 0x20) == 0);
            _idCustomBits |= 0x20;
        }

        public void idSetEvexbContext(uint options)
        {
            assert(!idIsEvexbContextSet());
            _idCustomBits = (byte)(((uint)_idCustomBits & ~0x30u) | ((options & 3) << 4));
        }

        public bool idIsEvexAaaContextSet() => (_idCustomBits & 7) != 0;

        public uint idGetEvexAaaContext()
        {
            assert(idIns() >= FIRST_SSE_INSTRUCTION && idIns() <= LAST_AVX512_INSTRUCTION);
            return (uint)(_idCustomBits & 7);
        }

        public void idSetEvexAaaContext(uint options)
        {
            assert(idGetEvexAaaContext() == 0);
            _idCustomBits = (byte)(((uint)_idCustomBits & ~7u) | ((options >> 2) & 7));
        }

        public bool idIsEvexZContextSet()
        {
            assert(idIns() >= FIRST_SSE_INSTRUCTION && idIns() <= LAST_AVX512_INSTRUCTION);
            return (_idCustomBits & 8) != 0;
        }

        public void idSetEvexZContext()
        {
            assert(!idIsEvexZContextSet());
            _idCustomBits |= 8;
        }

        public bool idIsEvexNdContextSet()
        {
            assert((CodeGen.instInfo[(int)idIns()] & INS_FLAGS_HasNDD) != 0);
            return (_idCustomBits & 0x10) != 0;
        }

        public void idSetEvexNdContext()
        {
            assert(!idIsEvexNdContextSet());
            _idCustomBits |= 0x10;
        }

        public bool idIsEvexZuContextSet()
        {
#if TARGET_AMD64
            assert(idIns() >= INS_seto_apx && idIns() <= INS_setg_apx);
#endif
            return (_idCustomBits & 0x10) != 0;
        }

        public void idSetEvexZuContext()
        {
            assert(!idIsEvexZuContextSet());
            _idCustomBits |= 0x10;
        }

        public bool idIsEvexNfContextSet() => (_idCustomBits & 0x20) != 0;

        public void idSetEvexNfContext()
        {
            assert(!idIsEvexNfContextSet());
            _idCustomBits |= 0x20;
        }

        public bool idIsApxPpxContextSet() => (_idCustomBits & 0x40) != 0 && HasApxPpx(idIns());

        public void idSetApxPpxContext()
        {
            assert(!idIsApxPpxContextSet());
            _idCustomBits |= 0x40;
        }

        public bool idIsNoApxEvexPromotion() => (_idCustomBits & 0x40) != 0 && !HasApxPpx(idIns());

        public void idSetNoApxEvexPromotion()
        {
            assert(!idIsNoApxEvexPromotion());
            _idCustomBits |= 0x40;
        }

        public uint idGetEvexDFV() => (uint)(_idCustomBits & 0xF);

        public void idSetEvexDFV(uint options)
        {
            _idCustomBits = (byte)(((uint)_idCustomBits & ~0xFu) | ((options >> 8) & 0xF));
        }
    }
}
#endif
