// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
using System.Numerics;
using System.Runtime.Intrinsics.X86;

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_MASKED_HW_INTRINSICS
    public GenTree gtFoldExprConvertVecCnsToMask(GenTreeHWIntrinsic tree, GenTreeVecCon vecCon)
    {
        assert(tree.IsConvertVectorToMask);
        assert((vecCon == tree.GetOp(1)) || (vecCon == tree.GetOp(2)));
        assert(varTypeIsMask(tree.Type));

        var mskCon = gtNewMskConNode(default);
        switch (vecCon.Type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                EvaluateSimdCvtVectorToMask(tree.SimdBaseType, ref mskCon.SimdMaskVal,
                    vecCon.SimdVal.AsSpan<byte>()[..vecCon.Type.Size]);
                break;
            }

#if TARGET_ARM64
            case TYP_SIMD:
            {
                NYI("ARM64 scalable vector-to-mask constant folding");
                fatal(CORJIT_IMPLLIMITATION);
                return tree;
            }
#endif

            default:
            {
                unreached();
                break;
            }
        }

        return mskCon;
    }
#endif

#if TARGET_XARCH && FEATURE_MASKED_HW_INTRINSICS
    public GenTree gtFoldExprHWIntrinsic(GenTreeHWIntrinsic tree)
    {
        assert(!optValnumCSE_phase);
        assert(opts.Tier0OptimizationEnabled);

        var ni = tree.HWIntrinsicId;
        var retType = tree.Type;
        var simdBaseType = tree.SimdBaseType;
        var simdSize = tree.SimdSize;

        simd_t simdVal = default;

        if (GenTreeVecCon.IsHWIntrinsicCreateConstant(tree, ref simdVal))
        {
            var vecCon = gtNewVconNode(retType);

            foreach (var arg in tree.Operands)
            {
                DEBUG_DESTROY_NODE(arg);
            }

            vecCon.SimdVal = simdVal;
            vecCon.SetMorphed(this);
            fgUpdateConstTreeValueNumber(vecCon);
            return vecCon;
        }

        GenTree op1;
        GenTree? op2 = null;
        GenTree? op3 = null;
        var opCount = tree.Operands.Length;

        switch (opCount)
        {
            case 3:
            {
                op3 = tree.GetOp(3);
                goto case 2;
            }

            case 2:
            {
                op2 = tree.GetOp(2);
                goto case 1;
            }

            case 1:
            {
                op1 = tree.GetOp(1);
                break;
            }

            default:
            {
                return tree;
            }
        }

        // Fold ConvertMaskToVector(ConvertVectorToMask(vec)) to vec
        if (tree.IsConvertMaskToVector)
        {
            var op = op1;

            if (op.Oper.IsHWIntrinsic)
            {
                uint simdBaseTypeSize = simdBaseType.Size;
                var cvtOp = op.AsHWIntrinsic();

                if (cvtOp.IsConvertVectorToMask)
                {
                    if (cvtOp.SimdBaseType.Size == simdBaseTypeSize)
                    {
                        // We need the operand to be the same kind of mask; otherwise
                        // the bitwise operation can differ in how it performs

                        var vectorNode = cvtOp.GetOp(1);

                        DEBUG_DESTROY_NODE(op);
                        DEBUG_DESTROY_NODE(tree);
                        vectorNode.SetMorphed(this);
                        return vectorNode;
                    }
                }
            }
        }

        // Fold ConvertVectorToMask(ConvertMaskToVector(mask)) to mask
        if (tree.IsConvertVectorToMask)
        {
            var op = op1;
            if (op.Oper.IsHWIntrinsic)
            {
                uint simdBaseTypeSize = simdBaseType.Size;
                var cvtOp = op.AsHWIntrinsic();

                if (cvtOp.IsConvertMaskToVector)
                {
                    if (cvtOp.SimdBaseType.Size == simdBaseTypeSize)
                    {
                        // We need the operand to be the same kind of mask; otherwise
                        // the bitwise operation can differ in how it performs

                        var maskNode = cvtOp.GetOp(1);

                        DEBUG_DESTROY_NODE(op);
                        DEBUG_DESTROY_NODE(tree);
                        return maskNode;
                    }
                }
            }
        }

        var oper = tree.GetOperForHWIntrinsicId(out var isScalar);

        // We shouldn't find AND_NOT nodes since it should only be produced in lowering
        assert(oper != GT_AND_NOT);

        if (GenTreeHWIntrinsic.OperIsBitwiseHWIntrinsic(oper))
        {
            // Comparisons that produce masks lead to more verbose trees than
            // necessary in many scenarios due to requiring a CvtMaskToVector
            // node to be inserted over them and this can block various opts
            // that are dependent on tree height and similar. So we want to
            // fold the unnecessary back and forth conversions away where possible.

            var effectiveOper = oper;

            // We need both operands to be ConvertMaskToVector in
            // order to optimize this to a direct mask operation

            if (op1.IsConvertMaskToVector)
            {
                assert((oper == GT_NOT) == (op2 is null));

                if ((op2 is not null) && !op2.Oper.IsHWIntrinsic)
                {
                    if ((oper == GT_XOR) && op2.IsVectorAllBitsSet)
                    {
                        // We want to explicitly recognize op1 ^ AllBitsSet as
                        // some platforms don't have direct support for ~op1

                        effectiveOper = GT_NOT;
                    }
                }

                var cvtOp1 = op1.AsHWIntrinsic();
                GenTreeHWIntrinsic? cvtOp2;
                if (effectiveOper == GT_NOT)
                {
                    cvtOp2 = cvtOp1;
                }
                else
                {
                    assert(op2 is not null);
                    cvtOp2 = op2.IsConvertMaskToVector ? op2.AsHWIntrinsic() : null;
                }

                if (cvtOp2 is not null)
                {
                    var op1SimdBaseType = cvtOp1.SimdBaseType;
                    var op2SimdBaseType = cvtOp2.SimdBaseType;

                    if (op1SimdBaseType.Size == op2SimdBaseType.Size)
                    {
                        // We need both operands to be the same kind of mask; otherwise
                        // the bitwise operation can differ in how it performs

                        var maskIntrinsicId = NI_Illegal;

                        switch (effectiveOper)
                        {
                            case GT_AND:
                            {
                                maskIntrinsicId = NI_AVX512_AndMask;
                                break;
                            }

                            case GT_NOT:
                            {
                                maskIntrinsicId = NI_AVX512_NotMask;
                                break;
                            }

                            case GT_OR:
                            {
                                maskIntrinsicId = NI_AVX512_OrMask;
                                break;
                            }

                            case GT_XOR:
                            {
                                maskIntrinsicId = NI_AVX512_XorMask;
                                break;
                            }

                            default:
                            {
                                unreached();
                                break;
                            }
                        }

                        assert(maskIntrinsicId != NI_Illegal);

                        if (effectiveOper == oper)
                        {
                            tree.ChangeHWIntrinsicId(maskIntrinsicId);
                            tree.GetOpRef(1) = cvtOp1.GetOp(1);
                        }
                        else
                        {
                            assert(effectiveOper == GT_NOT);
                            tree.ResetHWIntrinsicId(maskIntrinsicId, cvtOp1.GetOp(1));
                            tree.Flags &= ~GTF_REVERSE_OPS;
                        }

                        // The bitwise operation is likely normalized to int or uint, while
                        // the underlying convert ops may be a small type. We need to preserve
                        // such a small type since that indicates how many elements are in the mask.
                        simdBaseType = cvtOp1.SimdBaseType;
                        tree.SimdBaseType = simdBaseType;

                        tree.Type = TYP_MASK;
                        DEBUG_DESTROY_NODE(op1);

                        if (effectiveOper != GT_NOT)
                        {
                            tree.GetOpRef(2) = cvtOp2.GetOp(1);
                        }

                        if (op2 is not null)
                        {
                            DEBUG_DESTROY_NODE(op2);
                        }
                        tree.SetMorphed(this);

                        tree = gtNewSimdCvtMaskToVectorNode(retType, tree, simdBaseType, simdSize).AsHWIntrinsic();
                        tree.SetMorphed(this);

                        return tree;
                    }
                }
            }
        }

        GenTree? cnsNode = null;
        GenTree? otherNode = null;

        if (op1.Oper.IsConst)
        {
            cnsNode = op1;
            otherNode = op2;
        }
        else if (op2 is not null)
        {
            if (op2.Oper.IsConst)
            {
                cnsNode = op2;
                otherNode = op1;
            }
            else if (op3 is not null)
            {
                if (op3.Oper.IsConst)
                {
                    cnsNode = op3;
                }
            }
        }

        if (cnsNode is null)
        {
            // No constants, so nothing to fold
            return tree;
        }

        GenTree resultNode = tree;

        if (opCount == 1)
        {
            if (oper != GT_NONE)
            {
                if (varTypeIsMask(retType))
                {
                    cnsNode.AsMskCon().EvaluateUnaryInPlace(oper, isScalar, simdBaseType, simdSize);
                }
                else
                {
                    if (!cnsNode.AsVecCon().TryEvaluateUnaryInPlace(oper, isScalar, simdBaseType))
                    {
                        return tree;
                    }
                }
                resultNode = cnsNode;
            }
            else if (tree.IsConvertMaskToVector)
            {
                var mskCon = cnsNode.AsMskCon();

                {
                    simd_t maskVector = default;
                    EvaluateSimdCvtMaskToVector(simdBaseType, maskVector.AsSpan<byte>(), mskCon.SimdMaskVal);

                    var vector = gtNewVconNode(retType);
                    vector.SimdVal = maskVector;
                    resultNode = vector;
                }
            }
            else if (tree.IsConvertVectorToMask)
            {
                resultNode = gtFoldExprConvertVecCnsToMask(tree, cnsNode.AsVecCon());
            }
            else
            {
                switch (ni)
                {
                    case NI_Vector_ExtractMostSignificantBits:
                    case NI_X86Base_MoveMask:
                    case NI_AVX_MoveMask:
                    case NI_AVX2_MoveMask:
                    {
                        simdmask_t simdMaskVal = default;

                        switch (simdSize)
                        {

                            case 16:
                            {
                                EvaluateExtractMSB(simdBaseType, ref simdMaskVal, cnsNode.AsVecCon().SimdVal.AsSpan<byte>()[..16]);
                                break;
                            }

                            case 32:
                            {
                                EvaluateExtractMSB(simdBaseType, ref simdMaskVal, cnsNode.AsVecCon().SimdVal.AsSpan<byte>()[..32]);
                                break;
                            }

                            default:
                            {
                                unreached();
                                break;
                            }
                        }

                        var elemCount = simdSize / simdBaseType.Size;
                        var mask = simdMaskVal.RawBits & simdmask_t.GetBitMask(elemCount);

                        assert(varTypeIsInt(retType));
                        assert(elemCount <= 32);

                        resultNode = gtNewIconNode(TYP_INT, (int)(mask));
                        break;
                    }

                    case NI_AVX512_MoveMask:
                    {
                        var mskCns = cnsNode.AsMskCon();

                        var elemCount = simdSize / simdBaseType.Size;
                        var mask = mskCns.SimdMaskVal.RawBits & simdmask_t.GetBitMask(elemCount);

                        if (varTypeIsInt(retType))
                        {
                            assert(elemCount <= 32);
                            resultNode = gtNewIconNode(TYP_INT, (int)(mask));
                        }
                        else
                        {
                            assert(varTypeIsLong(retType));
                            resultNode = gtNewLconNode((long)(mask));
                        }
                        break;
                    }

                    case NI_AVX2_LeadingZeroCount:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = (int)(cnsNode.AsIntConCommon().IconValue);
                        var result = BitOperations.LeadingZeroCount((uint)(value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_AVX2_X64_LeadingZeroCount:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;
                        var result = BitOperations.LeadingZeroCount((ulong)(value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_Vector_AsVector3:
                    case NI_Vector_AsVector128Unsafe:
                    case NI_Vector_AsVector2:
                    case NI_Vector_GetLower:
                    case NI_Vector_GetLower128:
                    case NI_Vector_ToVector256Unsafe:
                    case NI_Vector_ToVector512Unsafe:
                    {
                        // These are all going to a smaller type taking the lowest bits
                        // or are unsafely going to a larger type, so we just need to retype
                        // the constant and we're good to go.

                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_Vector_ToVector256:
                    {
                        assert(retType == TYP_SIMD32);
                        assert((cnsNode.Type == TYP_SIMD16));
                        cnsNode.AsVecCon().SimdVal.v256[0].v128[1] = default;

                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_Vector_ToVector512:
                    {
                        assert(retType == TYP_SIMD64);

                        if ((cnsNode.Type == TYP_SIMD16))
                        {
                            cnsNode.AsVecCon().SimdVal.v128[1] = default;
                        }
                        else
                        {
                            assert((cnsNode.Type == TYP_SIMD32));
                        }
                        cnsNode.AsVecCon().SimdVal.v256[1] = default;

                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_Vector_GetUpper:
                    {
                        if (retType == TYP_SIMD16)
                        {
                            assert((cnsNode.Type == TYP_SIMD32));
                            cnsNode.AsVecCon().SimdVal.v128[0] = cnsNode.AsVecCon().SimdVal.v256[0].v128[1];
                        }
                        else
                        {
                            assert(retType == TYP_SIMD32);
                            assert((cnsNode.Type == TYP_SIMD64));
                            cnsNode.AsVecCon().SimdVal.v256[0] = cnsNode.AsVecCon().SimdVal.v256[1];
                        }

                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_Vector_ToScalar:
                    {

                        if (varTypeIsFloating(retType))
                        {
                            var result = cnsNode.AsVecCon().GetElementFloating(simdBaseType, 0);

                            resultNode = gtNewDconNode(retType, result);
                        }
                        else
                        {
                            assert(varTypeIsIntegral(retType));
                            var result = cnsNode.AsVecCon().GetElementIntegral(simdBaseType, 0);

                            if (varTypeIsLong(retType))
                            {
                                resultNode = gtNewLconNode(result);
                            }
                            else
                            {
                                resultNode = gtNewIconNode(retType, (int)(result));
                            }
                        }
                        break;
                    }

                    case NI_AVX2_TrailingZeroCount:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = (int)(cnsNode.AsIntConCommon().IconValue);
                        var result = BitOperations.TrailingZeroCount((uint)(value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_AVX2_X64_TrailingZeroCount:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;
                        var result = BitOperations.TrailingZeroCount((ulong)(value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_PopCount:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = (int)(cnsNode.AsIntConCommon().IconValue);
                        var result = BitOperations.PopCount((uint)(value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_X64_PopCount:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;
                        var result = BitOperations.PopCount((ulong)(value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_BitScanForward:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = (int)(cnsNode.AsIntConCommon().IconValue);

                        if (value == 0)
                        {
                            // bsf is undefined for 0
                            break;
                        }
                        var result = BitOperations.TrailingZeroCount((uint)(value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_X64_BitScanForward:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;

                        if (value == 0)
                        {
                            // bsf is undefined for 0
                            break;
                        }
                        var result = BitOperations.TrailingZeroCount((ulong)(value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_BitScanReverse:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = (int)(cnsNode.AsIntConCommon().IconValue);

                        if (value == 0)
                        {
                            // bsr is undefined for 0
                            break;
                        }
                        var result = (31 - BitOperations.LeadingZeroCount((uint)value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_X64_BitScanReverse:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;

                        if (value == 0)
                        {
                            // bsr is undefined for 0
                            break;
                        }
                        var result = (63 - BitOperations.LeadingZeroCount((ulong)value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }
        }
        else if (opCount == 2)
        {
            assert((op2 is not null) && (otherNode is not null));
            if (otherNode.Oper.IsConst)
            {
                if (oper != GT_NONE)
                {
                    if (varTypeIsMask(retType))
                    {
                        if (varTypeIsMask(cnsNode.Type))
                        {
                            cnsNode.AsMskCon().EvaluateBinaryInPlace(oper, isScalar, simdBaseType, simdSize,
                                                                       otherNode.AsMskCon());
                        }
                        else
                        {
                            cnsNode.AsVecCon().EvaluateBinaryInPlace(oper, isScalar, simdBaseType, otherNode.AsVecCon());
                        }
                    }
                    else
                    {
                        if ((oper == GT_LSH) || (oper == GT_RSH) || (oper == GT_RSZ))
                        {
                            if ((otherNode.Type == TYP_SIMD16))
                            {
                                if (!HWIntrinsicInfo.IsVariableShift(ni))
                                {
                                    // The xarch shift instructions support taking the shift amount as
                                    // a simd16, in which case they take the shift amount from the lower
                                    // 64-bits.

                                    var shiftAmount = otherNode.AsVecCon().GetElementIntegral(TYP_LONG, 0);

                                    if ((ulong)(shiftAmount) >=
                                        ((ulong)(simdBaseType.Size) * BITS_PER_BYTE))
                                    {
                                        // Set to -1 to indicate an explicit overshift
                                        shiftAmount = -1;
                                    }

                                    // Ensure we broadcast to the right vector size
                                    otherNode.Type = retType;

                                    otherNode.AsVecCon().EvaluateBroadcastInPlace(simdBaseType, shiftAmount);
                                }
                            }
                        }

                        if (otherNode.Oper.IsIntegralConst)
                        {
                            var scalar = otherNode.AsIntConCommon().IntegralValue;

                            otherNode = gtNewVconNode(retType);
                            otherNode.AsVecCon().EvaluateBroadcastInPlace(simdBaseType, scalar);
                        }

                        cnsNode.AsVecCon().EvaluateBinaryInPlace(oper, isScalar, simdBaseType, otherNode.AsVecCon());
                    }
                    resultNode = cnsNode;
                }
                else
                {
                    switch (ni)
                    {
                        case NI_Vector_GetElement:
                        {
                            var index = (uint)(otherNode.AsIntConCommon().IconValue);

                            if (index >= GenTreeVecCon.ElementCount(simdSize, simdBaseType))
                            {
                                // Nothing to fold for out of range indexes
                                break;
                            }

                            if (varTypeIsFloating(retType))
                            {
                                var result = cnsNode.AsVecCon().GetElementFloating(simdBaseType, (int)index);

                                resultNode = gtNewDconNode(retType, result);
                            }
                            else
                            {
                                assert(varTypeIsIntegral(retType));
                                var result = cnsNode.AsVecCon().GetElementIntegral(simdBaseType, (int)index);

                                if (varTypeIsLong(retType))
                                {
                                    resultNode = gtNewLconNode(result);
                                }
                                else
                                {
                                    resultNode = gtNewIconNode(retType, (int)(result));
                                }
                            }
                            break;
                        }

                        case NI_Vector_WithLower:
                        {
                            assert((cnsNode.Type == retType));

                            if (retType == TYP_SIMD32)
                            {
                                assert((otherNode.Type == TYP_SIMD16));
                                cnsNode.AsVecCon().SimdVal.v256[0].v128[0] = otherNode.AsVecCon().SimdVal.v128[0];
                            }
                            else
                            {
                                assert(retType == TYP_SIMD64);
                                assert((otherNode.Type == TYP_SIMD32));
                                cnsNode.AsVecCon().SimdVal.v256[0] = otherNode.AsVecCon().SimdVal.v256[0];
                            }

                            resultNode = cnsNode;
                            break;
                        }

                        case NI_Vector_WithUpper:
                        {
                            assert((cnsNode.Type == retType));

                            if (retType == TYP_SIMD32)
                            {
                                assert((otherNode.Type == TYP_SIMD16));
                                cnsNode.AsVecCon().SimdVal.v256[0].v128[1] = otherNode.AsVecCon().SimdVal.v128[0];
                            }
                            else
                            {
                                assert(retType == TYP_SIMD64);
                                assert((otherNode.Type == TYP_SIMD32));
                                cnsNode.AsVecCon().SimdVal.v256[1] = otherNode.AsVecCon().SimdVal.v256[0];
                            }

                            resultNode = cnsNode;
                            break;
                        }

                        case NI_Vector_op_Equality:
                        {
                            cnsNode.AsVecCon().EvaluateBinaryInPlace(GT_EQ, isScalar, simdBaseType,
                                                                       otherNode.AsVecCon());
                            resultNode = gtNewIconNode(retType, cnsNode.AsVecCon().IsAllBitsSet ? 1 : 0);
                            break;
                        }

                        case NI_Vector_op_Inequality:
                        {
                            cnsNode.AsVecCon().EvaluateBinaryInPlace(GT_NE, isScalar, simdBaseType,
                                                                       otherNode.AsVecCon());
                            resultNode = gtNewIconNode(retType, cnsNode.AsVecCon().IsZero ? 0 : 1);
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                if (isScalar)
                {
                    // We don't support folding for scalars when only one input is constant
                    // because it means one value is computed and the remaining values are
                    // either zeroed or preserved based on the underlying target architecture
                    oper = GT_NONE;
                }

                // For mask nodes in particular, the foldings below are done under the presumption
                // that we only produce something like `AddMask(op1, op2)` if op1 and op2 are compatible
                // masks. On xarch, for example, this means that it'd be adding 8, 16, 32, or 64-bits
                // together with the same size. We wouldn't ever encounter something like an 8 and 16 bit
                // masks being added. This ensures that we don't end up with a case where folding would
                // cause a different result to be produced, such as because the remaining upper bits are
                // no longer zeroed.

                switch (oper)
                {
                    case GT_ADD:
                    {
                        if (varTypeIsMask(retType))
                        {
                            // Handle `x + 0 == x` and `0 + x == x`
                            if (cnsNode.IsMaskZero)
                            {
                                resultNode = otherNode;
                            }
                            break;
                        }

                        if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `x + NaN == NaN` and `NaN + x == NaN`
                            // This is safe for all floats since we do not fault for sNaN

                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }

                            // Handle `x + -0 == x` and `-0 + x == x`

                            if (cnsNode.IsVectorNegativeZero(simdBaseType))
                            {
                                resultNode = otherNode;
                                break;
                            }

                            // We cannot handle `x + 0 == x` or `0 + x == x` since `-0 + 0 == 0`
                            break;
                        }

                        // Handle `x + 0 == x` and `0 + x == x`
                        if (cnsNode.IsVectorZero)
                        {
                            resultNode = otherNode;
                        }
                        break;
                    }

                    case GT_AND:
                    {
                        if (varTypeIsMask(retType))
                        {
                            // Handle `x & 0 == 0` and `0 & x == 0`
                            if (cnsNode.IsMaskZero)
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }

                            // Handle `x & AllBitsSet == x` and `AllBitsSet & x == x`
                            if (cnsNode.IsMaskAllBitsSet)
                            {
                                resultNode = otherNode;
                            }
                            break;
                        }

                        // Handle `x & 0 == 0` and `0 & x == 0`
                        if (cnsNode.IsVectorZero)
                        {
                            resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                            break;
                        }

                        // Handle `x & AllBitsSet == x` and `AllBitsSet & x == x`
                        if (cnsNode.IsVectorAllBitsSet)
                        {
                            resultNode = otherNode;
                        }
                        break;
                    }

                    case GT_DIV:
                    {
                        assert(!varTypeIsMask(retType));

                        if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `x / NaN == NaN` and `NaN / x == NaN`
                            // This is safe for all floats since we do not fault for sNaN

                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }

                        // Handle `x / 1 == x`.
                        // This is safe for all floats since we do not fault for sNaN

                        if (cnsNode != op2)
                        {
                            break;
                        }

                        if (!cnsNode.IsVectorBroadcast(simdBaseType))
                        {
                            break;
                        }

                        if (cnsNode.AsVecCon().IsScalarOne(simdBaseType))
                        {
                            resultNode = otherNode;
                        }
                        break;
                    }

                    case GT_EQ:
                    {
                        if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x == NaN) == false` and `(NaN == x) == false` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                long zero = 0;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, zero);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        else if (otherNode.IsVectorPerElementMask(this, simdBaseType, simdSize))
                        {
                            // Handle `Equals(PerElementMask, AllBitsSet)` and `Equals(AllBitsSet, PerElementMask)` for
                            // integrals
                            if (cnsNode.IsVectorAllBitsSet)
                            {
                                // We are comparing something that is known per element to be either
                                // AllBitsSet or Zero, with AllBitsSet.
                                //
                                // In such a case:
                                // * `AllBitsSet == AllBitsSet` is true and so produces `AllBitsSet`
                                // * `AllBitsSet == Zero` is false and so produces `Zero`
                                //
                                // This means that we are not changing anything and can just return
                                // the per element mask

                                resultNode = otherNode;
                                break;
                            }
                        }
                        break;
                    }

                    case GT_GT:
                    {
                        if (varTypeIsUnsigned(simdBaseType))
                        {
                            // Handle `(0 > x) == false` for unsigned types.
                            if ((cnsNode == op1) && cnsNode.IsVectorZero)
                            {
                                long zero = 0;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, zero);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        else if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x > NaN) == false` and `(NaN > x) == false` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                long zero = 0;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, zero);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        break;
                    }

                    case GT_GE:
                    {
                        if (varTypeIsUnsigned(simdBaseType))
                        {
                            // Handle `x >= 0 == true` for unsigned types.
                            if ((cnsNode == op2) && cnsNode.IsVectorZero)
                            {
                                long allBitsSet = -1;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, allBitsSet);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        else if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x >= NaN) == false` and `(NaN >= x) == false` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                long zero = 0;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, zero);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        break;
                    }

                    case GT_LT:
                    {
                        if (varTypeIsUnsigned(simdBaseType))
                        {
                            // Handle `x < 0 == false` for unsigned types.
                            if ((cnsNode == op2) && cnsNode.IsVectorZero)
                            {
                                long zero = 0;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, zero);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        else if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x < NaN) == false` and `(NaN < x) == false` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                long zero = 0;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, zero);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        break;
                    }

                    case GT_LE:
                    {
                        if (varTypeIsUnsigned(simdBaseType))
                        {
                            // Handle `0 <= x == true` for unsigned types.
                            if ((cnsNode == op1) && cnsNode.IsVectorZero)
                            {
                                long allBitsSet = -1;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, allBitsSet);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        else if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x <= NaN) == false` and `(NaN <= x) == false` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                long zero = 0;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, zero);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        break;
                    }

                    case GT_MUL:
                    {
                        assert(!varTypeIsMask(retType));

                        if (!varTypeIsFloating(simdBaseType))
                        {
                            // Handle `x * 0 == 0` and `0 * x == 0`
                            // Not safe for floating-point when x == -0.0, NaN, +Inf, -Inf
                            if (cnsNode.IsVectorZero)
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        else
                        {
                            // Handle `x * NaN == NaN` and `NaN * x == NaN`
                            // This is safe for all floats since we do not fault for sNaN

                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }

                            // We cannot handle `x *  0 ==  0` or ` 0 * x ==  0` since `-0 *  0 == -0`
                            // We cannot handle `x * -0 == -0` or `-0 * x == -0` since `-0 * -0 ==  0`
                        }

                        // Handle `x * 1 == x` and `1 * x == x`
                        // This is safe for all floats since we do not fault for sNaN

                        if (!cnsNode.IsVectorBroadcast(simdBaseType))
                        {
                            break;
                        }

                        if (cnsNode.AsVecCon().IsScalarOne(simdBaseType))
                        {
                            resultNode = otherNode;
                        }
                        break;
                    }

                    case GT_NE:
                    {
                        if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x != NaN) == true` and `(NaN != x) == true` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                long allBitsSet = -1;
                                cnsNode.AsVecCon().EvaluateBroadcastInPlace(TYP_LONG, allBitsSet);
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        else if (otherNode.IsVectorPerElementMask(this, simdBaseType, simdSize))
                        {
                            // Handle `~Equals(PerElementMask, Zero)` and `~Equals(Zero, PerElementMask)` for integrals
                            if (cnsNode.IsVectorZero)
                            {
                                // We are comparing something that is known per element to be either
                                // AllBitsSet or Zero, with Zero.
                                //
                                // In such a case:
                                // * `AllBitsSet != Zero` is true and so produces `AllBitsSet`
                                // * `Zero != Zero` is false and so produces `Zero`
                                //
                                // This means that we are not changing anything and can just return
                                // the per element mask

                                resultNode = otherNode;
                                break;
                            }
                        }
                        else if (otherNode.Oper.IsHWIntrinsic)
                        {
                            var otherIntrinsic = otherNode.AsHWIntrinsic();
                            var otherIntrinsicId = otherIntrinsic.HWIntrinsicId;

                            if (HWIntrinsicInfo.ReturnsPerElementMask(otherIntrinsicId) &&
                                (simdBaseType.Size == otherIntrinsic.SimdBaseType.Size))
                            {
                                // This optimization is only safe if we know the other node produces
                                // AllBitsSet or Zero per element and if the outer comparison is the
                                // same size as what the other node produces for its mask

                                // Handle `(Mask != Zero) == Mask` and `(Zero != Mask) == Mask` for integral types
                                if (cnsNode.IsVectorZero)
                                {
                                    resultNode = otherNode;
                                    break;
                                }
                            }
                        }
                        break;
                    }

                    case GT_OR:
                    {
                        if (varTypeIsMask(retType))
                        {
                            // Handle `x | 0 == x` and `0 | x == x`
                            if (cnsNode.IsMaskZero)
                            {
                                resultNode = otherNode;
                                break;
                            }

                            // Handle `x | AllBitsSet == AllBitsSet` and `AllBitsSet | x == AllBitsSet`
                            if (cnsNode.IsMaskAllBitsSet)
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                            }
                            break;
                        }

                        // Handle `x | 0 == x` and `0 | x == x`
                        if (cnsNode.IsVectorZero)
                        {
                            resultNode = otherNode;
                            break;
                        }

                        // Handle `x | AllBitsSet == AllBitsSet` and `AllBitsSet | x == AllBitsSet`
                        if (cnsNode.IsVectorAllBitsSet)
                        {
                            resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                        }
                        break;
                    }

                    case GT_ROL:
                    case GT_ROR:
                    case GT_LSH:
                    case GT_RSH:
                    case GT_RSZ:
                    {
                        // Handle `x rol 0 == x` and `0 rol x == 0`
                        // Handle `x ror 0 == x` and `0 ror x == 0`
                        // Handle `x <<  0 == x` and `0 <<  x == 0`
                        // Handle `x >>  0 == x` and `0 >>  x == 0`
                        // Handle `x >>> 0 == x` and `0 >>> x == 0`

                        if (varTypeIsMask(retType))
                        {
                            if (cnsNode.IsMaskZero)
                            {
                                if (cnsNode == op2)
                                {
                                    resultNode = otherNode;
                                }
                                else
                                {
                                    resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                }
                            }
                            else if (cnsNode.IsIntegralConst(0))
                            {
                                assert(cnsNode == op2);
                                resultNode = otherNode;
                            }
                            break;
                        }

                        if (cnsNode.IsVectorZero)
                        {
                            if (cnsNode == op2)
                            {
                                resultNode = otherNode;
                            }
                            else
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                            }
                        }
                        else if (cnsNode.IsIntegralConst(0))
                        {
                            assert(cnsNode == op2);
                            resultNode = otherNode;
                        }
                        break;
                    }

                    case GT_SUB:
                    {
                        assert(!varTypeIsMask(retType));

                        if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `x - NaN == NaN` and `NaN - x == NaN`
                            // This is safe for all floats since we do not fault for sNaN

                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }

                            // We cannot handle `x - -0 == x` since `-0 - -0 == 0`
                        }

                        // Handle `x - 0 == x`
                        if ((op2 == cnsNode) && cnsNode.IsVectorZero)
                        {
                            resultNode = otherNode;
                        }
                        break;
                    }

                    case GT_XOR:
                    {
                        if (varTypeIsMask(retType))
                        {
                            // Handle `x ^ 0 == x` and `0 ^ x == x`
                            if (cnsNode.IsMaskZero)
                            {
                                resultNode = otherNode;
                            }
                            break;
                        }

                        // Handle `x ^ 0 == x` and `0 ^ x == x`
                        if (cnsNode.IsVectorZero)
                        {
                            resultNode = otherNode;
                        }
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                switch (ni)
                {

                    case NI_Vector_op_Equality:
                    {
                        if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x == NaN) == false` and `(NaN == x) == false` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                resultNode = gtNewIconNode(retType, 0);
                                resultNode = gtWrapWithSideEffects(resultNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        break;
                    }

                    case NI_Vector_op_Inequality:
                    {
                        if (varTypeIsFloating(simdBaseType))
                        {
                            // Handle `(x != NaN) == true` and `(NaN != x) == true` for floating-point types
                            if (cnsNode.IsVectorNaN(simdBaseType))
                            {
                                resultNode = gtNewIconNode(retType, 1);
                                resultNode = gtWrapWithSideEffects(resultNode, otherNode, GTF_ALL_EFFECT);
                                break;
                            }
                        }
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }
        }
        else
        {
            assert((opCount == 3) && (op2 is not null) && (op3 is not null));
            switch (ni)
            {
                case NI_Vector_ConditionalSelect:
                {
                    assert(!varTypeIsMask(retType));
                    assert(!varTypeIsMask(op1.Type));

                    if (cnsNode != op1)
                    {
                        break;
                    }

                    if (op1.IsVectorAllBitsSet)
                    {
                        if ((op3.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0)
                        {
                            break;
                        }
                        return op2;
                    }

                    if (op1.IsVectorZero)
                    {
                        return gtWrapWithSideEffects(op3, op2, GTF_ALL_EFFECT);
                    }

                    if (op2.Oper.IsCnsVec && op3.Oper.IsCnsVec)
                    {
                        // op2 = op2 & op1
                        op2.AsVecCon().EvaluateBinaryInPlace(GT_AND, false, simdBaseType, op1.AsVecCon());

                        // op3 = op3 & ~op1
                        op3.AsVecCon().EvaluateBinaryInPlace(GT_AND_NOT, false, simdBaseType, op1.AsVecCon());

                        // op2 = op2 | op3
                        op2.AsVecCon().EvaluateBinaryInPlace(GT_OR, false, simdBaseType, op3.AsVecCon());

                        resultNode = op2;
                    }
                    break;
                }

                case NI_Vector_WithElement:
                {
                    if ((cnsNode != op1) || !op2.Oper.IsCnsIntOrI || !op3.Oper.IsConst)
                    {
                        break;
                    }

                    var index = (uint)(op2.AsIntConCommon().IconValue);

                    if (index >= GenTreeVecCon.ElementCount(simdSize, simdBaseType))
                    {
                        // Nothing to fold for out of range indexes
                        break;
                    }

                    if (varTypeIsFloating(simdBaseType))
                    {
                        var value = op3.AsDblCon().DconVal;
                        cnsNode.AsVecCon().SetElementFloating(simdBaseType, (int)index, value);
                        resultNode = cnsNode;
                    }
                    else
                    {
                        assert(varTypeIsIntegral(simdBaseType));
                        var value = op3.AsIntConCommon().IntegralValue;
                        cnsNode.AsVecCon().SetElementIntegral(simdBaseType, (int)index, value);
                        resultNode = cnsNode;
                    }
                    break;
                }

                case NI_AVX_Compare:
                case NI_AVX_CompareScalar:
                case NI_AVX512_CompareMask:
                {
                    if (!op3.Oper.IsCnsIntOrI)
                    {
                        break;
                    }

                    FloatComparisonMode mode = (FloatComparisonMode)(op3.AsIntConCommon().IntegralValue);
                    var id = ni;

                    switch (mode)
                    {
                        case FloatComparisonMode.OrderedFalseNonSignaling:
                        case FloatComparisonMode.OrderedFalseSignaling:
                        case FloatComparisonMode.UnorderedTrueNonSignaling:
                        case FloatComparisonMode.UnorderedTrueSignaling:
                        {
                            var isFalse = (mode == FloatComparisonMode.OrderedFalseNonSignaling) ||
                                           (mode == FloatComparisonMode.OrderedFalseSignaling);

                            if (ni == NI_AVX_CompareScalar)
                            {
                                // CompareScalar only updates the lowest element and copies the remaining
                                // (upper) elements from op1 unchanged. We can therefore only constant fold
                                // this when op1 is itself a constant so that those upper elements are known.

                                if (!op1.Oper.IsCnsVec)
                                {
                                    break;
                                }

                                var vecCon = op1.AsVecCon();

                                // Set the lowest element to the comparison result (all zeros for a false
                                // mode, all ones for a true mode) while leaving the upper elements intact.

                                if (simdBaseType == TYP_FLOAT)
                                {
                                    vecCon.SimdVal.u32[0] = isFalse ? 0 : 0xFFFFFFFF;
                                }
                                else
                                {
                                    assert(simdBaseType == TYP_DOUBLE);
                                    vecCon.SimdVal.u64[0] = isFalse ? 0 : 0xFFFFFFFFFFFFFFFF;
                                }

                                fgUpdateConstTreeValueNumber(vecCon);
                                resultNode = vecCon;
                            }
                            else if (isFalse)
                            {
                                resultNode = gtNewZeroConNode(retType);
                            }
                            else if (varTypeIsMask(retType))
                            {
                                // A true comparison produces an all-true mask for the given element count.

                                var mskCon = gtNewMskConNode(default);
                                mskCon.SimdMaskVal =
                                    simdmask_t.AllBitsSet(GenTreeVecCon.ElementCount(simdSize, simdBaseType));
                                resultNode = mskCon;
                            }
                            else
                            {
                                resultNode = gtNewAllBitsSetConNode(retType);
                            }

                            resultNode = gtWrapWithSideEffects(resultNode, tree, GTF_ALL_EFFECT);
                            break;
                        }

                        default:
                        {
                            id = HWIntrinsicInfo.lookupIdForFloatComparisonMode(ni, mode, simdBaseType, simdSize);
                            break;
                        }
                    }

                    if (id == ni)
                    {
                        break;
                    }

                    tree.ResetHWIntrinsicId(id, op1, op2);
                    DEBUG_DESTROY_NODE(op3);

                    tree.SetMorphed(this);
                    return gtFoldExprHWIntrinsic(tree);
                }

                case NI_X86Base_BlendVariable:
                case NI_AVX_BlendVariable:
                case NI_AVX2_BlendVariable:
                case NI_AVX512_BlendVariableMask:
                {
                    if (!op3.Oper.IsConst)
                    {
                        break;
                    }

                    bool maskIsZero;
                    var maskIsAllBitsSet = false;

                    if (op3.Oper.IsCnsMsk)
                    {
                        maskIsZero = op3.IsMaskZero;

                        if (!maskIsZero)
                        {
                            var mask = op3.AsMskCon();
                            var elemCount = simdSize / simdBaseType.Size;

                            maskIsAllBitsSet = mask.SimdMaskVal.RawBits == simdmask_t.GetBitMask(elemCount);
                        }
                    }
                    else
                    {
                        assert(op3.Oper.IsCnsVec);

                        maskIsZero = op3.IsVectorZero;

                        if (!maskIsZero)
                        {
                            maskIsAllBitsSet = op3.IsVectorAllBitsSet;
                        }
                    }

                    if (maskIsAllBitsSet)
                    {
                        if ((op1.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0)
                        {
                            break;
                        }
                        return op2;
                    }

                    if (maskIsZero)
                    {
                        if ((op2.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0)
                        {
                            break;
                        }
                        return op1;
                    }

                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        if (varTypeIsMask(retType) && !varTypeIsMask(resultNode.Type))
        {
            resultNode = gtNewSimdCvtVectorToMaskNode(retType, resultNode, simdBaseType, simdSize);
            return gtFoldExprHWIntrinsic(resultNode.AsHWIntrinsic());
        }

        if (resultNode != tree)
        {
            resultNode.SetMorphed(this);
            if (resultNode.Oper == GT_COMMA)
            {
                resultNode.AsOp().Op2.SetMorphed(this);
            }

            if (resultNode.Oper.IsConst)
            {
                fgUpdateConstTreeValueNumber(resultNode);

                // Make sure no side effect flags are set on this constant node.
                resultNode.Flags &= ~GTF_ALL_EFFECT;
            }
        }

        return resultNode;
    }
#endif
}
#endif
