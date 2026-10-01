// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if EMITTER_STATS
    private static uint totAllocdSize;
    private static uint totActualSize;

    private static readonly Histogram emitSizeTable = new(
        [100, 1024 * 1, 1024 * 2, 1024 * 3, 1024 * 4, 1024 * 5, 1024 * 10, 0]);
    private static readonly Histogram GCrefsTable = new([0, 1, 2, 5, 10, 20, 50, 128, 256, 512, 1024, 0]);
    private static readonly Histogram stkDepthTable = new([0, 1, 2, 5, 10, 16, 32, 128, 1024, 0]);
#endif

#if DEBUG && TARGET_ARM64
    private void emitInsPairSanityCheck(instrDesc? previousId, instrDesc id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 instruction-pair sanity checking is not ported.");
    }
#endif

#if DEBUG && !TARGET_XARCH
    private void emitDispGCInfoDelta()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Non-xarch GC delta diagnostics are not ported.");
    }

    private void emitDispInsIndent()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Non-xarch instruction indentation is not ported.");
    }
#endif
}
