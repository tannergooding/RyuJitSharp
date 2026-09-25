// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genBaseIntrinsic(GenTreeHWIntrinsic node, insOpts instOptions)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Base hardware intrinsic generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var intrinsicId = node.HWIntrinsicId;
        var targetReg = node.RegNum;
        var baseType = node.SimdBaseType;
        assert((baseType >= TYP_BYTE) && (baseType <= TYP_DOUBLE));

        var op1 = node.Operands.Length >= 1 ? node.GetOp(1) : null;
        var op2 = node.Operands.Length >= 2 ? node.GetOp(2) : null;
        var op3 = node.Operands.Length >= 3 ? node.GetOp(3) : null;

        genConsumeMultiOpOperands(node);
        var op1Reg = op1 is null ? REG_NA : op1.RegNum;
        var emit = Emitter;
        var simdType = Compiler.GetSimdTypeForSize(node.SimdSize);
        var attr = simdType.ActualType.EmitSize;
        var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);

        switch (intrinsicId)
        {
            case NI_Vector_CreateScalar:
            case NI_Vector_CreateScalarUnsafe:
            {
                assert(op1 is not null);
                if (varTypeIsIntegral(baseType))
                {
                    var baseAttr = baseType.ActualType.EmitSize;
                    if (op1.IsUsedFromMemory && (baseAttr == EA_8BYTE))
                    {
                        ins = INS_movq;
                    }
                    genHWIntrinsic_R_RM(node, ins, baseAttr, targetReg, op1, instOptions);
                }
                else
                {
                    assert(varTypeIsFloating(baseType));
                    attr = baseType.EmitSize;
                    if (op1.IsContained || op1.IsUsedFromSpillTemp)
                    {
                        genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                    }
                    else
                    {
                        assert(instOptions == INS_OPTS_NONE);
                        if (HWIntrinsicInfo.IsVectorCreateScalar(intrinsicId))
                        {
                            // Integer and memory loads already zero the upper elements.
                            // Floating register copies need explicit zeroing for CreateScalar.
                            if (baseType == TYP_FLOAT)
                            {
                                // insertps: zmask=1110, destination lane=0, source lane=0.
                                emit.emitIns_SIMD_R_R_R_I(INS_insertps, attr, targetReg, targetReg,
                                    op1Reg, 0x0E, instOptions);
                            }
                            else
                            {
                                _ = emit.emitIns_Mov(INS_movq, attr, targetReg, op1Reg, canSkip: false);
                            }
                            break;
                        }

                        // movaps permits a zero-latency register copy without promising zeroed lanes.
                        _ = emit.emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
                    }
                }
                break;
            }

            case NI_Vector_WithElement:
            {
                assert(op1 is not null);
                assert(op2 is not null);
                assert(op3 is not null);
                assert(!op1.IsContained);
                assert(!op2.Oper.IsConst);

                // Lowering has already range-checked the variable index. Spill the
                // vector to the SIMD temp, replace the element, then reload it.
                var simdInitTempVarNum = _compiler.lvaSimdInitTempVarNum;
                noway_assert(simdInitTempVarNum != BAD_VAR_NUM);
                var offs = _compiler.lvaFrameAddress(simdInitTempVarNum, out var isEBPbased);
#if !FEATURE_FIXED_OUT_ARGS
                if (!isEBPbased)
                {
                    offs = unchecked(offs + (int)genStackLevel);
                }
#else
                assert(genStackLevel == 0);
#endif
                assert(op2.Type == TYP_I_IMPL);
                var indexReg = op2.RegNum;
                var valueReg = op3.RegNum;

                emit.emitIns_S_R(ins_Store(simdType, _compiler.isSIMDTypeLocalAligned(simdInitTempVarNum)),
                    simdType.EmitSize, op1Reg, simdInitTempVarNum, 0);
                emit.emitIns_ARX_R(ins_Store(op3.Type), baseType.EmitSize, valueReg,
                    isEBPbased ? REG_EBP : REG_ESP, indexReg, baseType.Size, offs);
                emit.emitIns_R_S(ins_Load(simdType, _compiler.isSIMDTypeLocalAligned(simdInitTempVarNum)),
                    simdType.EmitSize, targetReg, simdInitTempVarNum, 0);
                break;
            }

            case NI_Vector_GetElement:
            {
                assert(op1 is not null);
                assert(op2 is not null);
                assert(instOptions == INS_OPTS_NONE);
                if (simdType == TYP_SIMD12)
                {
                    simdType = TYP_SIMD16;
                }
                assert(op2.Type == TYP_I_IMPL);

                if (!op1.IsUsedFromReg)
                {
                    assert(op1.IsContained);
                    regNumber baseReg;
                    regNumber indexReg;
                    var offset = 0;

                    if (op1.Oper.IsLocal)
                    {
                        var varNum = op1.AsLclVarCommon().LclNum;
                        offset = unchecked(offset + _compiler.lvaFrameAddress(varNum, out var isEBPbased));
#if !FEATURE_FIXED_OUT_ARGS
                        if (!isEBPbased)
                        {
                            offset = unchecked(offset + (int)genStackLevel);
                        }
#else
                        assert(genStackLevel == 0);
#endif
                        if (op1.Oper == GT_LCL_FLD)
                        {
                            offset = unchecked(offset + op1.AsLclFld().LclOffs);
                        }
                        baseReg = isEBPbased ? REG_EBP : REG_ESP;
                    }
                    else if (op1.Oper.IsCnsVec)
                    {
                        var hnd = emit.emitSimdConst(in op1.AsVecCon().SimdVal, op1.Type.EmitSize);
                        baseReg = _internalRegisters.GetSingle(node);
                        emit.emitIns_R_C(INS_lea, TYP_I_IMPL.EmitSize, baseReg, hnd, 0, INS_OPTS_NONE);
                    }
                    else
                    {
                        assert(op1.Oper == GT_IND);
                        var addr = op1.AsIndir().Addr;
                        assert(!addr.IsContained);
                        baseReg = addr.RegNum;
                    }

                    if (op2.Oper.IsConst)
                    {
                        assert(op2.IsContained);
                        indexReg = REG_NA;
                        offset = unchecked(offset + (int)op2.AsIntCon().IconValue * (int)baseType.Size);
                    }
                    else
                    {
                        indexReg = op2.RegNum;
                        assert(genIsValidIntReg(indexReg));
                    }

                    emit.emitIns_R_ARX(ins_Move_Extend(baseType, false), baseType.EmitSize, targetReg,
                        baseReg, indexReg, baseType.Size, offset);
                }
                else if (op2.Oper.IsConst)
                {
                    assert(simdType == TYP_SIMD16);
                    assert(varTypeIsFloating(baseType));
                    assert(op1Reg != REG_NA);
                    var ival = op2.AsIntCon().IconValue;

                    if (baseType == TYP_FLOAT)
                    {
                        if (ival == 1)
                        {
                            emit.emitIns_R_R(INS_movshdup, attr, targetReg, op1Reg);
                        }
                        else if (ival == 2)
                        {
                            emit.emitIns_SIMD_R_R_R(INS_unpckhps, attr, targetReg, op1Reg, op1Reg, instOptions);
                        }
                        else
                        {
                            assert(ival == 3);
                            emit.emitIns_SIMD_R_R_R_I(INS_shufps, attr, targetReg, op1Reg, op1Reg,
                                unchecked((sbyte)0xFF), instOptions);
                        }
                    }
                    else
                    {
                        assert(baseType == TYP_DOUBLE);
                        assert(ival == 1);
                        emit.emitIns_SIMD_R_R_R(INS_unpckhpd, attr, targetReg, op1Reg, op1Reg, instOptions);
                    }
                }
                else
                {
                    // The checked variable index requires a memory round trip.
                    var simdInitTempVarNum = _compiler.lvaSimdInitTempVarNum;
                    noway_assert(simdInitTempVarNum != BAD_VAR_NUM);
                    var offs = _compiler.lvaFrameAddress(simdInitTempVarNum, out var isEBPbased);
#if !FEATURE_FIXED_OUT_ARGS
                    if (!isEBPbased)
                    {
                        offs = unchecked(offs + (int)genStackLevel);
                    }
#else
                    assert(genStackLevel == 0);
#endif
                    var indexReg = op2.RegNum;
                    emit.emitIns_S_R(ins_Store(simdType, _compiler.isSIMDTypeLocalAligned(simdInitTempVarNum)),
                        simdType.EmitSize, op1Reg, simdInitTempVarNum, 0);
                    emit.emitIns_R_ARX(ins_Move_Extend(baseType, false), baseType.EmitSize, targetReg,
                        isEBPbased ? REG_EBP : REG_ESP, indexReg, baseType.Size, offs);
                }
                break;
            }

            case NI_Vector_AsVector128Unsafe:
            case NI_Vector_AsVector2:
            case NI_Vector_AsVector3:
            case NI_Vector_ToScalar:
            {
                assert(op1 is not null);
                // A descriptor may resolve a contained operand to a register.
                // Only a genuinely contained descriptor permits an integer load.
                var op1Desc = genOperandDesc(ins, op1);
                if (op1Desc.IsContained())
                {
                    if (varTypeIsIntegral(baseType))
                    {
                        ins = ins_Move_Extend(baseType, false);
                        attr = baseType.EmitSize;
                    }
                    genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1Desc, op1, instOptions);
                }
                else if (varTypeIsIntegral(baseType))
                {
                    assert(!varTypeIsLong(baseType) || TargetArchitecture.Is64Bit);
                    assert(intrinsicId == NI_Vector_ToScalar);
                    attr = baseType.ActualType.EmitSize;
                    genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1Desc, op1, instOptions);

                    if (varTypeIsSmall(baseType))
                    {
                        _ = emit.emitIns_Mov(ins_Move_Extend(baseType, true), baseType.EmitSize,
                            targetReg, targetReg, canSkip: false);
                    }
                }
                else
                {
                    assert(varTypeIsFloating(baseType));
                    assert(instOptions == INS_OPTS_NONE);
                    _ = emit.emitIns_Mov(INS_movaps, attr, targetReg, op1Desc.GetReg(), canSkip: true);
                }
                break;
            }

            case NI_Vector_ToVector256:
            case NI_Vector_ToVector512:
            {
                assert(op1 is not null);
                // A same-register move is still required: VEX/EVEX zeroes the
                // upper lanes to give the widening operation deterministic results.
                if (simdType == TYP_SIMD32)
                {
                    assert(intrinsicId == NI_Vector_ToVector512);
                    attr = TYP_SIMD32.EmitSize;
                }
                else
                {
                    attr = TYP_SIMD16.EmitSize;
                }

                if (op1.IsContained || op1.IsUsedFromSpillTemp)
                {
                    genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                }
                else
                {
                    assert(instOptions == INS_OPTS_NONE);
                    _ = emit.emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: false);
                }
                break;
            }

            case NI_Vector_ToVector256Unsafe:
            case NI_Vector_ToVector512Unsafe:
            case NI_Vector_GetLower:
            case NI_Vector_GetLower128:
            {
                assert(op1 is not null);
                if (op1.IsContained || op1.IsUsedFromSpillTemp)
                {
                    // Read only the necessary lower lanes from memory.
                    if (intrinsicId == NI_Vector_GetLower)
                    {
                        attr = node.Type.EmitSize;
                    }
                    else if (intrinsicId == NI_Vector_ToVector512Unsafe)
                    {
                        attr = TYP_SIMD32.EmitSize;
                    }
                    else
                    {
                        attr = TYP_SIMD16.EmitSize;
                    }
                    genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                }
                else
                {
                    assert(instOptions == INS_OPTS_NONE);
                    // Upper lanes are unspecified, permitting same-register elision.
                    if (intrinsicId == NI_Vector_GetLower)
                    {
                        attr = node.Type.EmitSize;
                    }
                    else if (intrinsicId == NI_Vector_ToVector256Unsafe)
                    {
                        attr = TYP_SIMD32.EmitSize;
                    }
                    else
                    {
                        attr = TYP_SIMD64.EmitSize;
                    }
                    _ = emit.emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
                }
                break;
            }

            case NI_Vector_op_Division:
            {
                assert(op2 is not null);
                // Check zero divisors and signed overflow before widening int32
                // lanes to double, dividing, and truncating back to int32.
                var op2Reg = op2.RegNum;
                var tmpReg1 = REG_NA;
                if (!_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    tmpReg1 = _internalRegisters.Extract(node,
                        _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX)
                            ? new regMaskTP(SRBM_ALLFLOAT) : new regMaskTP(SRBM_XMM0));
                }
                var tmpReg2 = _internalRegisters.Extract(node, new regMaskTP(SRBM_ALLFLOAT));
                var tmpReg3 = _internalRegisters.Extract(node, new regMaskTP(SRBM_ALLFLOAT));
                var nodeType = node.Type;
                var typeSize = nodeType.EmitSize;
                noway_assert(typeSize is EA_16BYTE or EA_32BYTE);
                var divTypeSize = typeSize;

                if (_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    divTypeSize = typeSize == EA_16BYTE ? EA_32BYTE : EA_64BYTE;
                }
                else if (_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX) && (typeSize == EA_16BYTE))
                {
                    divTypeSize = EA_32BYTE;
                }

                emit.emitIns_SIMD_R_R_R(INS_xorpd, typeSize, tmpReg2, tmpReg2, tmpReg2, instOptions);
                emit.emitIns_SIMD_R_R_R(INS_pcmpeqd, typeSize, tmpReg2, tmpReg2, op2Reg, instOptions);
                emit.emitIns_R_R(INS_ptest, typeSize, tmpReg2, tmpReg2, instOptions);
                genJumpToThrowHlpBlk(EJ_jne, SCK_DIV_BY_ZERO);

                if (varTypeIsSigned(baseType))
                {
                    simd_t minValueInt = default;
                    var numElements = nodeType.Size / 4;
                    for (var i = 0; i < numElements; i++)
                    {
                        minValueInt.i32[i] = int.MinValue;
                    }
                    var minValueFld = emit.emitSimdConst(in minValueInt, typeSize);
                    var negOneIntVec = simd_t.AllBitsSet;
                    var negOneFld = emit.emitSimdConst(in negOneIntVec, typeSize);

                    emit.emitIns_SIMD_R_R_C(INS_pcmpeqd, typeSize, tmpReg2, op1Reg, minValueFld, 0, instOptions);
                    emit.emitIns_SIMD_R_R_C(INS_pcmpeqd, typeSize, tmpReg3, op2Reg, negOneFld, 0, instOptions);
                    emit.emitIns_SIMD_R_R_R(INS_pandd, typeSize, tmpReg2, tmpReg2, tmpReg3, instOptions);
                    emit.emitIns_R_R(INS_ptest, typeSize, tmpReg2, tmpReg2, instOptions);
                    genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
                    emit.emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg2, op1Reg, instOptions);
                    emit.emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg3, op2Reg, instOptions);
                }
                else if (_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    emit.emitIns_R_R(INS_vcvtudq2pd, divTypeSize, tmpReg2, op1Reg, instOptions);
                    emit.emitIns_R_R(INS_vcvtudq2pd, divTypeSize, tmpReg3, op2Reg, instOptions);
                }
                else
                {
                    simd_t double2To32Const = default;
                    var numElements = nodeType.Size / 2;
                    for (var i = 0; i < numElements; i++)
                    {
                        double2To32Const.f64[i] = 4294967296.0; // 2^32 corrects negative signed conversions.
                    }
                    var double2To32ConstFld = emit.emitSimdConst(in double2To32Const, divTypeSize);

                    // Preserve the pinned C++ argument positions: here instOptions
                    // converts to useApxNdd/canSkip for moves and to offs for R_C.
                    if (_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX))
                    {
                        emit.emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg1, op1Reg, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, divTypeSize, tmpReg2, tmpReg1, false,
                            instOptions != INS_OPTS_NONE);
                        emit.emitIns_R_C(INS_addpd, divTypeSize, tmpReg2, double2To32ConstFld,
                            unchecked((int)instOptions));
                        emit.emitIns_SIMD_R_R_R_R(INS_blendvpd, divTypeSize, tmpReg2, tmpReg1, tmpReg2,
                            tmpReg1, instOptions);

                        emit.emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg1, op2Reg, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, divTypeSize, tmpReg3, tmpReg1, false,
                            instOptions != INS_OPTS_NONE);
                        emit.emitIns_R_C(INS_addpd, divTypeSize, tmpReg3, double2To32ConstFld,
                            unchecked((int)instOptions));
                        emit.emitIns_SIMD_R_R_R_R(INS_blendvpd, divTypeSize, tmpReg3, tmpReg1, tmpReg3,
                            tmpReg1, instOptions);
                    }
                    else
                    {
                        emit.emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg1, op1Reg, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, typeSize, tmpReg2, tmpReg1, false,
                            instOptions != INS_OPTS_NONE);
                        emit.emitIns_R_C(INS_addpd, typeSize, tmpReg2, double2To32ConstFld,
                            unchecked((int)instOptions));
                        emit.emitIns_R_R(INS_blendvpd, typeSize, tmpReg1, tmpReg2, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, typeSize, tmpReg2, tmpReg1, instOptions != INS_OPTS_NONE);

                        emit.emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg1, op2Reg, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, typeSize, tmpReg3, tmpReg1, false,
                            instOptions != INS_OPTS_NONE);
                        emit.emitIns_R_C(INS_addpd, typeSize, tmpReg3, double2To32ConstFld,
                            unchecked((int)instOptions));
                        emit.emitIns_R_R(INS_blendvpd, typeSize, tmpReg1, tmpReg3, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, typeSize, tmpReg3, tmpReg1, instOptions != INS_OPTS_NONE);
                    }
                }

                if (varTypeIsSigned(baseType) || _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    emit.emitIns_SIMD_R_R_R(INS_divpd, divTypeSize, targetReg, tmpReg2, tmpReg3, instOptions);
                    emit.emitIns_R_R(varTypeIsSigned(baseType) ? INS_cvttpd2dq : INS_vcvttpd2udq,
                        divTypeSize, targetReg, targetReg, instOptions);
                }
                else
                {
                    assert(varTypeIsUnsigned(baseType));
                    emit.emitIns_SIMD_R_R_R(INS_divpd, divTypeSize, tmpReg1, tmpReg2, tmpReg3, instOptions);

                    // A quotient >= 2^31 requires divisor 1. Replace the conversion
                    // sentinel with the dividend, selecting each packed int32 lane.
                    if (_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX))
                    {
                        emit.emitIns_R_R(INS_cvttpd2dq, divTypeSize, tmpReg3, tmpReg1, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, typeSize, tmpReg1, op1Reg, instOptions != INS_OPTS_NONE);
                        emit.emitIns_SIMD_R_R_R_R(INS_blendvps, typeSize, targetReg, tmpReg3, tmpReg1,
                            tmpReg3, instOptions);
                    }
                    else
                    {
                        emit.emitIns_R_R(INS_cvttpd2dq, divTypeSize, tmpReg1, tmpReg1, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, typeSize, tmpReg2, op1Reg, instOptions != INS_OPTS_NONE);
                        emit.emitIns_R_R(INS_blendvps, typeSize, tmpReg1, tmpReg2, instOptions);
                        _ = emit.emitIns_Mov(INS_movups, typeSize, targetReg, tmpReg1, false);
                    }
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
#endif
    }

    public void ClearUnusedMaskBits(regNumber maskReg, uint count)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Mask lane clearing requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(count is 2 or 4);
        assert(maskReg.IsMskReg);

        Emitter.emitIns_R_R_I(INS_kshiftlb, EA_8BYTE, maskReg, maskReg, unchecked((sbyte)(8 - count)));
        Emitter.emitIns_R_R_I(INS_kshiftrb, EA_8BYTE, maskReg, maskReg, unchecked((sbyte)(8 - count)));
#endif
    }
}
#endif
