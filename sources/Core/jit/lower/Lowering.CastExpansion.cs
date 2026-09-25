// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe void LowerCast(GenTreeCast tree)
    {
#if TARGET_AMD64
        assert(tree.Oper is GT_CAST);
        var compiler = CompilerInstance;
        var castOp = tree.CastOp;
        var dstType = tree.CastType;
        var srcType = castOp.Type.ActualType;
        if (tree.IsUnsigned)
        {
            srcType = varTypeToUnsigned(srcType);
        }
        if (varTypeIsFloating(srcType))
        {
            // Morph handles checked conversions and inserts the intermediate cast for small types.
            noway_assert(!tree.HasOverflowCheck);
            assert(!varTypeIsSmall(dstType));
        }

        if (varTypeIsFloating(srcType) && varTypeIsIntegral(dstType))
        {
            LABELEDDISPTREERANGE("LowerCast before", BlockRange(), tree);
            var castRange = new LIR.Range(null, null);
            GenTree castResult;
            if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX10v2))
            {
                var intrinsic = dstType switch {
                    TYP_INT => NI_AVX10v2_ConvertToInt32WithTruncatedSaturation,
                    TYP_UINT => NI_AVX10v2_ConvertToUInt32WithTruncatedSaturation,
                    TYP_LONG => NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation,
                    TYP_ULONG => NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation,
                    _ => throw new System.InvalidOperationException("Unexpected floating conversion destination."),
                };
                castResult = compiler.gtNewSimdHWIntrinsicNode(dstType.ActualType, intrinsic, srcType, 16, castOp);
                castRange.InsertAtEnd(castResult);
            }
            else
            {
                var srcVector = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD16, castOp, srcType, 16);
                castRange.InsertAtEnd(srcVector);
                if (srcVector.Oper is GT_CNS_VEC)
                {
                    castOp.IsUnusedValue = true;
                }

                if (varTypeIsUnsigned(dstType) && compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    // EVEX saturates positive overflow. MAX with zero as its second operand also fixes NaN.
                    var intrinsic = dstType is TYP_UINT
                        ? NI_AVX512_ConvertToUInt32WithTruncation
                        : NI_AVX512_X64_ConvertToUInt64WithTruncation;
                    var zero = compiler.gtNewZeroConNode(TYP_SIMD16);
                    var fixup = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_MaxScalar, srcType, 16,
                        srcVector, zero);
                    castResult = compiler.gtNewSimdHWIntrinsicNode(dstType.ActualType, intrinsic, srcType, 16, fixup);
                    castRange.InsertAtEnd(zero);
                    castRange.InsertAtEnd(fixup);
                    castRange.InsertAtEnd(castResult);
                }
                else
                {
                    NamedIntrinsic intrinsic;
                    GenTree maxIntegralValue;
                    double minFloatOverflow;
                    switch (dstType)
                    {
                        case TYP_INT:
                        {
                            intrinsic = NI_X86Base_ConvertToInt32WithTruncation;
                            maxIntegralValue = compiler.gtNewIconNode(TYP_INT, int.MaxValue);
                            minFloatOverflow = 2147483648.0; // 2^31
                            break;
                        }

                        case TYP_UINT:
                        {
                            intrinsic = NI_X86Base_X64_ConvertToInt64WithTruncation;
                            maxIntegralValue = compiler.gtNewIconNode(TYP_INT, unchecked((nint)uint.MaxValue));
                            minFloatOverflow = 4294967296.0; // 2^32
                            break;
                        }

                        case TYP_LONG:
                        {
                            intrinsic = NI_X86Base_X64_ConvertToInt64WithTruncation;
                            maxIntegralValue = compiler.gtNewLconNode(long.MaxValue);
                            minFloatOverflow = 9223372036854775808.0; // 2^63
                            break;
                        }

                        case TYP_ULONG:
                        {
                            intrinsic = NI_X86Base_X64_ConvertToInt64WithTruncation;
                            maxIntegralValue = compiler.gtNewLconNode(-1);
                            minFloatOverflow = 18446744073709551616.0; // 2^64
                            break;
                        }

                        default:
                        {
                            throw new System.InvalidOperationException("Unexpected floating conversion destination.");
                        }
                    }

                    var overflowVector = compiler.gtNewVconNode(TYP_SIMD16);
                    if (srcType is TYP_FLOAT)
                    {
                        overflowVector.SimdVal.f32[0] = (float)minFloatOverflow;
                    }
                    else
                    {
                        overflowVector.SimdVal.f64[0] = minFloatOverflow;
                    }
                    GenTree overflowValue = overflowVector;
                    LIR.Use.MakeDummyUse(castRange, srcVector, out var srcUse);
                    _ = srcUse.ReplaceWithLclVar(compiler);
                    srcVector = srcUse.Def();

                    GenTree convertResult;
                    if (varTypeIsSigned(dstType))
                    {
                        // Clear NaN before converting. Positive saturation must happen afterwards:
                        // the destination's MaxValue need not be exactly representable as a float.
                        var srcClone = CloneLocal(srcVector);
                        var nanMask = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_CompareScalarOrdered,
                            srcType, 16, srcVector, srcClone);
                        castRange.InsertAtEnd(srcClone);
                        castRange.InsertAtEnd(nanMask);
                        srcClone = CloneLocal(srcVector);
                        var fixup = compiler.gtNewSimdBinOpNode(GT_AND, TYP_SIMD16, nanMask, srcClone, srcType, 16);
                        castRange.InsertAtEnd(srcClone);
                        castRange.InsertAtEnd(fixup);
                        convertResult = compiler.gtNewSimdHWIntrinsicNode(dstType, intrinsic, srcType, 16, fixup);
                    }
                    else
                    {
                        var zero = compiler.gtNewZeroConNode(TYP_SIMD16);
                        var fixup = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_MaxScalar, srcType, 16,
                            srcVector, zero);
                        castRange.InsertAtEnd(zero);
                        castRange.InsertAtEnd(fixup);
                        if (dstType is TYP_UINT)
                        {
                            // X64's signed long conversion covers the entire uint range.
                            convertResult = compiler.gtNewSimdHWIntrinsicNode(TYP_LONG, intrinsic, srcType, 16, fixup);
                        }
                        else
                        {
                            assert(dstType is TYP_ULONG);
                            // For the upper half of ulong, convert x - 2^64 as a signed long.
                            // Double's precision guarantees whole numbers at that magnitude, so
                            // the positive and negative truncating conversions have identical low bits.
                            castRange.InsertAtEnd(overflowValue);
                            LIR.Use.MakeDummyUse(castRange, overflowValue, out var overflowUse);
                            _ = overflowUse.ReplaceWithLclVar(compiler);
                            overflowValue = overflowUse.Def();
                            var floor = CloneLocal(srcVector);
                            castRange.InsertAtEnd(floor);
                            var wrap = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_SubtractScalar,
                                srcType, 16, floor, overflowValue);
                            castRange.InsertAtEnd(wrap);
                            overflowValue = CloneLocal(overflowValue);

                            GenTree result = compiler.gtNewSimdHWIntrinsicNode(TYP_LONG, intrinsic, srcType, 16, fixup);
                            var negated = compiler.gtNewSimdHWIntrinsicNode(TYP_LONG, intrinsic, srcType, 16, wrap);
                            castRange.InsertAtEnd(result);
                            castRange.InsertAtEnd(negated);
                            LIR.Use.MakeDummyUse(castRange, result, out var resultUse);
                            _ = resultUse.ReplaceWithLclVar(compiler);
                            result = resultUse.Def();

                            // Signed conversion overflow returns MinValue. Its sign selects the wrapped result.
                            var sixtyThree = compiler.gtNewIconNode(TYP_INT, 63);
                            var mask = new GenTreeOp(GT_RSH, TYP_LONG, result, sixtyThree);
                            var andMask = new GenTreeOp(GT_AND, TYP_LONG, mask, negated);
                            var resultClone = CloneLocal(result);
                            castRange.InsertAtEnd(sixtyThree);
                            castRange.InsertAtEnd(mask);
                            castRange.InsertAtEnd(andMask);
                            castRange.InsertAtEnd(resultClone);
                            convertResult = new GenTreeOp(GT_OR, TYP_LONG, andMask, resultClone);
                        }
                    }

                    castRange.InsertAtEnd(overflowValue);
                    castRange.InsertAtEnd(maxIntegralValue);
                    castRange.InsertAtEnd(convertResult);
                    var compareSource = CloneLocal(srcVector);
                    var compareMax = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16,
                        NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual, srcType, 16, compareSource, overflowValue);
                    var select = new GenTreeConditional(GT_SELECT, dstType.ActualType, compareMax,
                        maxIntegralValue, convertResult);
                    select.Flags |= (compareMax.Flags | maxIntegralValue.Flags | convertResult.Flags) & GTF_ALL_EFFECT;
                    castResult = select;
                    castRange.InsertAtEnd(compareSource);
                    castRange.InsertAtEnd(compareMax);
                    castRange.InsertAtEnd(castResult);
                }
            }

            var first = castRange.FirstNode;
            var last = castRange.LastNode;
            BlockRange().InsertBefore(tree, castRange);
            LABELEDDISPTREERANGE("LowerCast after", BlockRange(), castResult);
            if (BlockRange().TryGetUse(tree, out var castUse))
            {
                castUse.ReplaceWith(castResult);
            }
            else
            {
                castResult.IsUnusedValue = true;
            }
            BlockRange().Remove(tree);
            LowerRange(first, last);
            return;
        }

        ContainCheckCast(tree);

        GenTree CloneLocal(GenTree node)
        {
            assert(node.Oper.IsLocalRead);
            var clone = compiler.gtClone(node);
            assert(clone is not null);
            return clone;
        }
#else
        throw new System.NotImplementedException("Non-AMD64 cast expansion is not ported.");
#endif
    }
}
