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
        public bool isPlacedAfterJmp;

        public override int NativeLogicalSize {
    get
    {
        bool __jitExceptionCondition291;
        __jitExceptionCondition291 = Configuration.TARGET_XARCH || Configuration.TARGET_ARM64;
        if (__jitExceptionCondition291)
        {
            return DescriptorSizes.Align;
        }
        else
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.AlignDescriptorSizeIsNotYetPortedForThisTarget);
            return default;
        }
    }
}
        public void idSetXarchCodeSize(uint size)
        {
            if (!Configuration.TARGET_XARCH)
            {
                FatalJitException.ThrowFatalJitException(
        CorJitResult.CORJIT_SKIPPED,
        FatalJitErrorCode.XarchAlignmentPaddingIsUnsupportedOnThisTarget
    );
                return;
            }
            idCodeSize(size);
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
