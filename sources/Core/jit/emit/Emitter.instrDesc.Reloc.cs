// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescReloc : instrDesc
    {
        public unsafe byte* idrRelocVal;

        public override int NativeLogicalSize => INSTR_DESC_SIZE + sizeof(uint);
    }
}
#endif
