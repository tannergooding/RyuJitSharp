// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorJitInfo
{
    public enum PgoSource
    {
        // PGO data source unknown
        Unknown = 0,

        // PGO data comes from embedded R2R profile data
        Static = 1,

        // PGO data comes from current run
        Dynamic = 2,

        // PGO data comes from blend of prior runs and current run
        Blend = 3,

        // PGO data comes from text file
        Text = 4,

        // PGO data from classic IBC
        IBC = 5,

        // PGO data derived from sampling
        Sampling = 6,

        // PGO data derived from synthesis
        Synthesis = 7,
    }
}
