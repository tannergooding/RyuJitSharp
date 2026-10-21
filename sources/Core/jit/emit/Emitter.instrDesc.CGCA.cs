// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescCGCA : instrDesc
    {
        public VARSET_TP idcGCvars = [];
        public nint idcDisp;
        public regMaskTP idcGcrefRegs;
        public regMaskTP idcByrefRegs;
        public uint idcArgCnt;
        private bool _hasAsyncContinuationRet;

        private GCInfo.GCtype _secondRetRegGCType;

        internal GCInfo.GCtype idSecondGCref()
        {
            return _secondRetRegGCType;
        }

        internal void idSecondGCref(GCInfo.GCtype type)
        {
            _secondRetRegGCType = type;
        }

        // emit.h:2484-2526: 16-byte base, pointer-sized varset and displacement,
        // two 16-byte AMD64 register masks, uint argument count, then a shared
        // bitfield allocation for the SysV second-return GC type and async bit.
        // Both AMD64 layouts round to the same eight-byte boundary.
        internal static int NativeSize
        {
            get
            {
                if (Configuration.TARGET_AMD64)
                {
                    return 72;
                }

                if (Configuration.TARGET_ARM64)
                {
                    return Arm64CallDescriptorSize();
                }

                if (Configuration.TARGET_ARM)
                {
                    // The 12-byte base, 4-byte varset/displacement/count, and two 8-byte masks round to 48 bytes.
                    return 48;
                }

                if (Configuration.TARGET_RISCV64)
                {
                    // LP64 layout: 16-byte base, 8-byte varset/displacement and masks, 4-byte argument count,
                    // then the one-byte tail bitfield rounded up to the next 8-byte boundary.
                    return 56;
                }

                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "The call descriptor layout is not ported for this target.");
                return default!;
            }
        }

        public override int NativeLogicalSize => NativeSize;

        public bool hasAsyncContinuationRet()
        {
            return _hasAsyncContinuationRet;
        }

        public void hasAsyncContinuationRet(bool value)
        {
            _hasAsyncContinuationRet = value;
        }
    }
}
