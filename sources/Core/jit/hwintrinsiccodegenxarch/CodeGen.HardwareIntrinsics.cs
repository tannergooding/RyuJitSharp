// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genHWIntrinsic(GenTreeHWIntrinsic node)
    {
        Emitter.RequireSupportedInstructionRecording();
        var intrinsic = node.HWIntrinsicId;
        var isa = HWIntrinsicInfo.lookupIsa(intrinsic);
        var category = HWIntrinsicInfo.lookupCategory(intrinsic);
        var numArgs = node.Operands.Length;
        GenTree? embMaskNode = null;
        GenTree? embMaskOp = null;
#if DEBUG
        if (isa == InstructionSet_Vector)
        {
            if (node.SimdSize == 64)
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_Vector512));
            }
            else if (node.SimdSize == 32)
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_Vector256));
            }
            else
            {
                assert(node.SimdSize is 8 or 12 or 16);
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_Vector128));
            }
        }
        else
        {
            assert(_compiler.compIsaSupportedDebugOnly(isa));
        }
        assert((HWIntrinsicInfo.lookupFlags(intrinsic) & HW_Flag_NoCodeGen) == 0);
        assert(!HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(intrinsic) || !varTypeIsSmall(node.SimdBaseType));
#endif
        var tableDriven = HWIntrinsicInfo.genIsTableDrivenHWIntrinsic(intrinsic, category);
        var options = INS_OPTS_NONE;
        if (Emitter.UseEvexEncodings)
        {
            if (numArgs == 3)
            {
                var op2 = node.GetOp(2);
                if ((op2.Flags & GTF_HW_EM_OP) != 0)
                {
                    assert(intrinsic == NI_AVX512_BlendVariableMask);
                    assert(op2.IsContained);
                    assert(op2.Oper.IsHWIntrinsic);
                    assert(tableDriven);
                    var op1 = node.GetOp(1);
                    var op3 = node.GetOp(3);
                    var targetReg = node.RegNum;
                    var mergeReg = op1.RegNum;
                    var maskReg = op3.RegNum;
                    assert(!op2.IsRmwHWIntrinsic(_compiler));
                    var mergeWithZero = op1.IsContained;
                    if (mergeWithZero)
                    {
                        assert(op1.IsVectorZero);
                    }
                    else
                    {
                        genConsumeReg(op1);
                        var attr = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
                        Emitter.emitIns_Mov(INS_movaps, attr, targetReg, mergeReg, canSkip: true);
                    }

                    // The contained operation produces the outer mask node's destination.
                    // Consume the mask and produce both logical nodes at their normal lifetime points.
                    op2.IsContained = false;
                    op2.RegNum = targetReg;
                    embMaskNode = node;
                    node = op2.AsHWIntrinsic();
                    intrinsic = node.HWIntrinsicId;
                    isa = HWIntrinsicInfo.lookupIsa(intrinsic);
                    category = HWIntrinsicInfo.lookupCategory(intrinsic);
                    numArgs = node.Operands.Length;
                    options = AddEmbMaskingMode(options, maskReg, mergeWithZero);
                    embMaskOp = op3;
                }
            }

            if (node.IsEmbeddedRoundingEnabled)
            {
                var lastOp = node.GetOp(numArgs);
                switch (numArgs)
                {
                    case 2:
                    {
                        numArgs = 1;
                        node.ResetHWIntrinsicId(intrinsic, node.GetOp(1));
                        break;
                    }

                    case 3:
                    {
                        numArgs = 2;
                        node.ResetHWIntrinsicId(intrinsic, node.GetOp(1), node.GetOp(2));
                        break;
                    }

                    case 4:
                    {
                        numArgs = 3;
                        node.ResetHWIntrinsicId(intrinsic, node.GetOp(1), node.GetOp(2), node.GetOp(3));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                if (lastOp.IsContained)
                {
                    assert(lastOp.Oper.IsCnsIntOrI);
                    options = AddEmbRoundingMode(options, unchecked((sbyte)lastOp.AsIntCon().IconValue));
                }
                else
                {
                    var ins = HWIntrinsicInfo.lookupIns(intrinsic, node.SimdBaseType, _compiler);
                    var simdSize = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
                    assert(ins != INS_invalid);
                    assert(simdSize != 0);
                    genConsumeMultiOpOperands(node);
                    genConsumeRegs(lastOp);
                    if (tableDriven)
                    {
                        if (embMaskOp is not null)
                        {
                            assert(embMaskNode is not null);
                            genConsumeReg(embMaskOp);
                        }
                        switch (numArgs)
                        {
                            case 1:
                            {
                                var targetReg = node.RegNum;
                                var operand = node.GetOp(1);
                                var baseReg = _internalRegisters.Extract(node);
                                var offsReg = _internalRegisters.GetSingle(node);
                                genHWIntrinsicJumpTableFallback(intrinsic, ins, simdSize, lastOp.RegNum, baseReg, offsReg,
                                    immediate => genHWIntrinsic_R_RM(node, ins, simdSize, targetReg, operand,
                                        AddEmbRoundingMode(options, immediate)));
                                break;
                            }

                            case 2:
                            {
                                var baseReg = _internalRegisters.Extract(node);
                                var offsReg = _internalRegisters.GetSingle(node);
                                genHWIntrinsicJumpTableFallback(intrinsic, ins, simdSize, lastOp.RegNum, baseReg, offsReg,
                                    immediate => genHWIntrinsic_R_R_RM(node, ins, simdSize,
                                        AddEmbRoundingMode(options, immediate)));
                                break;
                            }

                            default:
                            {
                                unreached();
                                break;
                            }
                        }
                    }
                    else
                    {
                        assert((embMaskNode is null) && (embMaskOp is null));
                        genNonTableDrivenHWIntrinsicsJumpTableFallback(node, lastOp);
                    }

                    genProduceReg(node);
                    if (embMaskNode is not null)
                    {
                        assert(embMaskOp is not null);
                        genProduceReg(embMaskNode);
                    }
                    return;
                }
            }
        }

        if (tableDriven)
        {
            genConsumeMultiOpOperands(node);
            if (embMaskOp is not null)
            {
                assert(embMaskNode is not null);
                genConsumeReg(embMaskOp);
            }

            var targetReg = node.RegNum;
            var baseType = node.SimdBaseType;
            var ins = HWIntrinsicInfo.lookupIns(intrinsic, baseType, _compiler);
            var simdSize = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
            assert(ins != INS_invalid);
            assert(simdSize != 0);
            var fixedImmediate = HWIntrinsicInfo.lookupIval(_compiler, intrinsic, baseType);
            switch (numArgs)
            {
                case 1:
                {
                    var op1 = node.GetOp(1);
                    if (node.IsMemoryLoad())
                    {
                        assert(options == INS_OPTS_NONE);
                        var load = indirForm(node.Type, op1);
                        Emitter.emitInsLoadInd(ins, simdSize, targetReg, load);
                    }
                    else
                    {
                        var op1Reg = op1.RegNum;
                        if (fixedImmediate != -1)
                        {
                            assert((fixedImmediate >= 0) && (fixedImmediate <= 127));
                            if (HWIntrinsicInfo.CopiesUpperBits(intrinsic))
                            {
                                assert(!op1.IsContained);
                                Emitter.emitIns_SIMD_R_R_R_I(ins, simdSize, targetReg, op1Reg, op1Reg,
                                    (sbyte)fixedImmediate, options);
                            }
                            else
                            {
                                genHWIntrinsic_R_RM_I(node, ins, simdSize, (sbyte)fixedImmediate, options);
                            }
                        }
                        else if (HWIntrinsicInfo.CopiesUpperBits(intrinsic))
                        {
                            assert(!op1.IsContained);
                            Emitter.emitIns_SIMD_R_R_R(ins, simdSize, targetReg, op1Reg, op1Reg, options);
                        }
                        else
                        {
                            genHWIntrinsic_R_RM(node, ins, simdSize, targetReg, op1, options);
                        }
                    }
                    break;
                }

                case 2:
                {
                    var op1 = node.GetOp(1);
                    var op2 = node.GetOp(2);
                    if (category == HW_Category_MemoryStore)
                    {
                        assert(options == INS_OPTS_NONE);
                        var store = storeIndirForm(node.Type, op1, op2);
                        Emitter.emitInsStoreInd(ins, simdSize, store);
                        break;
                    }
                    var op1Reg = op1.RegNum;
                    var op2Reg = op2.RegNum;
                    if ((op1Reg != targetReg) && (op2Reg == targetReg) && node.IsRmwHWIntrinsic(_compiler))
                    {
                        // LSRA delay-frees noncommutative second operands; a commutative
                        // instruction can instead make the target its first source.
                        noway_assert(node.IsCommutativeHWIntrinsic);
                        op2Reg = op1Reg;
                        op1Reg = targetReg;
                    }

                    if (fixedImmediate != -1)
                    {
                        assert((fixedImmediate >= 0) && (fixedImmediate <= 127));
                        genHWIntrinsic_R_R_RM_I(node, ins, simdSize, (sbyte)fixedImmediate, options);
                    }
                    else if (category == HW_Category_MemoryLoad)
                    {
                        var isMaskLoad = intrinsic is NI_AVX_MaskLoad or NI_AVX2_MaskLoad;
                        var address = isMaskLoad ? op1 : op2;
                        var otherReg = isMaskLoad ? op2Reg : op1Reg;
                        var load = indirForm(node.Type, address);
                        assert(!node.IsRmwHWIntrinsic(_compiler));
                        inst_RV_RV_TT(ins, simdSize, targetReg, otherReg, load, false, options);
                    }
                    else if (HWIntrinsicInfo.isImmOp(intrinsic, op2))
                    {
                        void EmitCase(sbyte immediate)
                        {
                            if (HWIntrinsicInfo.CopiesUpperBits(intrinsic))
                            {
                                assert(!op1.IsContained);
                                Emitter.emitIns_SIMD_R_R_R_I(ins, simdSize, targetReg, op1Reg, op1Reg, immediate, options);
                            }
                            else
                            {
                                genHWIntrinsic_R_RM_I(node, ins, simdSize, immediate, options);
                            }
                        }

                        if (op2.Oper.IsCnsIntOrI)
                        {
                            var immediate = op2.AsIntCon().IconValue;
                            assert((immediate >= 0) && (immediate <= 255));
                            EmitCase(unchecked((sbyte)immediate));
                        }
                        else
                        {
                            var baseReg = _internalRegisters.Extract(node);
                            var offsReg = _internalRegisters.GetSingle(node);
                            genHWIntrinsicJumpTableFallback(intrinsic, ins, simdSize, op2Reg, baseReg, offsReg, EmitCase);
                        }
                    }
                    else if (node.Type == TYP_VOID)
                    {
                        genHWIntrinsic_R_RM(node, ins, simdSize, op1Reg, op2, options);
                    }
                    else
                    {
                        genHWIntrinsic_R_R_RM(node, ins, simdSize, options);
                    }
                    break;
                }

                case 3:
                {
                    var op1 = node.GetOp(1);
                    var op2 = node.GetOp(2);
                    var op3 = node.GetOp(3);
                    var op1Reg = op1.RegNum;
                    var op2Reg = op2.RegNum;
                    var op3Reg = op3.RegNum;
                    assert(fixedImmediate == -1);
                    if (HWIntrinsicInfo.isImmOp(intrinsic, op3))
                    {
                        void EmitCase(sbyte immediate)
                        {
                            genHWIntrinsic_R_R_RM_I(node, ins, simdSize, immediate, options);
                        }
                        if (op3.Oper.IsCnsIntOrI)
                        {
                            var immediate = op3.AsIntCon().IconValue;
                            assert((immediate >= 0) && (immediate <= 255));
                            EmitCase(unchecked((sbyte)immediate));
                        }
                        else
                        {
                            var baseReg = _internalRegisters.Extract(node);
                            var offsReg = _internalRegisters.GetSingle(node);
                            genHWIntrinsicJumpTableFallback(intrinsic, ins, simdSize, op3Reg, baseReg, offsReg, EmitCase);
                        }
                    }
                    else if (category == HW_Category_MemoryLoad)
                    {
                        var mergeWithZero = false;
                        if (op3.IsContained)
                        {
                            op3Reg = targetReg;
                            mergeWithZero = true;
                        }
                        assert(genIsValidMaskReg(op2Reg));
                        assert(mergeWithZero == op3.IsVectorZero);
                        var load = indirForm(node.Type, op1);
                        Emitter.emitIns_Mov(INS_movaps, simdSize, targetReg, op3Reg, canSkip: true);
                        options = AddEmbMaskingMode(options, op2Reg, mergeWithZero);
                        Emitter.emitIns_R_A(ins, simdSize, targetReg, load, options);
                    }
                    else if (category == HW_Category_MemoryStore)
                    {
                        if (genIsValidMaskReg(op2Reg))
                        {
                            var store = storeIndirForm(node.Type, op1, op3);
                            options = AddEmbMaskingMode(options, op2Reg, false);
                            Emitter.emitInsStoreInd(ins, simdSize, store, options);
                            break;
                        }
                        assert(!op2.IsContained);
                        if (intrinsic is NI_AVX_MaskStore or NI_AVX2_MaskStore)
                        {
                            Emitter.emitIns_AR_R_R(ins, simdSize, op2Reg, op3Reg, op1Reg, 0, options);
                        }
                        else
                        {
                            assert(intrinsic == NI_X86Base_MaskMove);
                            assert(targetReg == REG_NA);
                            // SSE2 MASKMOV uses DI/EDI/RDI implicitly for its destination.
                            Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_EDI, op3Reg, canSkip: true);
                            Emitter.emitIns_R_R(ins, simdSize, op1Reg, op2Reg, options);
                        }
                    }
                    else
                    {
                        switch (intrinsic)
                        {
                            case NI_X86Base_BlendVariable:
                            case NI_AVX_BlendVariable:
                            case NI_AVX2_BlendVariable:
                            case NI_AVX512_BlendVariableMask:
                            {
                                genHWIntrinsic_R_R_RM_R(node, ins, simdSize, options);
                                break;
                            }

                            case NI_AVX512_CompressMask:
                            case NI_AVX512_ExpandMask:
                            {
                                var mergeWithZero = false;
                                if (op1.IsContained)
                                {
                                    op1Reg = targetReg;
                                    mergeWithZero = true;
                                }
                                assert(genIsValidMaskReg(op2Reg));
                                assert(mergeWithZero == op1.IsVectorZero);
                                var attr = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
                                Emitter.emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
                                options = AddEmbMaskingMode(options, op2Reg, mergeWithZero);
                                Emitter.emitIns_R_R(ins, attr, targetReg, op3Reg, options);
                                break;
                            }

                            case NI_AVXVNNI_MultiplyWideningAndAdd:
                            case NI_AVXVNNI_MultiplyWideningAndAddSaturate:
                            case NI_AVX512v3_MultiplyWideningAndAdd:
                            case NI_AVX512v3_MultiplyWideningAndAddSaturate:
                            case NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction:
                            case NI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction:
                            {
                                assert(targetReg != REG_NA);
                                assert(op1Reg != REG_NA);
                                assert(op2Reg != REG_NA);
                                genHWIntrinsic_R_R_R_RM(ins, simdSize, targetReg, op1Reg, op2Reg, op3, options);
                                break;
                            }

                            default:
                            {
                                unreached();
                                break;
                            }
                        }
                    }
                    break;
                }

                case 4:
                {
                    var op4 = node.GetOp(4);
                    assert(fixedImmediate == -1);
                    if (HWIntrinsicInfo.isImmOp(intrinsic, op4))
                    {
                        void EmitCase(sbyte immediate)
                        {
                            genHWIntrinsic_R_R_R_RM_I(node, ins, simdSize, immediate, options);
                        }
                        if (op4.Oper.IsCnsIntOrI)
                        {
                            var immediate = op4.AsIntCon().IconValue;
                            assert((immediate >= 0) && (immediate <= 255));
                            EmitCase(unchecked((sbyte)immediate));
                        }
                        else
                        {
                            var baseReg = _internalRegisters.Extract(node);
                            var offsReg = _internalRegisters.GetSingle(node);
                            genHWIntrinsicJumpTableFallback(intrinsic, ins, simdSize, op4.RegNum, baseReg, offsReg, EmitCase);
                        }
                    }
                    else
                    {
                        unreached();
                    }
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            genProduceReg(node);
            if (embMaskNode is not null)
            {
                assert(embMaskOp is not null);
                genProduceReg(embMaskNode);
            }
            return;
        }

        assert((embMaskNode is null) && (embMaskOp is null));
        switch (isa)
        {
            case InstructionSet_Vector:
            {
                genBaseIntrinsic(node, options);
                break;
            }

            case InstructionSet_X86Base:
            case InstructionSet_X86Base_X64:
            {
                genX86BaseIntrinsic(node, options);
                break;
            }

            case InstructionSet_AVX:
            case InstructionSet_AVX2:
            case InstructionSet_AVX2_X64:
            case InstructionSet_AVX512:
            case InstructionSet_AVX512_X64:
            case InstructionSet_AVX512v2:
            case InstructionSet_AVX10v1:
            case InstructionSet_AVX10v2:
            case InstructionSet_AVX10v2_X64:
            case InstructionSet_AVXVNNIINT:
            case InstructionSet_AVXVNNIINT_V512:
            {
                genAvxFamilyIntrinsic(node, options);
                break;
            }

            case InstructionSet_X86Serialize:
            case InstructionSet_X86Serialize_X64:
            {
                assert(options == INS_OPTS_NONE);
                genX86SerializeIntrinsic(node);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }
}
#endif
