// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genHWIntrinsicJumpTableFallback(NamedIntrinsic intrinsic, instruction ins, emitAttr attr,
        regNumber nonConstImmReg, regNumber baseReg, regNumber offsReg, Action<sbyte> emitSwCase)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert(nonConstImmReg != REG_NA);
        assert(!HWIntrinsicInfo.isAVX2GatherIntrinsic(intrinsic));
        var maxByte = (uint)HWIntrinsicInfo.lookupImmUpperBound(intrinsic);
        uint mask = 0xFF;
        if (HWIntrinsicInfo.HasFullRangeImm(intrinsic))
        {
            maxByte = GetImmediateMaxAndMask(ins, EA_SIZE_IN_BYTES(attr), out mask);
            if (mask != 0xFF)
            {
                Emitter.emitIns_R_I(INS_and, EA_4BYTE, nonConstImmReg, (nint)mask);
            }
            else if (maxByte < 255)
            {
                Emitter.emitIns_R_I(INS_cmp, EA_4BYTE, nonConstImmReg, (nint)maxByte);
                var skipLabel = genCreateTempLabel();
                inst_JMP(EJ_jbe, skipLabel);
                instGen_Set_Reg_To_Imm(EA_4BYTE, nonConstImmReg, (nint)maxByte);
                genDefineTempLabel(skipLabel);
            }
        }

        // Import already emitted the range check. Entries retain their original
        // indices even when masking makes some values unreachable.
        assert(maxByte <= 255);
        var labels = new BasicBlock[maxByte + 1];
        var tableBase = Emitter.emitBBTableDataGenBeg(maxByte + 1, true);
        for (uint index = 0; index <= maxByte; index++)
        {
            labels[index] = genCreateTempLabel();
            Emitter.emitDataGenData(index, labels[index]);
        }
        Emitter.emitDataGenEnd();

        assert(_compiler.fgFirstBB is not null);
        Emitter.emitIns_R_C(INS_lea, TYP_I_IMPL.EmitSize, offsReg, Compiler.eeFindJitDataOffs(tableBase), 0);
        Emitter.emitIns_R_ARX(INS_mov, EA_4BYTE, offsReg, offsReg, nonConstImmReg, 4, 0);
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE | EA_DSP_RELOC_FLG, _compiler.fgFirstBB, baseReg);
        Emitter.emitIns_R_R(INS_add, EA_PTRSIZE, offsReg, baseReg);
        Emitter.emitIns_R(INS_i_jmp, TYP_I_IMPL.EmitSize, offsReg);

        var tableBegin = genCreateTempLabel();
        var tableEnd = genCreateTempLabel();
        genDefineTempLabel(tableBegin);
        for (uint index = 0; index <= maxByte; index++)
        {
            genDefineTempLabel(labels[index]);
            if ((index & mask) != index)
            {
                continue;
            }
            emitSwCase(unchecked((sbyte)index));
            Emitter.emitIns_J(INS_jmp, tableEnd);
        }
        genDefineTempLabel(tableEnd);
    }

    public void genNonTableDrivenHWIntrinsicsJumpTableFallback(GenTreeHWIntrinsic node, GenTree lastOp)
    {
        Emitter.RequireSupportedInstructionRecording();
        var intrinsic = node.HWIntrinsicId;
        var category = HWIntrinsicInfo.lookupCategory(intrinsic);
        assert((HWIntrinsicInfo.lookupFlags(intrinsic) & HW_Flag_EmbRoundingCompatible) != 0);
        assert(!lastOp.IsContained);
        assert(!HWIntrinsicInfo.genIsTableDrivenHWIntrinsic(intrinsic, category));
        var baseType = node.SimdBaseType;
        var attr = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
        var ins = HWIntrinsicInfo.lookupIns(intrinsic, baseType, _compiler);
        var targetReg = node.RegNum;
        switch (intrinsic)
        {
            case NI_AVX512_ConvertToVector256Int32:
            case NI_AVX512_ConvertToVector256UInt32:
            {
                assert(varTypeIsFloating(baseType));
                var operand = node.GetOp(1);
                var baseReg = _internalRegisters.Extract(node);
                var offsReg = _internalRegisters.GetSingle(node);
                genHWIntrinsicJumpTableFallback(intrinsic, ins, attr, lastOp.RegNum, baseReg, offsReg,
                    immediate => genHWIntrinsic_R_RM(node, ins, attr, targetReg, operand,
                        AddEmbRoundingMode(INS_OPTS_NONE, immediate)));
                break;
            }

            case NI_AVX512_ConvertToInt32:
            case NI_AVX512_ConvertToUInt32:
#if TARGET_AMD64
            case NI_AVX512_X64_ConvertToInt64:
            case NI_AVX512_X64_ConvertToUInt64:
#endif
            {
                assert(varTypeIsFloating(baseType));
                attr = node.Type.EmitSize;
                var operand = node.GetOp(1);
                var baseReg = _internalRegisters.Extract(node);
                var offsReg = _internalRegisters.GetSingle(node);
                genHWIntrinsicJumpTableFallback(intrinsic, ins, attr, lastOp.RegNum, baseReg, offsReg,
                    immediate => genHWIntrinsic_R_RM(node, ins, attr, targetReg, operand,
                        AddEmbRoundingMode(INS_OPTS_NONE, immediate)));
                break;
            }

            case NI_AVX512_X64_ConvertScalarToVector128Single:
            case NI_AVX512_X64_ConvertScalarToVector128Double:
            {
                assert(varTypeIsLong(baseType));
                var baseReg = _internalRegisters.Extract(node);
                var offsReg = _internalRegisters.GetSingle(node);
                genHWIntrinsicJumpTableFallback(intrinsic, ins, attr, lastOp.RegNum, baseReg, offsReg,
                    immediate => genHWIntrinsic_R_R_RM(node, ins, EA_8BYTE,
                        AddEmbRoundingMode(INS_OPTS_NONE, immediate)));
                break;
            }

            case NI_AVX512_FusedMultiplyAdd:
            case NI_AVX512_FusedMultiplyAddScalar:
            case NI_AVX10v1_FusedMultiplyAddScalar:
            case NI_AVX512_FusedMultiplyAddNegated:
            case NI_AVX512_FusedMultiplyAddNegatedScalar:
            case NI_AVX512_FusedMultiplyAddSubtract:
            case NI_AVX512_FusedMultiplySubtract:
            case NI_AVX512_FusedMultiplySubtractAdd:
            case NI_AVX512_FusedMultiplySubtractNegated:
            case NI_AVX512_FusedMultiplySubtractNegatedScalar:
            case NI_AVX512_FusedMultiplySubtractScalar:
            {
                assert(HWIntrinsicInfo.IsFmaIntrinsic(intrinsic));
                assert(!node.GetOp(1).IsContained);
                assert(!node.GetOp(2).IsContained);
                assert(!node.GetOp(3).IsContained);
                var baseReg = _internalRegisters.Extract(node);
                var offsReg = _internalRegisters.GetSingle(node);
                // FMA must choose 132/213/231 using the actual source/destination aliases.
                genHWIntrinsicJumpTableFallback(intrinsic, ins, attr, lastOp.RegNum, baseReg, offsReg,
                    immediate => genFmaIntrinsic(node, AddEmbRoundingMode(INS_OPTS_NONE, immediate)));
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
