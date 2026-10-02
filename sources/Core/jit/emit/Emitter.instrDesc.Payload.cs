// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
#if TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
        private const int REGNUM_BITS = 6;
#endif

#if TARGET_ARM
        private insSize _idInsSize;
        private insFlags _idInsFlags;

        public bool idInstrIsT1()
        {
            return _idInsSize == insSize.ISZ_16BIT;
        }

        public insSize idInsSize()
        {
            return _idInsSize;
        }

        public void idInsSize(insSize size)
        {
            _idInsSize = (insSize)((uint)size & 3);
            assert(size == _idInsSize);
        }

        public insFlags idInsFlags()
        {
            return _idInsFlags;
        }

        public void idInsFlags(insFlags flags)
        {
            _idInsFlags = (insFlags)((uint)flags & 1);
            assert(flags == _idInsFlags);
        }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
        private uint _idCodeSize;
        private insOpts _idInsOpt;

        public void idCodeSize(uint size)
        {
#if TARGET_LOONGARCH64
            // A descriptor can represent several instructions, such as an immediate load.
            assert(size <= 16);
            _idCodeSize = size & 31;
#else
            assert(size <= 32);
            _idCodeSize = size & 63;
#endif
        }

        public insOpts idInsOpt()
        {
            return _idInsOpt;
        }

        public void idInsOpt(insOpts options)
        {
            _idInsOpt = (insOpts)((uint)options & 63);
            assert(options == _idInsOpt);
        }

        public partial struct idAddrUnion
        {
#if TARGET_LOONGARCH64
            [FieldOffset(4)]
#else
            [FieldOffset(0)]
#endif
            internal uint iiaRegisterBits;
        }

        public regNumber idReg3()
        {
            assert(!idIsSmallDsc());
            return (regNumber)(idAddr().iiaRegisterBits & ((1u << REGNUM_BITS) - 1));
        }

        public void idReg3(regNumber reg)
        {
            assert(!idIsSmallDsc());
            ref var bits = ref idAddr().iiaRegisterBits;
            var mask = (1u << REGNUM_BITS) - 1;
            bits = (bits & ~mask) | ((uint)reg & mask);
            assert(reg == (regNumber)(idAddr().iiaRegisterBits & mask));
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
            assert(reg == (regNumber)((idAddr().iiaRegisterBits & mask) >> REGNUM_BITS));
        }
#endif

#if !TARGET_XARCH && !TARGET_ARM64
        private uint GetTargetCodeSize()
        {
#if TARGET_ARM
            uint result = _idInsSize == insSize.ISZ_16BIT ? 2u : _idInsSize == insSize.ISZ_32BIT ? 4u : 6u;
            return result;
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
            return _idCodeSize;
#elif TARGET_WASM
            return GetWasmCodeSize();
#else
#error Unsupported or unset target architecture
#endif
        }
#endif

#if TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
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
#endif

#if TARGET_WASM
        private uint GetWasmCodeSize()
        {
            throw new FatalJitException(CORJIT_SKIPPED, "emitter::instrDesc::idCodeSize is not yet ported for Wasm.");
        }
#endif
    }
}
