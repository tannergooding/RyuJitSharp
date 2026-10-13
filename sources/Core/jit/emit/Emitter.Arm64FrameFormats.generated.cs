// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    internal static class Arm64FrameFormats
    {
        internal const insFormat IF_NONE = insFormat.IF_NONE;
        internal const insFormat IF_LARGEJMP = insFormat.IF_ARM64_LARGEJMP;
        internal const insFormat IF_LARGEADR = insFormat.IF_ARM64_LARGEADR;
        internal const insFormat IF_LARGELDC = insFormat.IF_ARM64_LARGELDC;
        internal const insFormat IF_DI_1B = insFormat.IF_ARM64_DI_1B;
        internal const insFormat IF_DI_1D = insFormat.IF_ARM64_DI_1D;
        internal const insFormat IF_DR_2C = insFormat.IF_ARM64_DR_2C;
        internal const insFormat IF_DR_2E = insFormat.IF_ARM64_DR_2E;
        internal const insFormat IF_DR_2G = insFormat.IF_ARM64_DR_2G;
        internal const insFormat IF_DR_4A = insFormat.IF_ARM64_DR_4A;
        internal const insFormat IF_SN_0A = insFormat.IF_ARM64_SN_0A;
        internal const insFormat IF_SVE_BB_2A = insFormat.IF_ARM64_SVE_BB_2A;
        internal const insFormat IF_SVE_BC_1A = insFormat.IF_ARM64_SVE_BC_1A;
        internal const insFormat IF_SVE_EB_1A = insFormat.IF_ARM64_SVE_EB_1A;
        internal const insFormat IF_SVE_JH_2A = insFormat.IF_ARM64_SVE_JH_2A;
        internal const uint Count = (uint)insFormat.IF_ARM64_COUNT;
    }
}