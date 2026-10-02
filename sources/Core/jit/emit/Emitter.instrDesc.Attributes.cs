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
#if !TARGET_XARCH
        private bool _idTlsGD;
#endif
#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
        private bool _idLclVar;
#endif
#if TARGET_ARM
        private bool _idLclFPBase;
#endif
#if TARGET_ARMARCH
        private insOpts _idInsOpt;
#endif

        public bool idIsBound()
        {
            assert(!IsSimdInstruction(idIns()));
            return (_idCustomBits & 1) != 0;
        }

        public void idSetIsBound()
        {
            assert(!IsSimdInstruction(idIns()));
            _idCustomBits |= 1;
        }

        public emitAttr idOpSize()
        {
            return emitDecodeSize(_idOpSize);
        }

        public void idOpSize(emitAttr size)
        {
#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            _idOpSize = (opSize)((uint)emitEncodeSize(size) & 7);
#else
            _idOpSize = (opSize)((uint)emitEncodeSize(size) & 3);
#endif
        }

#if TARGET_ARMARCH
        public insOpts idInsOpt()
        {
            return _idInsOpt;
        }

        public void idInsOpt(insOpts options)
        {
#if TARGET_ARM64
            _idInsOpt = (insOpts)((uint)options & 0x3F);
#else
            _idInsOpt = (insOpts)((uint)options & 7);
#endif
            assert(options == _idInsOpt);
        }
#endif

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
            _idReg1 = (regNumber)((uint)reg & ((1u << REGNUM_BITS) - 1));
            assert(reg == _idReg1);
        }

        public regNumber idReg2()
        {
            return _idReg2;
        }

        public void idReg2(regNumber reg)
        {
            _idReg2 = (regNumber)((uint)reg & ((1u << REGNUM_BITS) - 1));
            assert(reg == _idReg2);
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

        public void idSetRelocFlags(emitAttr attr)
        {
            _idCnsReloc = EA_IS_CNS_RELOC(attr);
            _idDspReloc = EA_IS_DSP_RELOC(attr);
        }

        public bool idIsReloc()
        {
            return idIsDspReloc() || idIsCnsReloc();
        }

        public bool idIsTlsGD()
        {
            assert(!IsSimdInstruction(idIns()));
#if TARGET_XARCH
            return (_idCustomBits & 2) != 0;
#else
            return _idTlsGD;
#endif
        }

        public void idSetTlsGD()
        {
            assert(!IsSimdInstruction(idIns()));
#if TARGET_XARCH
            _idCustomBits |= 2;
#else
            _idTlsGD = true;
#endif
        }

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
        public bool idIsLclVar()
        {
            return _idLclVar;
        }

        public void idSetIsLclVar()
        {
            _idLclVar = true;
        }
#endif

#if TARGET_ARM
        public bool idIsLclFPBase()
        {
            return _idLclFPBase;
        }

        public void idSetIsLclFPBase()
        {
            _idLclFPBase = true;
        }
#endif
    }
}
