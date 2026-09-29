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
        internal const int Label = Jump + 8;
#if DEBUG
        internal const int Align = 48;
#else
        internal const int Align = 40;
#endif
        internal const int DebugPrefix = 8;
        internal const int DebugInfo = 56;
#if TARGET_ARM64
        // The four-byte emitLclVarAddr payload rounds each eight-byte-aligned
        // base up by eight bytes (emit.h:2385-2397).
        internal const int LocalVarPair = Full + 8;
        internal const int LocalVarPairConstant = ConstantDescriptorSizes.Constant + 8;
#endif
    }

    protected sealed class instrDescBasic : instrDesc
    {
        public override int NativeLogicalSize
        {
            get
            {
#if TARGET_AMD64 || TARGET_ARM64
                if (idIns() is INS_invalid or INS_align
#if TARGET_AMD64
                    or INS_jmp
#endif
                    )
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
#elif TARGET_ARM64
        if (descriptor.idIsSmallDsc())
        {
            return SMALL_IDSC_SIZE;
        }

        assert(descriptor.idInsFmt() < insFormat.IF_COUNT);
        var operands = (ID_OPS)emitFmtToOps[(int)descriptor.idInsFmt()];
        var isCall = descriptor.idIns() is INS_bl or INS_blr or INS_b_tail or INS_br_tail;
        var mayBeCall = descriptor.idIns() is INS_b or INS_br;

        switch (operands)
        {
            case ID_OP_NONE:
            {
                break;
            }

            case ID_OP_JMP:
            {
                return DescriptorSizes.Jump;
            }

            case ID_OP_CALL:
            {
                assert(isCall || mayBeCall);
                if (descriptor.idIsLargeCall())
                {
                    return Arm64CallDescriptorSize();
                }

                assert(!descriptor.idIsLargeDsp());
                assert(!descriptor.idIsLargeCns());
                return INSTR_DESC_SIZE;
            }

            default:
            {
                NO_WAY("unexpected instruction descriptor format");
                break;
            }
        }

        if (descriptor.idIsLargeCns())
        {
            if (descriptor.idIsLclVarPair())
            {
                return DescriptorSizes.LocalVarPairConstant;
            }
            if (descriptor.idIsLargeDsp())
            {
                return ConstantDescriptorSizes.ConstantDisplacement;
            }

            return ConstantDescriptorSizes.Constant;
        }
        if (descriptor.idIsLclVarPair())
        {
            return DescriptorSizes.LocalVarPair;
        }
        if (descriptor.idIsLargeDsp())
        {
            return ConstantDescriptorSizes.Displacement;
        }
#if FEATURE_LOOP_ALIGN
        if (descriptor.idIns() == INS_align)
        {
            return DescriptorSizes.Align;
        }
#endif

        return INSTR_DESC_SIZE;
#else
        throw new PlatformNotSupportedException("Instruction descriptor sizes are not yet ported for this target.");
#endif
    }

#if TARGET_ARM64
    private static int Arm64CallDescriptorSize()
    {
#if TARGET_WINDOWS
        // MSVC gives the GCtype and following bool bitfields separate storage
        // units at offsets 68 and 72, rounding the complete descriptor to 80.
        return 80;
#else
        throw new PlatformNotSupportedException("The ARM64 call descriptor layout is not established for this ABI.");
#endif
    }
#endif
}
