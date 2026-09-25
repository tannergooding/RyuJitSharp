// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genHWIntrinsic_R_RM(GenTreeHWIntrinsic node, instruction ins, emitAttr attr,
        regNumber reg, GenTree rmOp, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        if (IsEmbeddedBroadcastEnabled(ins, rmOp))
        {
            options = AddEmbBroadcastMode(options);
        }
        else if ((options == INS_OPTS_NONE) && !Emitter.IsVexEncodableInstruction(ins))
        {
            // Opportunistic EVEX forms can use shorter VEX encodings when no
            // embedded feature requires the wider encoding.
            ins = ins switch
            {
                INS_vbroadcastf64x2 => INS_vbroadcastf32x4,
                INS_vbroadcasti64x2 => INS_vbroadcasti32x4,
                INS_vmovdqa64 => INS_movdqa32,
                INS_vmovdqu64 => INS_movdqu32,
                _ => ins,
            };
        }

        var descriptor = genOperandDesc(ins, rmOp);
        genHWIntrinsic_R_RM(node, ins, attr, reg, descriptor, rmOp, options);
    }

    private unsafe void genHWIntrinsic_R_RM(GenTreeHWIntrinsic node, instruction ins, emitAttr attr,
        regNumber reg, OperandDesc descriptor, GenTree rmOp, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        if (((options & INS_OPTS_EVEX_b_MASK) != 0) && (descriptor.GetKind() == OperandKind.Reg))
        {
            assert(rmOp.RegNum != REG_NA);
            Emitter.emitIns_R_R(ins, attr, reg, rmOp.RegNum, options);
            return;
        }
        if (descriptor.IsContained())
        {
            assert(HWIntrinsicInfo.SupportsContainment(node.HWIntrinsicId));
            assertIsContainableHWIntrinsicOp(node, rmOp);
        }

        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_R_C(ins, attr, reg, descriptor.GetFieldHnd(), 0, options);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_R_S(ins, attr, reg, descriptor.GetVarNum(), descriptor.GetLclOffset(), options);
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_R_A(ins, attr, reg, descriptor.GetIndirForm(), options);
                break;
            }

            case OperandKind.Reg:
            {
                var rmReg = descriptor.GetReg();
                if (RyuJitSharp.Emitter.IsMovInstruction(ins))
                {
                    assert(options == INS_OPTS_NONE);
                    Emitter.emitIns_Mov(ins, attr, reg, rmReg, canSkip: false);
                }
                else
                {
                    if (varTypeIsIntegral(rmOp.Type))
                    {
                        var broadcastFixup = false;
                        var instructionFixup = false;
                        switch (node.HWIntrinsicId)
                        {
                            case NI_AVX2_BroadcastScalarToVector128:
                            case NI_AVX2_BroadcastScalarToVector256:
                            {
                                if (_compiler.canUseEvexEncoding())
                                {
                                    instructionFixup = true;
                                }
                                else
                                {
                                    broadcastFixup = true;
                                }
                                break;
                            }

                            case NI_AVX512_BroadcastScalarToVector512:
                            {
                                instructionFixup = true;
                                break;
                            }
                        }

                        if (broadcastFixup)
                        {
                            // Lowering removed CreateScalarUnsafe around this operand.
                            // A register allocation instead of memory needs that move restored.
                            var moveSize = node.SimdBaseType.EmitActualSize;
#if TARGET_AMD64
                            var move = moveSize == EA_4BYTE ? INS_movd32 : INS_movd64;
#else
                            var move = INS_movd32;
#endif
                            Emitter.emitIns_Mov(move, moveSize, reg, rmReg, canSkip: false);
                            rmReg = reg;
                        }
                        else if (instructionFixup)
                        {
                            ins = ins switch
                            {
                                INS_vpbroadcastb => INS_vpbroadcastb_gpr,
                                INS_vpbroadcastd => INS_vpbroadcastd_gpr,
                                INS_vpbroadcastq => INS_vpbroadcastq_gpr,
                                INS_vpbroadcastw => INS_vpbroadcastw_gpr,
                                _ => throw new FatalJitException("Unexpected scalar broadcast instruction."),
                            };
                        }
                    }
                    Emitter.emitIns_R_R(ins, attr, reg, rmReg, options);
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void genHWIntrinsic_R_RM_I(GenTreeHWIntrinsic node, instruction ins, emitAttr attr,
        sbyte immediate, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        var op1 = node.GetOp(1);
        assert(node.RegNum != REG_NA);
        assert(!node.IsCommutativeHWIntrinsic);
        if (op1.IsContained || op1.IsUsedFromSpillTemp)
        {
            assert(HWIntrinsicInfo.SupportsContainment(node.HWIntrinsicId));
            assertIsContainableHWIntrinsicOp(node, op1);
        }
        inst_RV_TT_IV(ins, attr, node.RegNum, op1, immediate, options);
    }

    public void genHWIntrinsic_R_R_RM(GenTreeHWIntrinsic node, instruction ins, emitAttr attr, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        assert(node.RegNum != REG_NA);
        assert(op1.RegNum != REG_NA);
        if (op2.IsContained || op2.IsUsedFromSpillTemp)
        {
            assert(HWIntrinsicInfo.SupportsContainment(node.HWIntrinsicId));
            assertIsContainableHWIntrinsicOp(node, op2);
        }
        inst_RV_RV_TT(ins, attr, node.RegNum, op1.RegNum, op2, node.IsRmwHWIntrinsic(_compiler), options);
    }

    public void genHWIntrinsic_R_R_RM_I(GenTreeHWIntrinsic node, instruction ins, emitAttr attr,
        sbyte immediate, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op1Reg = op1.RegNum;
        assert(node.RegNum != REG_NA);
        if (ins == INS_insertps)
        {
            if (op1.IsContained)
            {
                assert(op1.IsVectorZero);
                op1Reg = node.RegNum;
            }
            if (op2.IsContained && op2.IsVectorZero)
            {
                // The immediate zeros the selected lane, so no zero source register is needed.
                Emitter.emitIns_SIMD_R_R_R_I(ins, attr, node.RegNum, op1Reg, op1Reg, immediate, options);
                return;
            }
        }
        if (op2.IsContained || op2.IsUsedFromSpillTemp)
        {
            assert(HWIntrinsicInfo.SupportsContainment(node.HWIntrinsicId));
            assertIsContainableHWIntrinsicOp(node, op2);
        }
        assert(op1Reg != REG_NA);
        inst_RV_RV_TT_IV(ins, attr, node.RegNum, op1Reg, op2, immediate, node.IsRmwHWIntrinsic(_compiler), options);
    }

    public unsafe void genHWIntrinsic_R_R_RM_R(GenTreeHWIntrinsic node, instruction ins, emitAttr attr, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);
        var op1Reg = op1.RegNum;
        if (op1.IsContained)
        {
            assert(op1.IsVectorZero);
            options = AddEmbMaskingMode(options, REG_K0, true);
            op1Reg = node.RegNum;
        }
        assert(node.RegNum != REG_NA);
        assert(op1Reg != REG_NA);
        assert(op3.RegNum != REG_NA);
        if (IsEmbeddedBroadcastEnabled(ins, op2))
        {
            options = AddEmbBroadcastMode(options);
        }

        var descriptor = genOperandDesc(ins, op2);
        if (descriptor.IsContained())
        {
            assert(HWIntrinsicInfo.SupportsContainment(node.HWIntrinsicId));
            assertIsContainableHWIntrinsicOp(node, op2);
        }
        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_SIMD_R_R_C_R(ins, attr, node.RegNum, op1Reg, op3.RegNum,
                    descriptor.GetFieldHnd(), 0, options);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_SIMD_R_R_S_R(ins, attr, node.RegNum, op1Reg, op3.RegNum,
                    descriptor.GetVarNum(), descriptor.GetLclOffset(), options);
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_SIMD_R_R_A_R(ins, attr, node.RegNum, op1Reg, op3.RegNum,
                    descriptor.GetIndirForm(), options);
                break;
            }

            case OperandKind.Reg:
            {
                Emitter.emitIns_SIMD_R_R_R_R(ins, attr, node.RegNum, op1Reg, descriptor.GetReg(), op3.RegNum, options);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public unsafe void genHWIntrinsic_R_R_R_RM(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, GenTree op3, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert(targetReg != REG_NA);
        assert(op1Reg != REG_NA);
        assert(op2Reg != REG_NA);
        if (IsEmbeddedBroadcastEnabled(ins, op3))
        {
            options = AddEmbBroadcastMode(options);
        }
        var descriptor = genOperandDesc(ins, op3);
        if (((options & INS_OPTS_EVEX_b_MASK) != 0) && (descriptor.GetKind() == OperandKind.Reg))
        {
            assert(op3.RegNum != REG_NA);
            Emitter.emitIns_SIMD_R_R_R_R(ins, attr, targetReg, op1Reg, op2Reg, descriptor.GetReg(), options);
            return;
        }
        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_SIMD_R_R_R_C(ins, attr, targetReg, op1Reg, op2Reg, descriptor.GetFieldHnd(), 0, options);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_SIMD_R_R_R_S(ins, attr, targetReg, op1Reg, op2Reg,
                    descriptor.GetVarNum(), descriptor.GetLclOffset(), options);
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_SIMD_R_R_R_A(ins, attr, targetReg, op1Reg, op2Reg, descriptor.GetIndirForm(), options);
                break;
            }

            case OperandKind.Reg:
            {
                Emitter.emitIns_SIMD_R_R_R_R(ins, attr, targetReg, op1Reg, op2Reg, descriptor.GetReg(), options);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public unsafe void genHWIntrinsic_R_R_R_RM_I(GenTreeHWIntrinsic node, instruction ins, emitAttr attr,
        sbyte immediate, insOpts options)
    {
        Emitter.RequireSupportedInstructionRecording();
        var targetReg = node.RegNum;
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);
        var op1Reg = op1.RegNum;
        var op2Reg = op2.RegNum;
        if (op1.IsContained)
        {
            // Unused ternary inputs need no allocated register or RMW dependency.
            assert(!node.IsRmwHWIntrinsic(_compiler));
            op1Reg = targetReg;
            if (op2.IsContained)
            {
#if DEBUG
                assert(node.HWIntrinsicId == NI_AVX512_TernaryLogic);
                var info = TernaryLogicInfo.Lookup(unchecked((byte)immediate));
                assert(info.GetAllUseFlags() == TernaryLogicUseFlags.C);
#endif
                op2Reg = targetReg;
            }
            else
            {
#if DEBUG
                if (node.HWIntrinsicId == NI_AVX512_TernaryLogic)
                {
                    var info = TernaryLogicInfo.Lookup(unchecked((byte)immediate));
                    assert(info.GetAllUseFlags() == TernaryLogicUseFlags.BC);
                }
#endif
            }
        }
        else if (node.HWIntrinsicId == NI_AVX512_TernaryLogic)
        {
            const byte A = 0xF0;
            const byte B = 0xCC;
            const byte C = 0xAA;
            var info = TernaryLogicInfo.Lookup(unchecked((byte)immediate));
            if (info.GetAllUseFlags() == TernaryLogicUseFlags.ABC)
            {
                // Permute the truth table with the inputs when the destination aliases a source.
                if (targetReg == op2Reg)
                {
                    (op1, op2) = (op2, op1);
                    (op1Reg, op2Reg) = (op2Reg, op1Reg);
                    immediate = unchecked((sbyte)TernaryLogicInfo.GetTernaryControlByte(info, B, A, C));
                }
                else if ((targetReg == op3.RegNum) && !op3.IsUsedFromSpillTemp)
                {
                    assert(!op3.IsContained);
                    (op1, op3) = (op3, op1);
                    op1Reg = op1.RegNum;
                    immediate = unchecked((sbyte)TernaryLogicInfo.GetTernaryControlByte(info, C, B, A));
                }
            }
        }
        assert(targetReg != REG_NA);
        assert(op1Reg != REG_NA);
        assert(op2Reg != REG_NA);
        if (IsEmbeddedBroadcastEnabled(ins, op3))
        {
            options = AddEmbBroadcastMode(options);
        }
        var descriptor = genOperandDesc(ins, op3);
        switch (descriptor.GetKind())
        {
            case OperandKind.ClsVar:
            {
                Emitter.emitIns_SIMD_R_R_R_C_I(ins, attr, targetReg, op1Reg, op2Reg,
                    descriptor.GetFieldHnd(), 0, immediate, options);
                break;
            }

            case OperandKind.Local:
            {
                Emitter.emitIns_SIMD_R_R_R_S_I(ins, attr, targetReg, op1Reg, op2Reg,
                    descriptor.GetVarNum(), descriptor.GetLclOffset(), immediate, options);
                break;
            }

            case OperandKind.Indir:
            {
                Emitter.emitIns_SIMD_R_R_R_A_I(ins, attr, targetReg, op1Reg, op2Reg,
                    descriptor.GetIndirForm(), immediate, options);
                break;
            }

            case OperandKind.Reg:
            {
                Emitter.emitIns_SIMD_R_R_R_R_I(ins, attr, targetReg, op1Reg, op2Reg, descriptor.GetReg(), immediate, options);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void genXCNTIntrinsic(GenTreeHWIntrinsic node, instruction ins)
    {
        Emitter.RequireSupportedInstructionRecording();
        var op1 = node.GetOp(1);
        var sourceReg1 = REG_NA;
        var sourceReg2 = REG_NA;
        if (!op1.IsContained)
        {
            sourceReg1 = op1.RegNum;
        }
        else if (op1.Oper.IsIndir)
        {
            var indir = op1.AsIndir();
            if (indir.Base is GenTree baseNode)
            {
                sourceReg1 = baseNode.RegNum;
            }
            if (indir.Index is GenTree indexNode)
            {
                sourceReg2 = indexNode.RegNum;
            }
        }
        var targetReg = node.RegNum;
        if ((targetReg != sourceReg1) && (targetReg != sourceReg2))
        {
            // Break Intel's false destination dependency without destroying a real input.
            Emitter.emitIns_R_R(INS_xor, EA_4BYTE, targetReg, targetReg);
        }
        genHWIntrinsic_R_RM(node, ins, node.Type.EmitSize, targetReg, op1, INS_OPTS_NONE);
    }

    public void genX86SerializeIntrinsic(GenTreeHWIntrinsic node)
    {
        Emitter.RequireSupportedInstructionRecording();
        genConsumeMultiOpOperands(node);
        switch (node.HWIntrinsicId)
        {
            case NI_X86Serialize_Serialize:
            {
                assert(node.SimdBaseType == TYP_UNKNOWN);
                Emitter.emitIns(INS_serialize);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
        genProduceReg(node);
    }
}
#endif
