// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreInd(GenTreeStoreInd tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Indirect store generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_STOREIND);

#if FEATURE_SIMD
        if (tree.Type is TYP_SIMD12)
        {
            genStoreIndTypeSimd12(tree);
            return;
        }
#endif
        var data = tree.Data;
        var addr = tree.Addr;
        var targetType = tree.Type;
        assert(!varTypeIsFloating(targetType) || (targetType.Size == data.Type.Size));

        var writeBarrierForm = GCInfo.gcIsWriteBarrierCandidate(tree);
        if (writeBarrierForm != GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            // Consume both operands before assigning the fixed helper arguments,
            // so copies inserted for interfering registers have already run.
            genConsumeOperands(tree);
            if (genEmitOptimizedGCWriteBarrier(writeBarrierForm, addr, data))
            {
                return;
            }

            noway_assert(data.RegNum != REG_WRITE_BARRIER_DST);
            genCopyRegIfNeeded(addr, REG_WRITE_BARRIER_DST);
            genCopyRegIfNeeded(data, REG_WRITE_BARRIER_SRC);
            genGCWriteBarrier(writeBarrierForm);
        }
        else
        {
            var dataIsUnary = false;
            var isRMWMemoryOp = tree.IsRMWMemoryOp;
            GenTree? rmwSrc = null;

            genConsumeAddress(addr);
            if (isRMWMemoryOp)
            {
                assert(data.IsContained && !data.Oper.IsLeaf);
                GenTree? rmwDst;
                dataIsUnary = data.Oper.IsUnary;
                if (!dataIsUnary)
                {
                    var binary = data.AsOp();
                    if (tree.IsRMWDstOp1)
                    {
                        rmwDst = binary.Op1;
                        rmwSrc = binary.Op2;
                    }
                    else
                    {
                        assert(tree.IsRMWDstOp2);
                        rmwDst = binary.Op2;
                        rmwSrc = binary.Op1;
                    }

                    assert(rmwSrc is not null);
                    genConsumeRegs(rmwSrc);
                }
                else
                {
                    // Lowering cleared the contained read's operand counts;
                    // the address was consumed above, so do not consume it twice.
                    assert(tree.IsRMWDstOp1);
                    rmwSrc = data.AsUnOp().Op1;
                    rmwDst = rmwSrc;
                    assert(rmwSrc.IsUsedFromMemory);
                }

                assert(rmwDst is not null);
                assert(Lowering.IndirsAreEquivalent(rmwDst, tree));
            }
            else
            {
                genConsumeRegs(data);
            }

            if (isRMWMemoryOp)
            {
                assert(rmwSrc is not null);
                if (dataIsUnary)
                {
                    Emitter.emitInsRMW(genGetInsForOper(data.Oper, data.Type), tree.Type.EmitSize, tree);
                }
                else if (data.Oper.IsShiftOrRotate)
                {
                    assert(tree.IsRMWDstOp1);
                    assert(rmwSrc == data.AsOp().Op2);
                    genCodeForShiftRMW(tree);
                }
                else if ((data.Oper is GT_ADD) && rmwSrc.IsContainedIntOrIImmed &&
                    (rmwSrc.IsIntegralConst(1) || rmwSrc.IsIntegralConst(-1)))
                {
                    // Morph changes SUB(x, +/-1) to ADD(x, -/+1).
                    // Native TODO-AMD64: NativeWalker::Decode may assert on
                    // inc/dec memory operands; the pinned condition does not
                    // yet exclude debuggable code.
                    var ins = rmwSrc.IsIntegralConst(1) ? INS_inc : INS_dec;
                    Emitter.emitInsRMW(ins, tree.Type.EmitSize, tree);
                }
                else
                {
                    Emitter.emitInsRMW(genGetInsForOper(data.Oper, data.Type), tree.Type.EmitSize, tree, rmwSrc);
                }
            }
            else
            {
                var ins = INS_invalid;
                var attr = tree.Type.EmitSize;
                if (data.IsContained)
                {
                    if (data.Oper is GT_BSWAP or GT_BSWAP16)
                    {
                        var needsEvex = Emitter.IsExtendedGPReg(data.AsUnOp().Op1.RegNum) ||
                            (tree.HasBase && Emitter.IsExtendedGPReg(tree.Base.RegNum)) ||
                            (tree.HasIndex && Emitter.IsExtendedGPReg(tree.Index.RegNum));
                        ins = needsEvex ? INS_movbe_apx : INS_movbe;
                    }
#if FEATURE_HW_INTRINSICS
                    else if (data.Oper is GT_HWINTRINSIC)
                    {
                        var intrinsic = data.AsHWIntrinsic();
                        var intrinsicId = intrinsic.HWIntrinsicId;
                        var baseType = intrinsic.SimdBaseType;
                        var simdSize = intrinsic.SimdSize;

                        switch (intrinsicId)
                        {
                            case NI_Vector_ToScalar:
                            case NI_X86Base_ConvertToInt32:
                            case NI_X86Base_ConvertToUInt32:
                            case NI_X86Base_X64_ConvertToInt64:
                            case NI_X86Base_X64_ConvertToUInt64:
                            case NI_AVX2_ConvertToInt32:
                            case NI_AVX2_ConvertToUInt32:
                            {
                                ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                                attr = baseType.ActualType.EmitSize;
                                break;
                            }

                            case NI_Vector_GetElement:
                            {
                                assert(baseType is TYP_FLOAT);
                                assert(simdSize == 16);
                                goto case NI_X86Base_Extract;
                            }

                            case NI_X86Base_Extract:
                            case NI_X86Base_X64_Extract:
                            case NI_AVX_ExtractVector128:
                            case NI_AVX2_ConvertToVector128Half:
                            case NI_AVX2_ConvertToVector256Half:
                            case NI_AVX2_ExtractVector128:
                            case NI_AVX512_ExtractVector128:
                            case NI_AVX512_ExtractVector256:
                            {
                                ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
                                attr = Compiler.GetSimdTypeForSize(simdSize).ActualType.EmitSize;
                                ins = ins switch
                                {
                                    INS_vextractf64x2 => INS_vextractf32x4,
                                    INS_vextracti64x2 => INS_vextracti32x4,
                                    _ => ins,
                                };

                                // Intrinsic imm8 values are unsigned, but the emitter
                                // requires a signed-byte value sign-extended to pointer size.
                                var op2 = intrinsic.GetOp(2).AsIntCon();
                                var value = op2.IconValue;
                                assert((value >= 0) && (value <= 255));
                                op2.IconValue = unchecked((sbyte)value);
                                break;
                            }

                            case NI_AVX512_ConvertToVector128UInt32:
                            case NI_AVX512_ConvertToVector128UInt32WithSaturation:
                            case NI_AVX512_ConvertToVector256Int32:
                            case NI_AVX512_ConvertToVector256UInt32:
                            {
                                assert(!varTypeIsFloating(baseType));
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
                                attr = Compiler.GetSimdTypeForSize(simdSize).ActualType.EmitSize;
                                break;
                            }

                            default:
                            {
                                unreached();
                                break;
                            }
                        }
                    }
#endif
                }

                if (ins == INS_invalid)
                {
                    ins = ins_Store(data.Type);
                }
                Emitter.emitInsStoreInd(ins, attr, tree);
            }
        }
#endif
    }

    public bool genEmitOptimizedGCWriteBarrier(GCInfo.WriteBarrierForm writeBarrierForm, GenTree addr, GenTree data)
    {
        assert(writeBarrierForm != GCInfo.WriteBarrierForm.WBF_NoBarrier);
#if TARGET_X86 && NOGC_WRITE_BARRIERS
        throw new FatalJitException(CORJIT_SKIPPED, "Register-specific x86 write-barrier generation is not implemented.");
#else
        return false;
#endif
    }
}
#endif
