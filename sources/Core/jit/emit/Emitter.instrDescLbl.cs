// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    protected sealed class instrDescLbl : instrDescJmp
    {
        // The managed target is separate, so the destination remains in iiaLclVar.
        // Retain native instrDescLbl's aligned footprint for descriptor traversal.
        public override int NativeLogicalSize => DescriptorSizes.Label;
    }
#endif
}
