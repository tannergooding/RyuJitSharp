// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genIntrinsic(GenTreeIntrinsic treeNode)
    {
        var op1 = treeNode.Op1;
        var op2 = treeNode.Op2;

        // Handle integer-domain saturation intrinsics separately; they use branches
        // and a temporary register rather than the single-instruction pattern below.
        switch (treeNode.IntrinsicName)
        {
            case NI_PRIMITIVE_SaturateToInt8:
            case NI_PRIMITIVE_SaturateToInt16:
            case NI_PRIMITIVE_SaturateToUInt8:
            case NI_PRIMITIVE_SaturateToUInt16:
            {
                nint minVal;
                nint maxVal;
                switch (treeNode.IntrinsicName)
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

                genConsumeOperands(treeNode.AsOp());
                var dst = treeNode.RegNum;
                var src = op1.RegNum;
                var tmpReg = InternalRegisters.GetSingle(treeNode);

                // Copy src to dst, normalizing to a sign-extended 32-bit value so the
                // subsequent full-register bge compares against the (signed) clamp bounds
                // are well-defined. `sext.w rd, rs` sign-extends bits[31:0] into rd[63:0].
                Emitter.emitIns_R_R(INS_sext_w, EA_4BYTE, dst, src);

                // Clamp lower bound: if dst < minVal, dst = minVal.
                var skipLo = genCreateTempLabel();
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, minVal);
                Emitter.emitIns_J_cond_la(INS_bge, skipLo, dst, tmpReg); // skip if dst >= minVal
                Emitter.emitIns_R_R(INS_mov, EA_PTRSIZE, dst, tmpReg);   // dst = minVal
                genDefineTempLabel(skipLo);

                // Clamp upper bound: if dst > maxVal, dst = maxVal.
                var skipHi = genCreateTempLabel();
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, maxVal);
                Emitter.emitIns_J_cond_la(INS_bge, skipHi, tmpReg, dst); // skip if maxVal >= dst
                Emitter.emitIns_R_R(INS_mov, EA_PTRSIZE, dst, tmpReg);   // dst = maxVal
                genDefineTempLabel(skipHi);

                genProduceReg(treeNode);
                return;
            }

            default:
            {
                break;
            }
        }

        var size = op1.Type.EmitActualSize;
        var is4 = size == EA_4BYTE;

        var instr = INS_invalid;
        switch (treeNode.IntrinsicName)
        {
            case NI_System_Math_Abs:
            {
                instr = is4 ? INS_fsgnjx_s : INS_fsgnjx_d;
                op2 = op1; // "fabs rd, rs" is a pseudo-instruction for "fsgnjx rd, rs, rs"
                break;
            }

            case NI_System_Math_Sqrt:
            {
                instr = is4 ? INS_fsqrt_s : INS_fsqrt_d;
                break;
            }

            case NI_System_Math_MinNative:
            {
                instr = is4 ? INS_fmin_s : INS_fmin_d;
                break;
            }

            case NI_System_Math_MaxNative:
            {
                instr = is4 ? INS_fmax_s : INS_fmax_d;
                break;
            }

            case NI_System_Math_Min:
            {
                instr = INS_min;
                break;
            }

            case NI_System_Math_MinUnsigned:
            {
                instr = INS_minu;
                break;
            }

            case NI_System_Math_Max:
            {
                instr = INS_max;
                break;
            }

            case NI_System_Math_MaxUnsigned:
            {
                instr = INS_maxu;
                break;
            }

            case NI_PRIMITIVE_LeadingZeroCount:
            {
                instr = is4 ? INS_clzw : INS_clz;
                break;
            }

            case NI_PRIMITIVE_TrailingZeroCount:
            {
                instr = is4 ? INS_ctzw : INS_ctz;
                break;
            }

            case NI_PRIMITIVE_PopCount:
            {
                instr = is4 ? INS_cpopw : INS_cpop;
                break;
            }

            default:
            {
                NO_WAY("Unknown intrinsic");
                break;
            }
        }

        genConsumeOperands(treeNode.AsOp());
        var dest = treeNode.RegNum;
        var src1 = op1.RegNum;
        if (op2 is null)
        {
            Emitter.emitIns_R_R(instr, size, dest, src1);
        }
        else
        {
            Emitter.emitIns_R_R_R(instr, size, dest, src1, op2.RegNum);
        }

        genProduceReg(treeNode);
    }
}
#endif
