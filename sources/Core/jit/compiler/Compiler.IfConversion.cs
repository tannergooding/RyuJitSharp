// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus optIfConversion()
    {
        if (!opts.OptimizationEnabled)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (JitConfig.JitDoIfConversion == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif

        var madeChanges = false;
        assert(!fgSsaValid);
        optReachableBitVecTraits = null;

#if TARGET_ARM64 || TARGET_XARCH || TARGET_RISCV64
        var budget = new[] { 20000 };
        for (var block = fgLastBB; block is not null; block = block.Prev)
        {
            var descriptor = new OptIfConversionDsc(this, block);
            madeChanges |= descriptor.optIfConvert(budget);
        }
#endif
        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
