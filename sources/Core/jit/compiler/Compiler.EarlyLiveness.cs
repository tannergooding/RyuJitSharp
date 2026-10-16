// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private static ConfigMethodRange s_jitEnableEarlyLivenessRange;

    public unsafe PhaseStatus fgEarlyLiveness()
    {
        if (!opts.OptimizationEnabled)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (Configuration.DEBUG)
        {
            s_jitEnableEarlyLivenessRange.EnsureInit(JitConfig.JitEnableEarlyLivenessRange);
            if (!s_jitEnableEarlyLivenessRange.Contains(info.compMethodHash()))
            {
                return PhaseStatus.MODIFIED_NOTHING;
            }
        }

        new Liveness<EarlyLiveness>(this).RunEarly();
        fgDidEarlyLiveness = true;
        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    private readonly struct EarlyLiveness : ILivenessPolicy
    {
        public static bool IsEarly => true;

        public static bool EliminateDeadCode => true;
    }
}
