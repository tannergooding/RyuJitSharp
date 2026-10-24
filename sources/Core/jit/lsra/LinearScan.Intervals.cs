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
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.LSRAIntervalRequiresAConcreteRegisterType
    );
            return default!;
        }

        if (registerType is TYP_DOUBLE)
        {
            return _availableDoubleRegs;
        }

        switch (registerType.Register)
        {
            case VTR_INT:
            {
                return _availableIntRegs;
            }

            case VTR_FLOAT:
            {
                return _availableFloatRegs;
            }

            case VTR_MASK when Configuration.FEATURE_MASKED_HW_INTRINSICS:
            {
                return _availableMaskRegs;
            }

            default:
            {
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    FatalJitException.ThrowFatalJitException(
                        FatalJitErrorCode.MissingRegisterBank,
                        registerType);
                }
                else
                {
                    FatalJitException.ThrowFatalJitException(
                        FatalJitErrorCode.MissingRegisterBank);
                }

                return default!;
            }
        }
    }

    private Interval newInterval(RegisterType registerType)
    {
        var interval = new Interval(registerType, allRegs(registerType));
        intervals.Add(interval);

        if (Configuration.DEBUG)
        {
            interval.intervalIndex = (uint)(intervals.Count - 1);
            if (VERBOSE)
            {
                dumpInterval(interval);
            }
        }

        return interval;
    }
}
