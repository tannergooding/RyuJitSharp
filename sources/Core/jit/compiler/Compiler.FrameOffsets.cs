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
        assert(lvaDoneFrameLayout == FINAL_FRAME_LAYOUT);
        assert(codeGen is not null);
        offset += isFpBased ? codeGen.genCallerSPtoFPdelta : codeGen.genCallerSPtoInitialSPdelta;
        if (Configuration.FEATURE_ON_STACK_REPLACEMENT)
        {
            if (forRootFrame && opts.IsOSR)
            {
                // TotalFrameSize accounts for the popped OSR pseudo return address, so the
                // root caller-SP adjustment is TotalFrameSize plus one register.
                var adjustment = info.compPatchpointInfo->TotalFrameSize +
                    (Configuration.TARGET_AMD64 ? Configuration.REGSIZE_BYTES : 0);
                offset -= adjustment;
            }
        }
        else
        {
            assert(!opts.IsOSR);
        }

        return offset;
    }

    public int lvaToInitialSPRelativeOffset(uint offset, bool isFpBased)
    {
        assert(lvaDoneFrameLayout == FINAL_FRAME_LAYOUT);
        if (!Configuration.TARGET_AMD64)
        {
            NYI("lvaToInitialSPRelativeOffset");
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LvaToInitialSPRelativeOffsetIsNotImplementedForThisTarget);
            return default!;
        }

        if (isFpBased)
        {
            // Native takes an unsigned offset even when the local's FP-relative offset is negative.
            assert(codeGen is not null);
            assert(codeGen.IsFramePointerUsed);
            offset = unchecked(offset + (uint)codeGen.genSPtoFPdelta);
        }

        return unchecked((int)offset);
    }
}
