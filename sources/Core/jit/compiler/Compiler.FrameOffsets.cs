// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe int lvaGetCallerSPRelativeOffset(int varNum)
    {
        assert(lvaDoneFrameLayout == FINAL_FRAME_LAYOUT);
        ref var descriptor = ref lvaGetDesc(varNum);
        assert(descriptor.lvOnFrame);
        return lvaToCallerSPRelativeOffset(descriptor.StackOffset, descriptor.lvFramePointerBased);
    }

    public unsafe int lvaToCallerSPRelativeOffset(int offset, bool isFpBased, bool forRootFrame = true)
    {
#if !TARGET_AMD64 && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Caller-SP-relative frame offsets require AMD64 or ARM64.");
#else
        assert(lvaDoneFrameLayout == FINAL_FRAME_LAYOUT);
        assert(codeGen is not null);
        offset += isFpBased ? codeGen.genCallerSPtoFPdelta : codeGen.genCallerSPtoInitialSPdelta;
#if FEATURE_ON_STACK_REPLACEMENT
        if (forRootFrame && opts.IsOSR)
        {
#if TARGET_AMD64
            // TotalFrameSize accounts for the popped OSR pseudo return address, so the
            // root caller-SP adjustment is TotalFrameSize plus one register.
            var adjustment = info.compPatchpointInfo->TotalFrameSize + REGSIZE_BYTES;
#else
            var adjustment = info.compPatchpointInfo->TotalFrameSize;
#endif
            offset -= adjustment;
        }
#else
        assert(!opts.IsOSR);
#endif

        return offset;
#endif
    }
}
