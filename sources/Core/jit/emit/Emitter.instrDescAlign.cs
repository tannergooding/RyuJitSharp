// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_LOOP_ALIGN
namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescAlign : instrDesc
    {
        public instrDescAlign? idaNext;
        public insGroup? idaIG;
        public insGroup? idaLoopHeadPredIG;
#if DEBUG
        public bool isPlacedAfterJmp;
#endif

#if TARGET_AMD64
        public override int NativeLogicalSize => DescriptorSizes.Align;
#else
        public override int NativeLogicalSize => throw new System.PlatformNotSupportedException("Align descriptor size is not yet ported for this target.");
#endif

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
#endif
