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

        if (Configuration.DEBUG && (JitConfig.JitDoIfConversion == 0))
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var madeChanges = false;
        assert(!fgSsaValid);
        optReachableBitVecTraits = null;

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_XARCH || Configuration.TARGET_RISCV64)
        {
            var budget = new[] { 20000 };
            for (var block = fgLastBB; block is not null; block = block.Prev)
            {
                var descriptor = new OptIfConversionDsc(this, block);
                madeChanges |= descriptor.optIfConvert(budget);
            }
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
