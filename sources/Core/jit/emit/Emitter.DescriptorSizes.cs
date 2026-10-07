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
    // payload sizes, not managed object sizes. AMD64's 16-byte base includes an
    // eight-byte address union; x86 uses a 12-byte base and four-byte pointers.
    // Jump adds three pointers and a four-byte bitfield. Align adds three pointers
    // and a Debug bool (40/48 bytes on AMD64). Debug info and its separately
    // owned pointer prefix have target-dependent native sizes.
    internal static class DescriptorSizes
    {
        internal const int Small = 8;
#if TARGET_X86 || TARGET_ARM || (TARGET_WASM && !HOST_64BIT)
        internal const int Full = Small + 4;
        internal const int Jump = 28;
        internal const int Label = Jump + 4;
#else
        internal const int Full = Small + 8;
        internal const int Jump = 48;
        internal const int Label = Jump + 8;
#endif
        internal const int WasmLocalVarDecl = Full + 8;
        internal const int WasmValTypeImm = Full + 8;
        internal const int WasmV128Imm = Full + 16;
#if HOST_64BIT
        internal const int WasmMemargLane = Full + 16;
#else
        internal const int WasmMemargLane = Full + 8;
#endif
#if DEBUG
#if TARGET_X86 || TARGET_ARM
        internal const int Align = 28;
#else
        internal const int Align = 48;
#endif
#else
#if TARGET_X86 || TARGET_ARM
        internal const int Align = 24;
#else
        internal const int Align = 40;
#endif
#endif
#if TARGET_X86 || TARGET_ARM || (TARGET_WASM && !HOST_64BIT)
        internal const int DebugPrefix = 4;
        internal const int DebugInfo = 36;
#else
        internal const int DebugPrefix = 8;
        internal const int DebugInfo = 56;
#endif
#if TARGET_ARM64
        // The four-byte emitLclVarAddr payload rounds each eight-byte-aligned
        // base up by eight bytes (emit.h:2385-2397).
        internal const int LocalVarPair = Full + 8;
        internal const int LocalVarPairConstant = ConstantDescriptorSizes.Constant + 8;
#endif
#if TARGET_RISCV64
        // Native instrDescLoadImm appends eight 32-bit instruction values and eight int32 immediates.
        internal const int RiscVLoadImmediate =
            ConstantDescriptorSizes.Constant + (instrDescLoadImm.absMaxInsCount * sizeof(int) * 2);
#endif
    }

    protected sealed class instrDescBasic : instrDesc
    {
#if TARGET_XARCH || TARGET_ARM || TARGET_ARM64 || TARGET_WASM
        public override int NativeLogicalSize
        {
            get
            {
#if TARGET_WASM
                if (idIns() == INS_invalid)
                {
                    throw new InvalidOperationException("This instruction requires an initialized descriptor with its native layout.");
                }
#elif TARGET_ARM
                if (idIns() == INS_invalid)
                {
                    throw new InvalidOperationException("This instruction requires an initialized descriptor with its native layout.");
                }
#else
                if (idIns() is INS_invalid or INS_align
#if TARGET_AMD64
                    or INS_jmp
#endif
                    )
                {
                    throw new InvalidOperationException("This instruction requires an initialized descriptor with its native layout.");
                }
#endif

                if (idIsLargeCns() || idIsLargeDsp() || idIsLargeCall())
                {
                    throw new NotSupportedException("Extended instruction descriptor layout is not yet ported.");
                }

                return idIsSmallDsc() ? SMALL_IDSC_SIZE : INSTR_DESC_SIZE;
            }
        }
#else
        public override int NativeLogicalSize =>
            throw new PlatformNotSupportedException("Base instruction descriptor size is not yet ported for this target.");
#endif
    }

    private int emitSizeOfInsDsc(instrDesc descriptor)
    {
#if TARGET_XARCH
        assert((uint)descriptor.idInsFmt() < (uint)emitFmtToOps.Length);
        var operands = (ID_OPS)emitFmtToOps[(int)descriptor.idInsFmt()];

        if (descriptor.idIns() == INS_call)
        {
            assert(operands is ID_OP_CALL or ID_OP_SPEC or ID_OP_JMP);
        }

        switch (operands)
        {
            case ID_OP_NONE:
            {
                return emitSizeOfInsDsc_NONE(descriptor);
            }

            case ID_OP_LBL:
            {
                return DescriptorSizes.Label;
            }

            case ID_OP_JMP:
            {
                return DescriptorSizes.Jump;
            }

            case ID_OP_CALL:
            case ID_OP_SPEC:
            {
                return emitSizeOfInsDsc_SPEC(descriptor);
            }

            case ID_OP_SCNS:
            case ID_OP_CNS:
            {
                return emitSizeOfInsDsc_CNS(descriptor);
            }

            case ID_OP_DSP:
            case ID_OP_DSP_CNS:
            {
                return emitSizeOfInsDsc_DSP(descriptor);
            }

            case ID_OP_AMD:
            case ID_OP_AMD_CNS:
            {
                return emitSizeOfInsDsc_AMD(descriptor);
            }

            default:
            {
                NO_WAY("unexpected instruction descriptor format");
                return INSTR_DESC_SIZE;
            }
        }
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
#elif TARGET_ARM
        if (descriptor.idIsSmallDsc())
        {
            return SMALL_IDSC_SIZE;
        }

        assert((uint)descriptor.idInsFmt() < (uint)emitFmtToOps.Length);
        var operands = (ID_OPS)emitFmtToOps[(int)descriptor.idInsFmt()];
        var isCall = descriptor.idIns() is INS_bl or INS_blx;
        var mayBeCall = descriptor.idIns() is INS_b or INS_bx;

        assert(!isCall || operands is ID_OPS.ID_OP_CALL or ID_OPS.ID_OP_SPEC or ID_OPS.ID_OP_JMP);

        switch (operands)
        {
            case ID_OPS.ID_OP_NONE:
            {
                break;
            }

            case ID_OPS.ID_OP_JMP:
            {
                return DescriptorSizes.Jump;
            }

            case ID_OPS.ID_OP_LBL:
            {
                return DescriptorSizes.Label;
            }

            case ID_OPS.ID_OP_CALL:
            case ID_OPS.ID_OP_SPEC:
            {
                assert(isCall || mayBeCall);
                if (descriptor.idIsLargeCall())
                {
                    return instrDescCGCA.NativeSize;
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

        if (descriptor.idInsFmt() == insFormat.IF_T2_N3)
        {
            assert(descriptor.idIns() is INS_movw or INS_movt);
            return INSTR_DESC_SIZE + sizeof(uint);
        }

        if (descriptor.idIsLargeCns())
        {
            return descriptor.idIsLargeDsp()
                ? ConstantDescriptorSizes.ConstantDisplacement
                : ConstantDescriptorSizes.Constant;
        }

        if (descriptor.idIsLargeDsp())
        {
            return ConstantDescriptorSizes.Displacement;
        }

        return INSTR_DESC_SIZE;
#elif TARGET_RISCV64
        if (descriptor.idIsSmallDsc())
        {
            return SMALL_IDSC_SIZE;
        }

        switch (descriptor.idInsOpt())
        {
            case INS_OPTS_JUMP:
            {
                return DescriptorSizes.Jump;
            }

            case INS_OPTS_C:
            {
                if (descriptor.idIsLargeCall())
                {
                    return instrDescCGCA.NativeSize;
                }

                assert(!descriptor.idIsLargeDsp());
                assert(!descriptor.idIsLargeCns());
                return INSTR_DESC_SIZE;
            }

            case INS_OPTS_RC:
            case INS_OPTS_RL:
            case INS_OPTS_RELOC:
            case INS_OPTS_NONE:
            {
                return INSTR_DESC_SIZE;
            }

            case INS_OPTS_I:
            {
                return DescriptorSizes.RiscVLoadImmediate;
            }

            default:
            {
                NO_WAY("unexpected instruction descriptor format");
                return INSTR_DESC_SIZE;
            }
        }
#elif TARGET_WASM
        return descriptor.NativeLogicalSize;
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
