// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescJmp : instrDesc
    {
        private uint _idjOffs;

        public instrDescJmp? idjNext;
        public insGroup? idjIG;
        public unsafe byte* idjAddr;

        // Native iiaBBlabel occupies the address union. Managed references cannot
        // overlap that union's scalar fields, so retain the target separately.
        public BasicBlock? idjTarget;

        public uint idjOffs
        {
            get
            {
                return _idjOffs;
            }
            set
            {
#if TARGET_AMD64
                _idjOffs = value & 0x0FFF_FFFF;
#elif TARGET_X86
                _idjOffs = value & 0x1FFF_FFFF;
#else
                _idjOffs = value & 0x3FFF_FFFF;
#endif
            }
        }

        public bool idjIsRemovableJmpCandidate;
#if TARGET_AMD64
        public bool idjIsAfterCallBeforeEpilog;
#endif
        public bool idjShort;
        public bool idjKeepLong;

#if TARGET_AMD64
        public override int NativeLogicalSize => DescriptorSizes.Jump;
#else
        public override int NativeLogicalSize => throw new System.PlatformNotSupportedException("Jump descriptor size is not yet ported for this target.");
#endif
    }
}
