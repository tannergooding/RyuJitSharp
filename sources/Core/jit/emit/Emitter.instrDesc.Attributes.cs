// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using GCtype = RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private opSize _idOpSize;
        private GCtype _idGCref;
        private regNumber _idReg1;
        private regNumber _idReg2;
        private bool _idCnsReloc;
        private bool _idDspReloc;

        public emitAttr idOpSize()
        {
            return emitDecodeSize(_idOpSize);
        }

        public void idOpSize(emitAttr size)
        {
#if TARGET_AMD64
            _idOpSize = (opSize)((uint)emitEncodeSize(size) & 7);
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Instruction operand-size fields outside AMD64 are not implemented.");
#endif
        }

        internal GCtype idGCref()
        {
            return _idGCref;
        }

        internal void idGCref(GCtype type)
        {
            _idGCref = (GCtype)((uint)type & 3);
        }

        public regNumber idReg1()
        {
            return _idReg1;
        }

        public void idReg1(regNumber reg)
        {
#if TARGET_AMD64
            _idReg1 = (regNumber)((uint)reg & ((1u << REGNUM_BITS) - 1));
            assert(reg == _idReg1);
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Instruction register fields outside AMD64 are not implemented.");
#endif
        }

        public regNumber idReg2()
        {
            return _idReg2;
        }

        public void idReg2(regNumber reg)
        {
#if TARGET_AMD64
            _idReg2 = (regNumber)((uint)reg & ((1u << REGNUM_BITS) - 1));
            assert(reg == _idReg2);
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Instruction register fields outside AMD64 are not implemented.");
#endif
        }

        public bool idIsCnsReloc()
        {
            return _idCnsReloc;
        }

        public void idSetIsCnsReloc()
        {
            _idCnsReloc = true;
        }

        public bool idIsDspReloc()
        {
            return _idDspReloc;
        }

        public void idSetIsDspReloc(bool value = true)
        {
            _idDspReloc = value;
        }
    }
}
