// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    protected internal class instrDescJmp : instrDesc
    {
        private uint _idjOffs;

        public instrDescJmp? idjNext;
        public insGroup? idjIG;
        public unsafe byte* idjAddr;

        // Native iiaBBlabel occupies the address union. Managed references cannot
        // overlap that union's scalar fields, so retain the target separately.
        // Native stores these targets in a union, distinguished by idIsBound().
        public BasicBlock? idjTarget;
        public insGroup? idjTargetIG;

        public uint idjOffs
        {
            get
            {
                return _idjOffs;
            }
            set
            {
                var mask = Configuration.TARGET_AMD64 ? 0x0FFF_FFFFu :
                    Configuration.TARGET_X86 ? 0x1FFF_FFFFu : 0x3FFF_FFFFu;
                _idjOffs = value & mask;
            }
        }

        public bool idjIsRemovableJmpCandidate;
        public bool idjIsAfterCallBeforeEpilog;
        public bool idjShort;
        public bool idjKeepLong;

        public override int NativeLogicalSize
        {
            get
            {
                if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM || Configuration.TARGET_ARM64 ||
                    Configuration.TARGET_RISCV64)
                {
                    return DescriptorSizes.Jump;
                }

                throw new System.PlatformNotSupportedException("Jump descriptor size is not yet ported for this target.");
            }
        }
    }
}
