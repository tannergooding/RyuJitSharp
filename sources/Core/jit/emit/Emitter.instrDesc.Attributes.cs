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
        private bool _idTlsGD;
        private bool _idLclVar;
        private bool _idLclFPBase;
        private insOpts _idInsOpt;

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
            var mask = Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 ||
                Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64 ? 7u : 3u;
            _idOpSize = (opSize)((uint)emitEncodeSize(size) & mask);
        }

        public insOpts idInsOpt()
        {
            return _idInsOpt;
        }

        public void idInsOpt(insOpts options)
        {
            var mask = Configuration.TARGET_ARM ? 7u : 0x3Fu;
            _idInsOpt = (insOpts)((uint)options & mask);
            assert(options == _idInsOpt);
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
            var bits = Configuration.REGNUM_BITS;
            _idReg1 = (regNumber)((uint)reg & ((1u << bits) - 1));
            assert(reg == _idReg1);
        }

        public regNumber idReg2()
        {
            return _idReg2;
        }

        public void idReg2(regNumber reg)
        {
            var bits = Configuration.REGNUM_BITS;
            _idReg2 = (regNumber)((uint)reg & ((1u << bits) - 1));
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
            if (Configuration.TARGET_XARCH)
            {
                return (_idCustomBits & 2) != 0;
            }

            return _idTlsGD;
        }

        public void idSetTlsGD()
        {
            assert(!IsSimdInstruction(idIns()));
            if (Configuration.TARGET_XARCH)
            {
                _idCustomBits |= 2;
            }
            else
            {
                _idTlsGD = true;
            }
        }

        public bool idIsLclVar()
        {
            RequireLclVarTarget();
            return _idLclVar;
        }

        public void idSetIsLclVar()
        {
            RequireLclVarTarget();
            _idLclVar = true;
        }

        public bool idIsLclFPBase()
        {
            RequireArm32LclFPBase();
            return _idLclFPBase;
        }

        public void idSetIsLclFPBase()
        {
            RequireArm32LclFPBase();
            _idLclFPBase = true;
        }

        private static void RequireArm32LclFPBase()
        {
            if (!Configuration.TARGET_ARM)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "ARM32 local-frame-pointer descriptor flags are only supported on ARM32.");
                return;
            }
        }

        private static void RequireLclVarTarget()
        {
            if (!(Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 ||
                Configuration.TARGET_RISCV64))
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "Local-variable instruction descriptors are not supported for this target.");
                return;
            }
        }
    }
}
