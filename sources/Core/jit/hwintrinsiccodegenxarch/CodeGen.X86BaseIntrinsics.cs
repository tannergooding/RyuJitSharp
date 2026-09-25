// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genX86BaseIntrinsic(GenTreeHWIntrinsic node, insOpts instOptions)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "X86 base hardware intrinsic generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var intrinsicId = node.HWIntrinsicId;
        var targetReg = node.RegNum;
        var targetType = node.Type;
        var baseType = node.SimdBaseType;
        var emit = Emitter;

        genConsumeMultiOpOperands(node);

        switch (intrinsicId)
        {
            case NI_X86Base_X64_BigMul:
            {
                assert(node.Operands.Length == 2);
                assert(instOptions == INS_OPTS_NONE);
                assert(!node.GetOp(1).IsContained);
                var regOp = node.GetOp(1);
                var rmOp = node.GetOp(2);
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                var attr = baseType.EmitSize;

                // Reuse EAX as the implicit multiplicand when it already holds op2.
                if (rmOp.IsUsedFromReg && (rmOp.RegNum == REG_EAX))
                {
                    (rmOp, regOp) = (regOp, rmOp);
                }
                _ = emit.emitIns_Mov(INS_mov, attr, REG_EAX, regOp.RegNum, canSkip: true);
                _ = emit.emitInsBinary(ins, attr, node, rmOp);

                assert(node.GetRegByIndex(0) == REG_EAX);
                assert(node.GetRegByIndex(1) == REG_EDX);
                break;
            }

            case NI_X86Base_BitScanForward:
            case NI_X86Base_BitScanReverse:
            case NI_X86Base_X64_BitScanForward:
            case NI_X86Base_X64_BitScanReverse:
            {
                var op1 = node.GetOp(1);
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, targetType, _compiler);
                genHWIntrinsic_R_RM(node, ins, targetType.EmitSize, targetReg, op1, instOptions);
                break;
            }

            case NI_X86Base_Pause:
            {
                assert(node.SimdBaseType == TYP_UNKNOWN);
                emit.emitIns(INS_pause);
                break;
            }

            case NI_X86Base_DivRem:
            case NI_X86Base_X64_DivRem:
            {
                assert(node.Operands.Length == 3);
                assert(instOptions == INS_OPTS_NONE);
                targetType = node.SimdBaseType;
                var op1 = node.GetOp(1);
                var op2 = node.GetOp(2);
                var op3 = node.GetOp(3);
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, targetType, _compiler);
                var op1Reg = op1.RegNum;
                var op2Reg = op2.RegNum;
                var op3Reg = op3.RegNum;
                var attr = targetType.EmitSize;

                assert(op1Reg != REG_EDX);
                assert(op2Reg != REG_EAX);
                if (op3.IsUsedFromReg)
                {
                    assert(op3Reg != REG_EDX);
                    assert(op3Reg != REG_EAX);
                }

                _ = emit.emitIns_Mov(INS_mov, attr, REG_EAX, op1Reg, canSkip: true);
                _ = emit.emitIns_Mov(INS_mov, attr, REG_EDX, op2Reg, canSkip: true);
                _ = emit.emitInsBinary(ins, attr, node, op3);

                assert(node.GetRegNumByIdx(0) == REG_EAX);
                assert(node.GetRegNumByIdx(1) == REG_EDX);
                break;
            }

            case NI_X86Base_X64_ConvertScalarToVector128Double:
            case NI_X86Base_X64_ConvertScalarToVector128Single:
            {
                assert(baseType is TYP_LONG or TYP_ULONG);
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                genHWIntrinsic_R_R_RM(node, ins, EA_8BYTE, instOptions);
                break;
            }

            case NI_X86Base_Prefetch0:
            case NI_X86Base_Prefetch1:
            case NI_X86Base_Prefetch2:
            case NI_X86Base_PrefetchNonTemporal:
            {
                assert(baseType == TYP_UBYTE);
                assert(instOptions == INS_OPTS_NONE);
                assert(!node.GetOp(1).IsContained);
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, node.SimdBaseType, _compiler);
                emit.emitIns_AR(ins, baseType.EmitSize, node.GetOp(1).RegNum, 0);
                break;
            }

            case NI_X86Base_StoreFence:
            {
                assert(baseType == TYP_UNKNOWN);
                emit.emitIns(INS_sfence);
                break;
            }

            case NI_X86Base_X64_ConvertScalarToVector128Int64:
            case NI_X86Base_X64_ConvertScalarToVector128UInt64:
            case NI_X86Base_ConvertToInt32:
            case NI_X86Base_ConvertToInt32WithTruncation:
            case NI_X86Base_ConvertToUInt32:
            case NI_X86Base_X64_ConvertToInt64:
            case NI_X86Base_X64_ConvertToInt64WithTruncation:
            case NI_X86Base_X64_ConvertToUInt64:
            {
                emitAttr attr;
                if (varTypeIsIntegral(baseType))
                {
                    assert(baseType is TYP_INT or TYP_UINT or TYP_LONG or TYP_ULONG);
                    attr = baseType.ActualType.EmitSize;
                }
                else
                {
                    assert(baseType is TYP_DOUBLE or TYP_FLOAT);
                    attr = targetType.EmitSize;
                }

                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, node.GetOp(1), instOptions);
                break;
            }

            case NI_X86Base_LoadFence:
            {
                assert(baseType == TYP_UNKNOWN);
                emit.emitIns(INS_lfence);
                break;
            }

            case NI_X86Base_MemoryFence:
            {
                assert(baseType == TYP_UNKNOWN);
                emit.emitIns(INS_mfence);
                break;
            }

            case NI_X86Base_StoreNonTemporal:
            case NI_X86Base_X64_StoreNonTemporal:
            {
                assert(baseType is TYP_INT or TYP_UINT or TYP_LONG or TYP_ULONG);
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                var store = storeIndirForm(node.Type, node.GetOp(1), node.GetOp(2));
                emit.emitInsStoreInd(ins, baseType.EmitSize, store);
                break;
            }

            case NI_X86Base_ConvertToVector128Int16:
            case NI_X86Base_ConvertToVector128Int32:
            case NI_X86Base_ConvertToVector128Int64:
            {
                var op1 = node.GetOp(1);
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                if (node.IsMemoryLoad())
                {
                    var load = indirForm(targetType, op1);
                    emit.emitInsLoadInd(ins, EA_16BYTE, targetReg, load);
                }
                else
                {
                    genHWIntrinsic_R_RM(node, ins, EA_16BYTE, targetReg, op1, instOptions);
                }
                break;
            }

            case NI_X86Base_Crc32:
            case NI_X86Base_X64_Crc32:
            {
                assert(instOptions == INS_OPTS_NONE);
                var ins = INS_crc32;
                var op1 = node.GetOp(1);
                var op1Reg = op1.RegNum;
                var op2 = node.GetOp(2);

                assert(!op2.IsUsedFromReg || (op2.RegNum != targetReg) || (op1Reg == targetReg) ||
                    genIsSameLocalVar(op1, op2));
                _ = emit.emitIns_Mov(INS_mov, targetType.EmitSize, targetReg, op1Reg, canSkip: true);

                var needsEvex = false;
                if (emit.IsExtendedGPReg(targetReg))
                {
                    needsEvex = true;
                }
                else if (op2.IsUsedFromReg && emit.IsExtendedGPReg(op2.RegNum))
                {
                    needsEvex = true;
                }
                else if (op2.Oper.IsIndir)
                {
                    var indir = op2.AsIndir();
                    if (indir.HasBase && emit.IsExtendedGPReg(indir.Base.RegNum))
                    {
                        needsEvex = true;
                    }
                    if (indir.HasIndex && emit.IsExtendedGPReg(indir.Index.RegNum))
                    {
                        needsEvex = true;
                    }
                }
                if (needsEvex)
                {
                    ins = INS_crc32_apx;
                }

                if (baseType is TYP_UBYTE or TYP_USHORT)
                {
                    assert(targetType == TYP_INT);
                    genHWIntrinsic_R_RM(node, ins, baseType.EmitSize, targetReg, op2, instOptions);
                }
                else
                {
                    assert(targetType is TYP_INT or TYP_LONG);
                    genHWIntrinsic_R_RM(node, ins, targetType.EmitSize, targetReg, op2, instOptions);
                }
                break;
            }

            case NI_X86Base_Extract:
            case NI_X86Base_X64_Extract:
            {
                var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                var op1 = node.GetOp(1);
                var op2 = node.GetOp(2);
                var attr = targetType.ActualType.EmitSize;

                void EmitSwCase(sbyte i)
                {
                    inst_RV_TT_IV(ins, attr, targetReg, op1, i, instOptions);
                }

                if (op2.Oper.IsCnsIntOrI)
                {
                    var ival = op2.AsIntCon().IconValue;
                    assert((ival >= 0) && (ival <= 255));
                    EmitSwCase(unchecked((sbyte)ival));
                }
                else
                {
                    // Nonconstant immediates use the common jump-table dispatch,
                    // including reflection and direct calls with variable indices.
                    var baseReg = _internalRegisters.Extract(node);
                    var offsReg = _internalRegisters.GetSingle(node);
                    genHWIntrinsicJumpTableFallback(intrinsicId, ins, EA_16BYTE, op2.RegNum,
                        baseReg, offsReg, EmitSwCase);
                }
                break;
            }

            case NI_X86Base_PopCount:
            case NI_X86Base_X64_PopCount:
            {
                genXCNTIntrinsic(node, INS_popcnt);
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
}
#endif
