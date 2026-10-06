// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
#if TARGET_XARCH
        private uint _idCodeSize;
#endif
        private instruction _idIns;
        private bool _idSmallDsc;
        private bool _idLargeCns;
        private bool _idLargeDsp;
        private bool _idCall;
        private byte _idCustomBits;
#if TARGET_XARCH
        private uint _idScaledPrevOffset;
#endif
        private instrDescDebugInfo? _debugInfo;

        public abstract int NativeLogicalSize { get; }

        public instruction idIns()
        {
            return _idIns;
        }

        public void idIns(instruction ins)
        {
            assert(ins is not INS_invalid && ins < INS_count);
            _idIns = ins;
        }

        public bool idInsIs(instruction ins)
        {
            return idIns() == ins;
        }

        public bool idInsIs(instruction ins, params ReadOnlySpan<instruction> rest)
        {
            if (idInsIs(ins))
            {
                return true;
            }

            foreach (var candidate in rest)
            {
                if (idInsIs(candidate))
                {
                    return true;
                }
            }

            return false;
        }

#if TARGET_XARCH
        public uint idCodeSize()
        {
            return _idCodeSize;
        }

        public void idCodeSize(uint size)
        {
            assert(size <= 15);
            _idCodeSize = size & 0xF;
            assert(size == _idCodeSize);
        }

        public uint idPrevSize()
        {
            return _idScaledPrevOffset * 4;
        }

        public void idSetPrevSize(uint previousSize)
        {
            assert((previousSize % 4) == 0);
#if HOST_64BIT
            _idScaledPrevOffset = (previousSize / 4) & 0x1F;
#else
            _idScaledPrevOffset = (previousSize / 4) & 0xF;
#endif
            assert(idPrevSize() == previousSize);
        }
#elif TARGET_ARM64
        public bool idIsEmptyAlign()
        {
            return (idIns() == INS_align) && (idInsOpt() == INS_OPTS_NONE);
        }

        public uint idCodeSize()
        {
            uint size = 4;
            switch (idInsFmt())
            {
                case insFormat.IF_LARGEADR:
                case insFormat.IF_LARGEJMP:
                {
                    // adrp + add, or a conditional branch followed by an unconditional branch.
                    size = 8;
                    break;
                }

                case insFormat.IF_LARGELDC:
                {
                    // Vectors need adrp + ldr + fmov (or adrp + add + ld1); scalars need adrp + ldr.
                    size = idReg1() is >= REG_V0 and <= REG_V31 ? 12u : 8u;
                    break;
                }

                case insFormat.IF_SN_0A:
                {
                    if (idIsEmptyAlign())
                    {
                        size = 0;
                    }
                    break;
                }
            }

            return size;
        }
#else
        public uint idCodeSize()
        {
            return GetTargetCodeSize();
        }
#endif

        public bool idIsSmallDsc()
        {
            return _idSmallDsc;
        }

        public void idSetIsSmallDsc()
        {
            _idSmallDsc = true;
        }

        public bool idIsLargeCns()
        {
            return _idLargeCns && !_idCall;
        }

        public void idSetIsLargeCns()
        {
            _idLargeCns = true;
        }

        public bool idIsLargeDsp()
        {
            return _idLargeDsp;
        }

        public void idSetIsLargeDsp()
        {
            _idLargeDsp = true;
        }

        public bool idIsCall()
        {
            return _idCall;
        }

        public void idSetIsCall()
        {
            _idCall = true;
        }

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

        public bool idIsLargeCall()
        {
            return _idCall && _idLargeCns;
        }

        public void idSetIsLargeCall()
        {
            _idCall = true;
            _idLargeCns = true;
        }

#if TARGET_WASM
        public bool idIsLclVarDecl()
        {
            return idInsFmt() == insFormat.IF_LOCAL_DECL;
        }

        public bool idIsValTypeImm()
        {
            return idInsFmt() == insFormat.IF_TRY_TABLE;
        }

        public bool idIsV128Imm()
        {
            return idInsFmt() == insFormat.IF_V128;
        }

        public bool idIsMemargLaneImm()
        {
            return idInsFmt() == insFormat.IF_MEMARG_LANE;
        }
#endif

        public instrDescDebugInfo? idDebugOnlyInfo()
        {
            return _debugInfo;
        }

        public void idDebugOnlyInfo(instrDescDebugInfo? info)
        {
            _debugInfo = info;
        }
    }
}
