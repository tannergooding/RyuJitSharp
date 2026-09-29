// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH || TARGET_ARM64
    protected sealed class instrDescCGCA : instrDesc
    {
        public VARSET_TP idcGCvars = [];
        public nint idcDisp;
        public regMaskTP idcGcrefRegs;
        public regMaskTP idcByrefRegs;
        public uint idcArgCnt;
        private bool _hasAsyncContinuationRet;

#if MULTIREG_HAS_SECOND_GC_RET
        private GCInfo.GCtype _secondRetRegGCType;

        internal GCInfo.GCtype idSecondGCref()
        {
            return _secondRetRegGCType;
        }

        internal void idSecondGCref(GCInfo.GCtype type)
        {
            _secondRetRegGCType = type;
        }
#endif

        // emit.h:2484-2526: 16-byte base, pointer-sized varset and displacement,
        // two 16-byte AMD64 register masks, uint argument count, then a shared
        // bitfield allocation for the SysV second-return GC type and async bit.
        // Both AMD64 layouts round to the same eight-byte boundary.
#if TARGET_AMD64
        public override int NativeLogicalSize => 72;
#elif TARGET_ARM64
        public override int NativeLogicalSize => Arm64CallDescriptorSize();
#else
        public override int NativeLogicalSize
            => throw new System.PlatformNotSupportedException("The x86 call descriptor layout is not yet ported.");
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
#endif

#if TARGET_XARCH
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
