// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescLbl : instrDescJmp
    {
        // The managed target is separate, so the destination remains in iiaLclVar.
        // Retain native instrDescLbl's aligned footprint for descriptor traversal.
        public override int NativeLogicalSize
        {
            get
            {
                if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARM)
                {
                    throw new FatalJitException(CORJIT_SKIPPED,
                        "Label instruction descriptors are not supported for this target.");
                }

                return DescriptorSizes.Label;
            }
        }
    }
}
