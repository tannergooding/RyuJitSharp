// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe GenTree? LowerStoreIndirCommon(GenTreeStoreInd ind)
    {
        var compiler = CompilerInstance;
        assert(ind.Type is not TYP_STRUCT);
        ind = TryRetypingFloatingPointStoreToIntegerStore(ind).AsStoreInd();
#if TARGET_ARM64
        // Folding the ADD removes the intermediate byref and its ordering hint.
        var isContainable = IsInvariantInRange(ind.Addr, ind, GTF_ORDER_SIDEEFF);
#else
        var isContainable = true;
#endif
        _ = TryCreateAddrMode(ref ind.AddrRef, isContainable, ind);

        assert(compiler.codeGen is not null);
        if (compiler.codeGen.GCInfo.gcIsWriteBarrierStoreIndNode(ind))
        {
#if TARGET_WASM
            return LowerStoreIndir(ind);
#else
            return ind.Next;
#endif
        }

#if !TARGET_XARCH
        if (ind.Data.Oper.IsCnsIntOrI && ind.Data.AsIntCon().IsIconHandle(GTF_ICON_OBJ_HDL) &&
            !compiler.info.compCompHnd->isObjectImmutable((CORINFO_OBJECT_HANDLE)ind.Data.AsIntCon().IconValue))
        {
            // Publishing a potentially mutable object requires release semantics.
            ind.Flags |= GTF_IND_VOLATILE;
        }
#endif
        LowerIndirectStoreCoalescing(ind);
        return LowerStoreIndir(ind);
    }

    private GenTree? LowerStoreIndir(GenTreeStoreInd node)
    {
#if TARGET_XARCH
        node.RmwStatus = STOREIND_RMW_STATUS_UNKNOWN;
        if (!varTypeIsFloating(node.Type) && LowerRMWMemOp(node))
        {
            return node.Next;
        }

        if (varTypeIsByte(node.Type) && (node.Data.Oper.IsCompare || (node.Data.Oper is GT_SETCC)))
        {
            node.Data.Type = TYP_BYTE;
        }
        ContainCheckStoreIndir(node);

        return node.Next;
#elif TARGET_ARM64
        var next = node.Next;
        ContainCheckStoreIndir(node);
        if (CompilerInstance.opts.OptimizationEnabled)
        {
            _ = OptimizeForLdpStp(node);
        }

        return next;
#elif TARGET_WASM
        if (((node.Flags & GTF_IND_NONFAULTING) == 0) ||
            ((node.Type is TYP_SIMD12) && (node.Addr.Oper is not GT_LCL_ADDR)))
        {
            // Keep the address available for null checks and SIMD12's trailing lane store.
            var foldable = GetFoldableAddrMode(node);
            var addressToReuse = node.Addr;
            if (foldable is not null)
            {
                addressToReuse = foldable.BaseAddress ??
                    throw new InvalidOperationException("Foldable Wasm address is missing its base.");
            }

            SetMultiplyUsed(addressToReuse
#if DEBUG
                , "LowerStoreIndir Addr (null check or simd12 lane store)"
#endif
            );
        }

        ContainCheckStoreIndir(node);
        return node.Next;
#elif TARGET_RISCV64
        ContainCheckStoreIndir(node);
        return node.Next;
#elif TARGET_LOONGARCH64
        var next = node.Next;
        ContainCheckStoreIndir(node);
        return next;
#else
        throw new NotImplementedException("Indirect-store lowering outside xarch and ARM64 is not ported.");
#endif
    }

    private void ContainCheckStoreIndir(GenTreeStoreInd node)
    {
#if TARGET_XARCH
        var compiler = CompilerInstance;
        var src = node.Data;
        // A register zero followed by a store is smaller for int-size and larger stores.
        if (IsContainableImmed(node, src) && (!src.IsIntegralConst(0) || varTypeIsSmall(node.Type)))
        {
            MakeSrcContained(node, src);
        }

        if (compiler.opts.Tier0OptimizationEnabled)
        {
            if ((src.Oper is GT_BSWAP or GT_BSWAP16) && compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
            {
                var swapSize = (src.Oper is GT_BSWAP16) ? 2 : src.Type.Size;
                if ((swapSize == node.Type.Size) && IsInvariantInRange(src, node))
                {
                    // Prefer the MOVBE store when the load was already contained.
                    src.AsUnOp().Op1.IsContained = false;
                    MakeSrcContained(node, src);
                }
            }
#if FEATURE_HW_INTRINSICS
            else if (src.Oper is GT_HWINTRINSIC)
            {
                var intrinsic = src.AsHWIntrinsic();
                var intrinsicId = intrinsic.HWIntrinsicId;
                var baseType = intrinsic.SimdBaseType;
                var simdSize = intrinsic.SimdSize;
                var isContainable = false;
                GenTree? clearContainedNode = null;

                switch (intrinsicId)
                {
                    case NI_Vector_ToScalar:
                    {
                        // Preserve contained loads for a pair of scalar moves, but prefer
                        // containing the store over making the input register optional.
                        var op1 = intrinsic.GetOp(1);
                        clearContainedNode = op1;
                        isContainable = !op1.IsContained;
                        if (isContainable && varTypeIsIntegral(baseType))
                        {
                            isContainable = baseType.Size == node.Type.Size;
                            if (isContainable && varTypeIsSmall(baseType))
                            {
                                var extractType = varTypeIsByte(node.Type) ? TYP_UBYTE : TYP_USHORT;
                                if (simdSize == 64)
                                {
                                    op1 = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_GetLower128,
                                        extractType, 64, op1);
                                    BlockRange().InsertBefore(intrinsic, op1);
                                    _ = LowerNode(op1);
                                }
                                else if (simdSize == 32)
                                {
                                    op1 = compiler.gtNewSimdGetLowerNode(TYP_SIMD16, op1, extractType, 32);
                                    BlockRange().InsertBefore(intrinsic, op1);
                                    _ = LowerNode(op1);
                                }

                                var zero = compiler.gtNewZeroConNode(TYP_INT);
                                BlockRange().InsertBefore(intrinsic, zero);
                                intrinsic.SimdBaseType = extractType;
                                intrinsic.SimdSize = 16;
                                intrinsic.ResetHWIntrinsicId(NI_X86Base_Extract, op1, zero);
                                zero.IsContained = true;
                            }
                        }
                        break;
                    }

                    case NI_X86Base_ConvertToInt32:
                    case NI_X86Base_ConvertToUInt32:
                    case NI_X86Base_X64_ConvertToInt64:
                    case NI_X86Base_X64_ConvertToUInt64:
                    case NI_AVX2_ConvertToInt32:
                    case NI_AVX2_ConvertToUInt32:
                    {
                        isContainable = varTypeIsIntegral(baseType) && (src.Type.Size == node.Type.Size);
                        break;
                    }

                    case NI_Vector_GetElement:
                    {
                        if (simdSize != 16)
                        {
                            break;
                        }
                        if (varTypeIsFloating(baseType) && intrinsic.GetOp(2).Oper.IsCnsIntOrI)
                        {
                            assert(!intrinsic.GetOp(2).IsIntegralConst(0));
                            if (baseType is TYP_FLOAT)
                            {
                                // A contained scalar load/store pair is cheaper than loading
                                // the entire vector just to extract an element to memory.
                                clearContainedNode = intrinsic.GetOp(1);
                                isContainable = !clearContainedNode.IsContained;
                            }
                            else
                            {
                                // Double needs a separate StoreHigh transformation.
                                assert(!isContainable);
                            }
                        }
                        break;
                    }

                    case NI_X86Base_Extract:
                    case NI_X86Base_X64_Extract:
                    case NI_AVX_ExtractVector128:
                    case NI_AVX2_ExtractVector128:
                    case NI_AVX512_ExtractVector128:
                    case NI_AVX512_ExtractVector256:
                    {
                        var lastOp = intrinsic.GetOp(intrinsic.Operands.Length);
                        isContainable = HWIntrinsicInfo.isImmOp(intrinsicId, lastOp) && lastOp.Oper.IsCnsIntOrI &&
                            (baseType.Size == node.Type.Size);
                        break;
                    }

                    case NI_AVX512_ConvertToVector128UInt32:
                    case NI_AVX512_ConvertToVector128UInt32WithSaturation:
                    case NI_AVX512_ConvertToVector256Int32:
                    case NI_AVX512_ConvertToVector256UInt32:
                    {
                        if (varTypeIsFloating(baseType))
                        {
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
                        var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, compiler);
                        var tupleType = Emitter.insTupleTypeInfo(ins);
                        var memSize = tupleType switch {
                            INS_TT_HALF_MEM => simdSize / 2,
                            INS_TT_QUARTER_MEM => simdSize / 4,
                            INS_TT_EIGHTH_MEM => simdSize / 8,
                            _ => throw new InvalidOperationException("Unexpected tuple type for a narrowing conversion."),
                        };
                        isContainable = node.Type.Size == memSize;
                        break;
                    }

                    case NI_AVX2_ConvertToVector128Half:
                    case NI_AVX2_ConvertToVector256Half:
                    {
                        var lastOp = intrinsic.GetOp(intrinsic.Operands.Length);
                        isContainable = HWIntrinsicInfo.isImmOp(intrinsicId, lastOp) && lastOp.Oper.IsCnsIntOrI &&
                            (node.Type.Size == (simdSize / 2));
                        break;
                    }
                }

                if (isContainable && IsInvariantInRange(src, node))
                {
                    MakeSrcContained(node, src);
                    clearContainedNode?.IsContained = false;
                }
            }
#endif
        }

        ContainCheckIndir(node);
#elif TARGET_ARM64
        var src = node.Data;
        if (src.IsIntegralConst(0))
        {
            MakeSrcContained(node, src);
        }

        ContainCheckIndir(node);
#elif TARGET_RISCV64
        var src = node.Data;
        if (!varTypeIsFloating(src.Type) && src.IsIntegralConst(0))
        {
            MakeSrcContained(node, src);
        }

        ContainCheckIndir(node);
#elif TARGET_LOONGARCH64
        var source = node.Data;
        if (!varTypeIsFloating(source.Type) && source.IsIntegralConst(0))
        {
            MakeSrcContained(node, source);
        }

        ContainCheckIndir(node);
#elif TARGET_WASM
        ContainCheckIndir(node);
#else
        throw new NotImplementedException("Indirect-store containment outside xarch and ARM64 is not ported.");
#endif
    }
}
