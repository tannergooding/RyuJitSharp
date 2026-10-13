// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescReloc : instrDesc
    {
        public unsafe byte* idrRelocVal;

        public override int NativeLogicalSize
        {
            get
            {
                if (!Configuration.TARGET_ARM)
                {
                    throw new FatalJitException(CORJIT_SKIPPED,
                        "ARM relocation descriptor size is not supported for this target.");
                }

                // ARM32 adds one 32-bit word to a small descriptor, then another for the relocation pointer.
                return DescriptorSizes.Small + (2 * sizeof(uint));
            }
        }
    }
}
