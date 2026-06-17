// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class InlineObservationExtensions
{
    extension(InlineObservation observation)
    {
        /// <summary>get the impact of an inline observation</summary>
        public InlineImpact Impact
        {
            get
            {
                assert(observation.IsValid);
                return s_impacts[(int)(observation)];
            }
        }

        /// <summary>get a string describing the impact of an inline observation</summary>
        public string ImpactString => observation.Impact switch {
            InlineImpact.FATAL => "correctness -- fatal",
            InlineImpact.FUNDAMENTAL => "correctness -- fundamental limitation",
            InlineImpact.LIMITATION => "correctness -- jit limitation",
            InlineImpact.PERFORMANCE => "performance",
            InlineImpact.INFORMATION => "information",
            _ => "unexpected impact",
        };

        public bool IsValid => observation is > InlineObservation.CALLEE_UNUSED_INITIAL and < InlineObservation.CALLEE_UNUSED_FINAL;

        /// <summary>get a string describing this inline observation</summary>
        public string String
        {
            get
            {
                assert(observation.IsValid);
                return s_descriptions[(int)(observation)];
            }
        }

        /// <summary>get the target of an inline observation</summary>
        public InlineTarget Target
        {
            get
            {
                assert(observation.IsValid);
                return s_targets[(int)(observation)];
            }
        }

        /// <summary>get a string describing the target of an inline observation</summary>
        public string TargetString => observation.Target switch {
            InlineTarget.CALLEE => "callee",
            InlineTarget.CALLER => "caller",
            InlineTarget.CALLSITE => "call site",
            _ => "unexpected target",
        };
    }
}
