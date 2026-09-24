// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private SingleTypeRegSet allRegs(RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);

        if (registerType is TYP_UNDEF or TYP_STRUCT)
        {
            throw new FatalJitException("LSRA interval requires a concrete register type.");
        }

        if (registerType is TYP_DOUBLE)
        {
            return _availableDoubleRegs;
        }

        return registerType.Register switch
        {
            VTR_INT => _availableIntRegs,
            VTR_FLOAT => _availableFloatRegs,
#if FEATURE_MASKED_HW_INTRINSICS
            VTR_MASK => _availableMaskRegs,
#endif
            _ => throw new FatalJitException($"LSRA has no register bank for {registerType}."),
        };
    }

    private Interval newInterval(RegisterType registerType)
    {
        var interval = new Interval(registerType, allRegs(registerType));
        intervals.Add(interval);

#if DEBUG
        interval.intervalIndex = (uint)(intervals.Count - 1);
        if (VERBOSE)
        {
            dumpInterval(interval);
        }
#endif

        return interval;
    }
}
