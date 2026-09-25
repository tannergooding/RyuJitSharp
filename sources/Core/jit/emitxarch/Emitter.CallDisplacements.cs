// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    protected sealed class instrDescCGCA : instrDesc
    {
        public VARSET_TP idcGCvars = [];
        public nint idcDisp;
        public regMaskTP idcGcrefRegs;
        public regMaskTP idcByrefRegs;
        public uint idcArgCnt;
        private bool _hasAsyncContinuationRet;

        // emit.h:2484-2526: 16-byte base, pointer-sized varset and displacement,
        // two 16-byte AMD64 register masks, uint argument count, and a bool bitfield;
        // the native structure rounds to an eight-byte boundary.
#if UNIX_AMD64_ABI
        public override int NativeLogicalSize =>
            throw new FatalJitException(CORJIT_SKIPPED, "System V large-call descriptor layout is not implemented.");
#else
        public override int NativeLogicalSize => 72;
#endif

        public bool hasAsyncContinuationRet()
        {
            return _hasAsyncContinuationRet;
        }

        public void hasAsyncContinuationRet(bool value)
        {
            _hasAsyncContinuationRet = value;
        }
    }

    private nint emitGetInsCIdisp(instrDesc id)
    {
        if (id.idIsLargeCall())
        {
            return ((instrDescCGCA)id).idcDisp;
        }
        else
        {
            assert(!id.idIsLargeDsp());
            assert(!id.idIsLargeCns());
            return id.idAddr().iiaAddrMode.amDisp;
        }
    }
#endif
}
