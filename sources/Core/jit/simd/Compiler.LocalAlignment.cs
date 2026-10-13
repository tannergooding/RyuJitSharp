// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool isSIMDTypeLocalAligned(int varNum)
    {
        if (Configuration.FEATURE_SIMD && Configuration.ALIGN_SIMD_TYPES && !Configuration.UNIX_X86_ABI)
        {
            ref var local = ref lvaGetDesc(varNum);

            if (varTypeIsSimd(local.Type))
            {
                // TODO-Cleanup: Can't this use the lvExactSize on the varDsc?
                var alignment = getSIMDTypeAlignment(local.Type);

                if (alignment <= Configuration.STACK_ALIGN)
                {
                    var offset = lvaFrameAddress(varNum, out var fpBased);

                    if (fpBased)
                    {
                        // AMD64 frame pointers are 16-byte aligned.
                        return (offset % alignment) == 0;
                    }

                    // RSP + 8 is aligned on entry; subtracting the frame size
                    // determines the alignment of an SP-based local.
                    assert(codeGen is not null);
                    var frameSize = codeGen.genTotalFrameSize;
                    return (unchecked(8 - frameSize + offset) % alignment) == 0;
                }
            }
        }

        return false;
    }

    private static int getSIMDTypeAlignment(var_types simdType)
    {
        var size = simdType.Size;

        if (Configuration.TARGET_XARCH)
        {
            if (size == 8)
            {
                return 8;
            }

            if (size <= 16)
            {
                assert(size is 12 or 16);
                return 16;
            }

            if (size == 32)
            {
                return 32;
            }

            assert(size == 64);
            return 64;
        }

        if (Configuration.TARGET_ARM64)
        {
            return size == 8 ? 8 : 16;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "SIMD local alignment for this target is not implemented.");
    }
}
