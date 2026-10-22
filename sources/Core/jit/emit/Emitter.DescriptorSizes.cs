// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private const int SMALL_IDSC_SIZE = DescriptorSizes.Small;
    private static int INSTR_DESC_SIZE => DescriptorSizes.Full;

    // emit.h:639-650, 1027, 2270-2332 and emit.cpp:1780. These are native
    // payload sizes, not managed object sizes. AMD64's 16-byte base includes an
    // eight-byte address union; x86 uses a 12-byte base and four-byte pointers.
    // Jump adds three pointers and a four-byte bitfield. Align adds three pointers
    // and a bool in Debug or ARM64 late-disassembly builds (40/48 bytes on AMD64).
    // Debug info and its separately owned pointer prefix have target-dependent native sizes.
    internal static class DescriptorSizes
    {
        internal const int Small = 8;
        private static bool Uses32BitLayout =>
            Configuration.TARGET_X86 || Configuration.TARGET_ARM ||
            (Configuration.TARGET_WASM && (IntPtr.Size == sizeof(int)));

        internal static int Full => Small + (Uses32BitLayout ? 4 : 8);
        internal static int Jump => Uses32BitLayout ? 28 : 48;
        internal static int Label => Jump + (Uses32BitLayout ? 4 : 8);
        internal static int WasmLocalVarDecl => Full + 8;
        internal static int WasmValTypeImm => Full + 8;
        internal static int WasmV128Imm => Full + 16;
        internal static int WasmMemargLane => Full + (IntPtr.Size == sizeof(long) ? 16 : 8);
        internal static int Align
        {
            get
            {
                if (Configuration.DEBUG)
                {
                    if (Configuration.TARGET_X86 || Configuration.TARGET_ARM)
                    {
                        return 28;
                    }

                    return 48;
                }

                if (Configuration.LATE_DISASM && Configuration.TARGET_ARM64)
                {
                    return 48;
                }
                if (Configuration.TARGET_X86 || Configuration.TARGET_ARM)
                {
                    return 24;
                }

                return 40;
            }
        }
        internal static int DebugPrefix => Uses32BitLayout ? 4 : 8;
        internal static int DebugInfo =>
            Configuration.TARGET_WASM ? (Uses32BitLayout ? 48 : 72) : (Uses32BitLayout ? 36 : 56);

        // The four-byte emitLclVarAddr payload rounds each eight-byte-aligned
        // base up by eight bytes (emit.h:2385-2397).
        internal static int LocalVarPair
        {
            get
            {
                if (!Configuration.TARGET_ARM64)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM64LocalPairDescriptorsAreUnavailableOnThisTarget
    );
                    return default!;
                }

                return Full + 8;
            }
        }

        internal static int LocalVarPairConstant
        {
            get
            {
                if (!Configuration.TARGET_ARM64)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM64LocalPairDescriptorsAreUnavailableOnThisTarget
    );
                    return default!;
                }

                return ConstantDescriptorSizes.Constant + 8;
            }
        }

        // Native instrDescLoadImm appends eight 32-bit instruction values and eight int32 immediates.
        internal static int RiscVLoadImmediate
        {
            get
            {
                if (!Configuration.TARGET_RISCV64)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCVLoadImmediateDescriptorsAreUnavailableOnThisTarget
    );
                    return default!;
                }

                return ConstantDescriptorSizes.Constant +
                    (TargetCostEstimation.RiscVAbsoluteMaxImmediateInstructionCount * sizeof(int) * 2);
            }
        }
    }

    protected sealed class instrDescBasic : instrDesc
    {
        public override int NativeLogicalSize
        {
            get
            {
                if (Configuration.TARGET_WASM || Configuration.TARGET_ARM)
                {
                    if (idIns() == INS_invalid)
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ThisInstructionRequiresAnInitializedDescriptorWithItsNativeLayout);
                    }
                }
                else if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64)
                {
                    if (idIns() is INS_invalid or INS_align || (Configuration.TARGET_AMD64 && idIns() == INS_jmp))
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ThisInstructionRequiresAnInitializedDescriptorWithItsNativeLayout);
                    }
                }
                else
                {
                    JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.BaseInstructionDescriptorSizeIsNotYetPortedForThisTarget);
                }

                if (idIsLargeCns() || idIsLargeDsp() || idIsLargeCall())
                {
                    JitExceptionHelpers.ThrowNotSupportedException(FatalJitErrorCode.ExtendedInstructionDescriptorLayoutIsNotYetPorted);
                }

                return idIsSmallDsc() ? SMALL_IDSC_SIZE : INSTR_DESC_SIZE;
            }
        }
    }

    private int emitSizeOfInsDsc(instrDesc descriptor)
    {
        if (Configuration.TARGET_XARCH)
        {
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
        }

        if (Configuration.TARGET_ARM64)
        {
            if (descriptor.idIsSmallDsc())
            {
                return SMALL_IDSC_SIZE;
            }

            assert((uint)descriptor.idInsFmt() < (uint)emitFmtToOps.Length);
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
            if (Configuration.FEATURE_LOOP_ALIGN && (descriptor.idIns() == INS_align))
            {
                return DescriptorSizes.Align;
            }

            return INSTR_DESC_SIZE;
        }

        if (Configuration.TARGET_ARM)
        {
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

            if (descriptor.idInsFmt() == insFormat.IF_ARM32_T2_N3)
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
        }

        if (Configuration.TARGET_RISCV64)
        {
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

                case INS_OPTS_RISCV64_C:
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
                case INS_OPTS_RISCV64_RELOC:
                case INS_OPTS_NONE:
                {
                    return INSTR_DESC_SIZE;
                }

                case INS_OPTS_RISCV64_I:
                {
                    return DescriptorSizes.RiscVLoadImmediate;
                }

                default:
                {
                    NO_WAY("unexpected instruction descriptor format");
                    return INSTR_DESC_SIZE;
                }
            }
        }

        if (Configuration.TARGET_WASM)
        {
            return descriptor.NativeLogicalSize;
        }

        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.InstructionDescriptorSizesAreNotYetPortedForThisTarget);
        return default;
    }

    private static int Arm64CallDescriptorSize()
    {
        if (!Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM64CallDescriptorLayoutIsOnlySupportedOnARM64
    );
            return default!;
        }

        if (Configuration.TARGET_WINDOWS)
        {
            // MSVC gives the GCtype and following bool bitfields separate storage
            // units at offsets 68 and 72, rounding the complete descriptor to 80.
            return 80;
        }
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM64CallDescriptorLayoutIsNotEstablishedForThisABI
    );
        return default!;
    }
}
