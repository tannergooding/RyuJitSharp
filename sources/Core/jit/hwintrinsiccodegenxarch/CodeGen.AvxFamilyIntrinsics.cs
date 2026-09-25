// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if FEATURE_HW_INTRINSICS
    public void genAvxFamilyIntrinsic(GenTreeHWIntrinsic node, insOpts instOptions)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "AVX family generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var intrinsicId = node.HWIntrinsicId;
        if (HWIntrinsicInfo.IsFmaIntrinsic(intrinsicId))
        {
            genConsumeMultiOpOperands(node);
            genFmaIntrinsic(node, instOptions);
            genProduceReg(node);
            return;
        }

        if (HWIntrinsicInfo.IsPermuteVar2x(intrinsicId))
        {
            genPermuteVar2x(node, instOptions);
            return;
        }

        var baseType = node.SimdBaseType;
        var targetType = node.Type;
        emitAttr attr;
        if (baseType == TYP_UNKNOWN)
        {
            baseType = targetType;
            attr = targetType.EmitSize;
        }
        else
        {
            attr = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
        }

        var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
        var numArgs = node.Operands.Length;
        var op1 = node.GetOp(1);
        var targetReg = node.RegNum;
        genConsumeMultiOpOperands(node);

        switch (intrinsicId)
        {
            case NI_AVX2_AndNotScalar:
            case NI_AVX2_X64_AndNot:
            case NI_AVX2_BitFieldExtract:
            case NI_AVX2_X64_BitFieldExtract:
            case NI_AVX2_ParallelBitDeposit:
            case NI_AVX2_ParallelBitExtract:
            case NI_AVX2_X64_ParallelBitDeposit:
            case NI_AVX2_X64_ParallelBitExtract:
            case NI_AVX2_ZeroHighBits:
            case NI_AVX2_X64_ZeroHighBits:
            {
                assert(targetType is TYP_INT or TYP_LONG);
                genHWIntrinsic_R_R_RM(node, ins, attr, instOptions);
                break;
            }

            case NI_AVX2_ConvertToInt32:
            case NI_AVX2_ConvertToUInt32:
            {
                assert(instOptions == INS_OPTS_NONE);
                var op1Reg = op1.RegNum;
                assert(baseType is TYP_INT or TYP_UINT);
                ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                _ = Emitter.emitIns_Mov(ins, baseType.EmitActualSize, targetReg, op1Reg, canSkip: false);
                break;
            }

            case NI_AVX2_ConvertToVector256Int16:
            case NI_AVX2_ConvertToVector256Int32:
            case NI_AVX2_ConvertToVector256Int64:
            {
                ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                if (node.IsMemoryLoad())
                {
                    // Address-mode emission uses a temporary indirection, not a replacement IR node.
                    var load = new GenTreeIndir(GT_IND, targetType, op1);
                    Emitter.emitInsLoadInd(ins, EA_32BYTE, targetReg, load);
                }
                else
                {
                    genHWIntrinsic_R_RM(node, ins, EA_32BYTE, targetReg, op1, instOptions);
                }
                break;
            }

            case NI_AVX2_ExtractLowestSetBit:
            case NI_AVX2_GetMaskUpToLowestSetBit:
            case NI_AVX2_ResetLowestSetBit:
            case NI_AVX2_X64_ExtractLowestSetBit:
            case NI_AVX2_X64_GetMaskUpToLowestSetBit:
            case NI_AVX2_X64_ResetLowestSetBit:
            {
                assert(targetType is TYP_INT or TYP_LONG);
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                break;
            }

            case NI_AVX2_GatherVector128:
            case NI_AVX2_GatherVector256:
            case NI_AVX2_GatherMaskVector128:
            case NI_AVX2_GatherMaskVector256:
            {
                assert(instOptions == INS_OPTS_NONE);
                var op2 = node.GetOp(2);
                var op3 = node.GetOp(3);
                GenTree lastOp;
                GenTree indexOp;
                var op1Reg = op1.RegNum;
                var op2Reg = op2.RegNum;
                regNumber addrBaseReg;
                regNumber addrIndexReg;
                var maskReg = _internalRegisters.Extract(node, new regMaskTP(SRBM_ALLFLOAT));
                if (numArgs == 5)
                {
                    assert(intrinsicId is NI_AVX2_GatherMaskVector128 or NI_AVX2_GatherMaskVector256);
                    var op4 = node.GetOp(4);
                    lastOp = node.GetOp(5);
                    addrBaseReg = op2Reg;
                    addrIndexReg = op3.RegNum;
                    indexOp = op3;

                    // Gather clears its mask, so preserve the input mask and initialize the merge destination.
                    _ = Emitter.emitIns_Mov(INS_movaps, attr, maskReg, op4.RegNum, canSkip: false);
                    _ = Emitter.emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
                }
                else
                {
                    assert(intrinsicId is NI_AVX2_GatherVector128 or NI_AVX2_GatherVector256);
                    addrBaseReg = op1Reg;
                    addrIndexReg = op2Reg;
                    indexOp = op2;
                    lastOp = op3;
                    assert(!RyuJitSharp.Emitter.isHighSimdReg(targetReg));
                    Emitter.emitIns_SIMD_R_R_R(INS_pcmpeqd, attr, maskReg, maskReg, maskReg, instOptions);
                }

                var isVector128GatherWithVector256Index = (targetType == TYP_SIMD16) && (indexOp.Type == TYP_SIMD32);
                if (varTypeIsLong(node.AuxiliaryType))
                {
                    // The instruction table defaults to dword indices.
                    switch (ins)
                    {
                        case INS_vpgatherdd:
                        {
                            ins = INS_vpgatherqd;
                            if (isVector128GatherWithVector256Index)
                            {
                                attr = EA_32BYTE;
                            }
                            break;
                        }
                        case INS_vpgatherdq:
                        {
                            ins = INS_vpgatherqq;
                            break;
                        }
                        case INS_vgatherdps:
                        {
                            ins = INS_vgatherqps;
                            if (isVector128GatherWithVector256Index)
                            {
                                attr = EA_32BYTE;
                            }
                            break;
                        }
                        case INS_vgatherdpd:
                        {
                            ins = INS_vgatherqpd;
                            break;
                        }
                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                }

                assert(lastOp.Oper.IsCnsIntOrI);
                var ival = lastOp.AsIntCon().IconValue;
                assert((ival >= 0) && (ival <= 255));
                assert(targetReg != maskReg);
                assert(targetReg != addrIndexReg);
                assert(maskReg != addrIndexReg);
                Emitter.emitIns_R_AR_R(ins, attr, targetReg, maskReg, addrBaseReg, addrIndexReg, unchecked((sbyte)ival), 0);
                break;
            }

            case NI_AVX2_LeadingZeroCount:
            case NI_AVX2_TrailingZeroCount:
            case NI_AVX2_X64_LeadingZeroCount:
            case NI_AVX2_X64_TrailingZeroCount:
            {
                assert(targetType is TYP_INT or TYP_LONG);
                genXCNTIntrinsic(node, ins);
                break;
            }

            case NI_AVX2_MultiplyNoFlags:
            case NI_AVX2_X64_MultiplyNoFlags:
            {
                assert(instOptions == INS_OPTS_NONE);
                assert(numArgs is 2 or 3);
                var op2 = node.GetOp(2);
                var op1Reg = op1.RegNum;
                var op2Reg = op2.RegNum;
                var op3Reg = REG_NA;
                regNumber lowReg;
                if (numArgs == 2)
                {
                    lowReg = targetReg;
                }
                else
                {
                    op3Reg = node.GetOp(3).RegNum;
                    assert(!node.GetOp(3).IsContained);
                    assert(op3Reg != op1Reg);
                    assert(op3Reg != targetReg);
                    assert(op3Reg != REG_RDX);
                    lowReg = _internalRegisters.GetSingle(node);
                    assert(op3Reg != lowReg);
                    assert(lowReg != targetReg);
                }

                assert(!op2.IsContained);
                attr = targetType.EmitSize;
                assert((op2Reg != REG_RDX) || (op1Reg == REG_RDX));
                _ = Emitter.emitIns_Mov(INS_mov, attr, REG_RDX, op1Reg, canSkip: true);
                assert(!node.IsRmwHWIntrinsic(_compiler));
                inst_RV_RV_TT(ins, attr, targetReg, lowReg, op2, false, INS_OPTS_NONE);
                if (numArgs == 3)
                {
                    Emitter.emitIns_AR_R(INS_mov, attr, lowReg, op3Reg, 0);
                }
                break;
            }

            case NI_AVX512_AddMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kaddb;
                }
                else if (count == 16)
                {
                    ins = INS_kaddw;
                }
                else if (count == 32)
                {
                    ins = INS_kaddd;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kaddq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = node.GetOp(2).RegNum;
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                // EA_32BYTE sets VEX.L for three-register mask instructions.
                Emitter.emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
                break;
            }

            case NI_AVX512_AndMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kandb;
                }
                else if (count == 16)
                {
                    ins = INS_kandw;
                }
                else if (count == 32)
                {
                    ins = INS_kandd;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kandq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = node.GetOp(2).RegNum;
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
                break;
            }

            case NI_AVX512_AndNotMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kandnb;
                }
                else if (count == 16)
                {
                    ins = INS_kandnw;
                }
                else if (count == 32)
                {
                    ins = INS_kandnd;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kandnq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = node.GetOp(2).RegNum;
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
                break;
            }

            case NI_AVX512_MoveMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kmovb_gpr;
                    attr = EA_4BYTE;
                }
                else if (count == 16)
                {
                    ins = INS_kmovw_gpr;
                    attr = EA_4BYTE;
                }
                else if (count == 32)
                {
                    ins = INS_kmovd_gpr;
                    attr = EA_4BYTE;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kmovq_gpr;
                    attr = EA_8BYTE;
                }
                var op1Reg = op1.RegNum;
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                _ = Emitter.emitIns_Mov(ins, attr, targetReg, op1Reg, canSkip: false);
                break;
            }

            case NI_AVX512_KORTEST:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kortestb;
                }
                else if (count == 16)
                {
                    ins = INS_kortestw;
                }
                else if (count == 32)
                {
                    ins = INS_kortestd;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kortestq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = op1Reg;
                if (numArgs == 2)
                {
                    op2Reg = node.GetOp(2).RegNum;
                }
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R(ins, EA_8BYTE, op1Reg, op1Reg);
                break;
            }

            case NI_AVX512_KTEST:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_ktestb;
                }
                else if (count == 16)
                {
                    ins = INS_ktestw;
                }
                else if (count == 32)
                {
                    ins = INS_ktestd;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_ktestq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = node.GetOp(2).RegNum;
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R(ins, EA_8BYTE, op1Reg, op1Reg);
                break;
            }

            case NI_AVX512_NotMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_knotb;
                }
                else if (count == 16)
                {
                    ins = INS_knotw;
                }
                else if (count == 32)
                {
                    ins = INS_knotd;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_knotq;
                }
                var op1Reg = op1.RegNum;
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R(ins, EA_8BYTE, targetReg, op1Reg);
                if (count < 8)
                {
                    // No two- or four-bit KNOT exists. Clear high bits before consumers such as KMOVB+POPCNT.
                    ClearUnusedMaskBits(targetReg, count);
                }
                break;
            }

            case NI_AVX512_OrMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_korb;
                }
                else if (count == 16)
                {
                    ins = INS_korw;
                }
                else if (count == 32)
                {
                    ins = INS_kord;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_korq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = node.GetOp(2).RegNum;
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
                break;
            }

            case NI_AVX512_ShiftLeftMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kshiftlb;
                }
                else if (count == 16)
                {
                    ins = INS_kshiftlw;
                }
                else if (count == 32)
                {
                    ins = INS_kshiftld;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kshiftlq;
                }
                var op1Reg = op1.RegNum;
                var op2 = node.GetOp(2);
                assert(op2.Oper.IsCnsIntOrI && op2.IsContained);
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                var ival = op2.AsIntCon().IconValue;
                assert((ival >= 0) && (ival <= 255));
                Emitter.emitIns_R_R_I(ins, EA_8BYTE, targetReg, op1Reg, unchecked((sbyte)ival));
                break;
            }

            case NI_AVX512_ShiftRightMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kshiftrb;
                }
                else if (count == 16)
                {
                    ins = INS_kshiftrw;
                }
                else if (count == 32)
                {
                    ins = INS_kshiftrd;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kshiftrq;
                }
                var op1Reg = op1.RegNum;
                var op2 = node.GetOp(2);
                assert(op2.Oper.IsCnsIntOrI && op2.IsContained);
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                var ival = op2.AsIntCon().IconValue;
                assert((ival >= 0) && (ival <= 255));
                Emitter.emitIns_R_R_I(ins, EA_8BYTE, targetReg, op1Reg, unchecked((sbyte)ival));
                break;
            }

            case NI_AVX512_XorMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kxorb;
                }
                else if (count == 16)
                {
                    ins = INS_kxorw;
                }
                else if (count == 32)
                {
                    ins = INS_kxord;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kxorq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = node.GetOp(2).RegNum;
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
                break;
            }

            case NI_AVX512_XnorMask:
            {
                assert(instOptions == INS_OPTS_NONE);
                var count = (uint)(node.SimdSize / baseType.Size);
                if (count <= 8)
                {
                    assert(count is 2 or 4 or 8);
                    ins = INS_kxnorb;
                }
                else if (count == 16)
                {
                    ins = INS_kxnorw;
                }
                else if (count == 32)
                {
                    ins = INS_kxnord;
                }
                else
                {
                    assert(count == 64);
                    ins = INS_kxnorq;
                }
                var op1Reg = op1.RegNum;
                var op2Reg = node.GetOp(2).RegNum;
                assert(targetReg is >= REG_K0 and <= REG_K7);
                assert(op1Reg is >= REG_K0 and <= REG_K7);
                assert(op2Reg is >= REG_K0 and <= REG_K7);
                Emitter.emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
                if (count < 8)
                {
                    ClearUnusedMaskBits(targetReg, count);
                }
                break;
            }

            case NI_AVX512_ConvertToInt32:
            case NI_AVX512_ConvertToUInt32:
            case NI_AVX512_ConvertToUInt32WithTruncation:
            case NI_AVX512_X64_ConvertToInt64:
            case NI_AVX512_X64_ConvertToUInt64:
            case NI_AVX512_X64_ConvertToUInt64WithTruncation:
            case NI_AVX10v2_ConvertToInt32WithTruncatedSaturation:
            case NI_AVX10v2_ConvertToUInt32WithTruncatedSaturation:
            case NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation:
            case NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation:
            {
                assert(baseType is TYP_DOUBLE or TYP_FLOAT);
                attr = targetType.EmitSize;
                ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                break;
            }

            case NI_AVX512_ConvertToVector128UInt32:
            case NI_AVX512_ConvertToVector256Int32:
            case NI_AVX512_ConvertToVector256UInt32:
            {
                if (varTypeIsFloating(baseType))
                {
                    ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                    genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                    break;
                }
                goto case NI_AVX512_ConvertToVector128Byte;
            }

            case NI_AVX512_ConvertToVector128Byte:
            case NI_AVX512_ConvertToVector128ByteWithSaturation:
            case NI_AVX512_ConvertToVector128Int16:
            case NI_AVX512_ConvertToVector128Int16WithSaturation:
            case NI_AVX512_ConvertToVector128Int32:
            case NI_AVX512_ConvertToVector128Int32WithSaturation:
            case NI_AVX512_ConvertToVector128SByte:
            case NI_AVX512_ConvertToVector128SByteWithSaturation:
            case NI_AVX512_ConvertToVector128UInt16:
            case NI_AVX512_ConvertToVector128UInt16WithSaturation:
            case NI_AVX512_ConvertToVector128UInt32WithSaturation:
            case NI_AVX512_ConvertToVector256Byte:
            case NI_AVX512_ConvertToVector256ByteWithSaturation:
            case NI_AVX512_ConvertToVector256Int16:
            case NI_AVX512_ConvertToVector256Int16WithSaturation:
            case NI_AVX512_ConvertToVector256Int32WithSaturation:
            case NI_AVX512_ConvertToVector256SByte:
            case NI_AVX512_ConvertToVector256SByteWithSaturation:
            case NI_AVX512_ConvertToVector256UInt16:
            case NI_AVX512_ConvertToVector256UInt16WithSaturation:
            case NI_AVX512_ConvertToVector256UInt32WithSaturation:
            {
                ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                // Narrowing instructions are RM_R: the destination occupies the RM field.
                Emitter.emitIns_R_R(ins, attr, op1.RegNum, targetReg, instOptions);
                break;
            }

            case NI_AVX512_X64_ConvertScalarToVector128Double:
            case NI_AVX512_X64_ConvertScalarToVector128Single:
            {
                assert(baseType is TYP_ULONG or TYP_LONG);
                ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                genHWIntrinsic_R_R_RM(node, ins, EA_8BYTE, instOptions);
                break;
            }

            case NI_AVX10v1_ConvertScalarToVector128Half:
            {
                // Integral sources use their GPR width; floating sources use the full XMM width.
                if (varTypeIsIntegral(baseType))
                {
                    attr = baseType.EmitActualSize;
                }
                genHWIntrinsic_R_R_RM(node, ins, attr, instOptions);
                break;
            }

            case NI_AVXVNNIINT_MultiplyWideningAndAddSaturate:
            case NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate:
            {
                var op2 = node.GetOp(2);
                var op3 = node.GetOp(3);
                var op1Reg = op1.RegNum;
                var op2Reg = op2.RegNum;
                assert(targetReg != REG_NA);
                assert(op1Reg != REG_NA);
                assert(op2Reg != REG_NA);
                var op3Type = node.AuxiliaryType;
                switch (baseType)
                {
                    case TYP_UBYTE:
                    {
                        ins = INS_vpdpbuuds;
                        break;
                    }
                    case TYP_BYTE:
                    {
                        switch (op3Type)
                        {
                            case TYP_UBYTE:
                            {
                                ins = INS_vpdpbsuds;
                                break;
                            }
                            case TYP_BYTE:
                            {
                                ins = INS_vpdpbssds;
                                break;
                            }
                            default:
                            {
                                unreached();
                                break;
                            }
                        }
                        break;
                    }
                    case TYP_SHORT:
                    {
                        ins = INS_vpdpwsuds;
                        break;
                    }
                    case TYP_USHORT:
                    {
                        switch (op3Type)
                        {
                            case TYP_USHORT:
                            {
                                ins = INS_vpdpwuuds;
                                break;
                            }
                            case TYP_SHORT:
                            {
                                ins = INS_vpdpwusds;
                                break;
                            }
                            default:
                            {
                                unreached();
                                break;
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
                genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, op1Reg, op2Reg, op3, instOptions);
                break;
            }

            case NI_AVXVNNIINT_MultiplyWideningAndAdd:
            case NI_AVXVNNIINT_V512_MultiplyWideningAndAdd:
            {
                var op2 = node.GetOp(2);
                var op3 = node.GetOp(3);
                var op1Reg = op1.RegNum;
                var op2Reg = op2.RegNum;
                assert(targetReg != REG_NA);
                assert(op1Reg != REG_NA);
                assert(op2Reg != REG_NA);
                var op3Type = node.AuxiliaryType;
                switch (baseType)
                {
                    case TYP_UBYTE:
                    {
                        ins = INS_vpdpbuud;
                        break;
                    }
                    case TYP_BYTE:
                    {
                        switch (op3Type)
                        {
                            case TYP_UBYTE:
                            {
                                ins = INS_vpdpbsud;
                                break;
                            }
                            case TYP_BYTE:
                            {
                                ins = INS_vpdpbssd;
                                break;
                            }
                            default:
                            {
                                unreached();
                                break;
                            }
                        }
                        break;
                    }
                    case TYP_SHORT:
                    {
                        ins = INS_vpdpwsud;
                        break;
                    }
                    case TYP_USHORT:
                    {
                        switch (op3Type)
                        {
                            case TYP_USHORT:
                            {
                                ins = INS_vpdpwuud;
                                break;
                            }
                            case TYP_SHORT:
                            {
                                ins = INS_vpdpwusd;
                                break;
                            }
                            default:
                            {
                                unreached();
                                break;
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
                genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, op1Reg, op2Reg, op3, instOptions);
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
#endif
}
