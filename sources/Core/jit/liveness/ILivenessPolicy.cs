// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public interface ILivenessPolicy
{
    static virtual bool IsEarly => false;

    static virtual bool IsLIR => false;

    static virtual bool SsaLiveness => false;

    static virtual bool ComputeMemoryLiveness => false;

    static virtual bool EliminateDeadCode => false;

    static virtual bool TrackAddressExposedLocals => false;
}
