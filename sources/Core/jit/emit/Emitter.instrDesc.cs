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
            _idScaledPrevOffset = (previousSize / 4) & 0x1F;
            assert(idPrevSize() == previousSize);
        }
#else
        public uint idCodeSize()
        {
            throw new PlatformNotSupportedException("Instruction descriptor code size is not yet ported for this target.");
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

        public bool idIsLargeCall()
        {
            return _idCall && _idLargeCns;
        }

        public void idSetIsLargeCall()
        {
            _idCall = true;
            _idLargeCns = true;
        }

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
