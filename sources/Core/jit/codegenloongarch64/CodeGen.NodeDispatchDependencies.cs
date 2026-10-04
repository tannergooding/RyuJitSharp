// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genSetRegToConst(regNumber targetReg, var_types targetType, GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 constant materialization is not ported.");
    }

    private void genCodeForShift(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 shift generation is not ported.");
    }

    private void genCodeForLclAddr(GenTreeLclFld tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 local-address generation is not ported.");
    }

    private void genCodeForLclFld(GenTreeLclFld tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 local-field load generation is not ported.");
    }

    private void genLeaInstruction(GenTreeAddrMode tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 address-mode generation is not ported.");
    }

    private void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 index-address generation is not ported.");
    }

    private void genIntToIntCast(GenTreeCast tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 integer cast generation is not ported.");
    }

    private void genIntrinsic(GenTreeIntrinsic tree)
    {
        var op1 = tree.Op1;
        var op2 = tree.Op2;

        switch (tree.IntrinsicName)
        {
            case NI_PRIMITIVE_SaturateToInt8:
            case NI_PRIMITIVE_SaturateToInt16:
            case NI_PRIMITIVE_SaturateToUInt8:
            case NI_PRIMITIVE_SaturateToUInt16:
            {
                nint minVal;
                nint maxVal;
                switch (tree.IntrinsicName)
                {
                    case NI_PRIMITIVE_SaturateToInt8:
                    {
                        minVal = sbyte.MinValue;
                        maxVal = sbyte.MaxValue;
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToInt16:
                    {
                        minVal = short.MinValue;
                        maxVal = short.MaxValue;
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToUInt8:
                    {
                        minVal = 0;
                        maxVal = byte.MaxValue;
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToUInt16:
                    {
                        minVal = 0;
                        maxVal = ushort.MaxValue;
                        break;
                    }

                    default:
                    {
                        unreached();
                        return;
                    }
                }

                genConsumeOperands(tree.AsOp());
                var dst = tree.RegNum;
                var src = op1.RegNum;
                var tmpReg = _internalRegisters.GetSingle(tree);
                var emit = GetEmitter();

                // slli.w sign-extends bits[31:0], so subsequent 64-bit comparisons use a normalized value.
                emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, dst, src, 0);

                var skipLo = genCreateTempLabel();
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, minVal);
                emit.emitIns_J_cond_la(INS_bge, skipLo, dst, tmpReg);
                emit.emitIns_R_R(INS_mov, EA_PTRSIZE, dst, tmpReg);
                genDefineTempLabel(skipLo);

                var skipHi = genCreateTempLabel();
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, maxVal);
                emit.emitIns_J_cond_la(INS_bge, skipHi, tmpReg, dst);
                emit.emitIns_R_R(INS_mov, EA_PTRSIZE, dst, tmpReg);
                genDefineTempLabel(skipHi);

                genProduceReg(tree);
                return;
            }

            default:
            {
                break;
            }
        }

        var attr = tree.Type.EmitActualSize;
        instruction instr;

        // All remaining intrinsics are binary floating-point operations.
        assert(op2 is not null);
        switch (tree.IntrinsicName)
        {
            case NI_System_Math_MaxNative:
            {
                instr = attr == EA_4BYTE ? INS_fmax_s : INS_fmax_d;
                break;
            }

            case NI_System_Math_MinNative:
            {
                instr = attr == EA_4BYTE ? INS_fmin_s : INS_fmin_d;
                break;
            }

            default:
            {
                NO_WAY("Unknown intrinsic");
                throw new FatalJitException(CORJIT_INTERNALERROR, "Unknown intrinsic reached unreachable code.");
            }
        }

        genConsumeOperands(tree.AsOp());
        GetEmitter().emitIns_R_R_R(instr, attr, tree.RegNum, op1.RegNum, op2.RegNum);
        genProduceReg(tree);
    }

#if FEATURE_HW_INTRINSICS
    private void genHWIntrinsic(GenTreeHWIntrinsic tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 hardware-intrinsic generation is not ported.");
    }
#endif

    private void genCodeForNullCheck(GenTreeIndir tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 null-check generation is not ported.");
    }

    private void genRangeCheck(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 range-check generation is not ported.");
    }
}
#endif
