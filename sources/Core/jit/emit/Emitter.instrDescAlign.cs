// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescAlign : instrDesc
    {
        public instrDescAlign? idaNext;
        public insGroup? idaIG;
        public insGroup? idaLoopHeadPredIG;
#if DEBUG || LATE_DISASM
        public bool isPlacedAfterJmp;
#endif

        public override int NativeLogicalSize =>
            Configuration.TARGET_XARCH || Configuration.TARGET_ARM64
                ? DescriptorSizes.Align
                : throw new System.PlatformNotSupportedException("Align descriptor size is not yet ported for this target.");

        public void idSetXarchCodeSize(uint size)
        {
            if (!Configuration.TARGET_XARCH)
            {
                throw new FatalJitException(CorJitResult.CORJIT_SKIPPED,
                    "Xarch alignment padding is unsupported on this target.");
            }
#if TARGET_XARCH
            idCodeSize(size);
#else
            throw new FatalJitException(CorJitResult.CORJIT_SKIPPED,
                "Xarch alignment padding storage is not available on this target.");
#endif
        }

        public insGroup? loopHeadIG()
        {
            assert(idaLoopHeadPredIG is not null);
            return idaLoopHeadPredIG.igNext;
        }

        public void removeAlignFlags()
        {
            assert(idaIG is not null);
            idaIG.igFlags &= ~InsGroupFlags.HasAlign;
            idaIG.igFlags |= InsGroupFlags.RemovedAlign;
        }
    }
}
