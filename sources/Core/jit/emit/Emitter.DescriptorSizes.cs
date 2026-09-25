// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    private const int SMALL_IDSC_SIZE = DescriptorSizes.Small;
    private const int INSTR_DESC_SIZE = DescriptorSizes.Full;

    // emit.h:639-650, 1027, 2270-2332 and emit.cpp:1780. These are native
    // payload sizes, not managed object sizes. The 16-byte base includes an
    // eight-byte address union; jump adds three pointers and a four-byte
    // bitfield (48 after alignment). Align adds three pointers and a Debug
    // bool (40 Release, 48 Debug). Debug info is 56 bytes, but each descriptor
    // reserves only an eight-byte pointer prefix for the separately owned info.
    internal static class DescriptorSizes
    {
        internal const int Small = 8;
        internal const int Full = Small + 8;
        internal const int Jump = 48;
#if DEBUG
        internal const int Align = 48;
#else
        internal const int Align = 40;
#endif
        internal const int DebugPrefix = 8;
        internal const int DebugInfo = 56;
    }

    protected sealed class instrDescBasic : instrDesc
    {
        public override int NativeLogicalSize
        {
            get
            {
#if TARGET_AMD64
                if (idIns() is INS_invalid or INS_jmp or INS_align)
                {
                    throw new InvalidOperationException("This instruction requires an initialized descriptor with its native layout.");
                }

                if (idIsLargeCns() || idIsLargeDsp() || idIsLargeCall())
                {
                    throw new NotSupportedException("Extended instruction descriptor layout is not yet ported.");
                }

                return idIsSmallDsc() ? SMALL_IDSC_SIZE : INSTR_DESC_SIZE;
#else
                throw new PlatformNotSupportedException("Base instruction descriptor size is not yet ported for this target.");
#endif
            }
        }
    }

    private int emitSizeOfInsDsc(instrDesc descriptor)
    {
#if TARGET_AMD64
        return descriptor.NativeLogicalSize;
#else
        throw new PlatformNotSupportedException("Instruction descriptor sizes are not yet ported for this target.");
#endif
    }
}
