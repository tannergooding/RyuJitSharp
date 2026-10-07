// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescLoadImm : instrDescCns
    {
        public const int absMaxInsCount = 8;

        public instruction[] ins = new instruction[absMaxInsCount];
        public int[] values = new int[absMaxInsCount];

        public override int NativeLogicalSize => DescriptorSizes.RiscVLoadImmediate;
    }
}
#endif
