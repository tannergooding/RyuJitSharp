// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgSsaLiveness()
    {
        new Liveness<SsaLivenessPolicy>(this).RunSsa();
    }

    private readonly struct SsaLivenessPolicy : ILivenessPolicy
    {
        public static bool SsaLiveness => true;

        public static bool ComputeMemoryLiveness => true;

        public static bool EliminateDeadCode => true;
    }
}
