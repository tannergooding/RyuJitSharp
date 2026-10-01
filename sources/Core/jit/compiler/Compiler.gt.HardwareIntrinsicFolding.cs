// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
using System;
using System.Buffers.Binary;
using System.Numerics;
#if TARGET_ARM64
using System.Runtime.InteropServices;
#endif
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
                if (!EvaluateSimdCvtScalableVectorToMask(tree.SimdBaseType, ref mskCon.SimdScalableMaskVal,
                    vecCon.SimdScalableVal))
                {
                    return tree;
                }
                break;
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

    public GenTree gtFoldExprHWIntrinsic(GenTreeHWIntrinsic tree)
    {
        assert(!optValnumCSE_phase);
        assert(opts.Tier0OptimizationEnabled);

        var ni = tree.HWIntrinsicId;
        var retType = tree.Type;
        var simdBaseType = tree.SimdBaseType;
        var simdSize = tree.SimdSize;

        simd_t simdVal = default;

        if (
#if TARGET_ARM64
            (retType != TYP_SIMD) &&
#endif
            GenTreeVecCon.IsHWIntrinsicCreateConstant(tree, ref simdVal))
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

#if FEATURE_MASKED_HW_INTRINSICS
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

#if TARGET_XARCH
                        var vectorNode = cvtOp.GetOp(1);
#elif TARGET_ARM64
                        var vectorNode = cvtOp.GetOp(2);
#else
#error Unsupported hardware-intrinsic folding target
#endif

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
#if TARGET_XARCH
            var tryHandle = op.Oper.IsHWIntrinsic;
#elif TARGET_ARM64
            assert(op.Oper.IsHWIntrinsic && op.AsHWIntrinsic().HWIntrinsicId == NI_Sve_ConversionTrueMask);
            op = op2 ?? throw new InvalidOperationException();
            var tryHandle = op.Oper.IsHWIntrinsic;
#else
            var tryHandle = false;
#endif
            if (tryHandle)
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

#if TARGET_ARM64
                        DEBUG_DESTROY_NODE(op1);
#endif
                        DEBUG_DESTROY_NODE(op);
                        DEBUG_DESTROY_NODE(tree);
                        return maskNode;
                    }
                }
            }
        }
#else
        assert(!tree.IsConvertMaskToVector);
        assert(!tree.IsConvertVectorToMask);
#endif

        var oper = tree.GetOperForHWIntrinsicId(out var isScalar);

        // We shouldn't find AND_NOT nodes since it should only be produced in lowering
        assert(oper != GT_AND_NOT);

#if FEATURE_MASKED_HW_INTRINSICS && TARGET_XARCH
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
#elif FEATURE_MASKED_HW_INTRINSICS && TARGET_ARM64
        if ((HWIntrinsicInfo.lookupFlags(ni) & HW_Flag_HasAllMaskVariant) != 0)
        {
            var maskVariant = HWIntrinsicInfo.GetMaskVariant(ni);
            assert(opCount == HWIntrinsicInfo.lookupNumArgs(maskVariant));

            var firstVectorOperand = 1;
            if (ni == NI_Sve_ConditionalSelect)
            {
                assert(varTypeIsMask(op1.Type));
                firstVectorOperand = 2;
            }

            var canFold = true;
            for (var index = firstVectorOperand; (index <= opCount) && canFold; index++)
            {
                var operand = tree.GetOp(index);
                canFold = operand.IsConvertMaskToVector &&
                    (operand.AsHWIntrinsic().SimdBaseType.Size == simdBaseType.Size);
            }

            if (canFold)
            {
                var operands = new GenTree[opCount];
                for (var index = 1; index <= opCount; index++)
                {
                    var operand = tree.GetOp(index);
                    if (operand.IsConvertMaskToVector)
                    {
                        operand = operand.AsHWIntrinsic().GetOp(1);
                    }
                    else if (operand.IsVectorZero)
                    {
                        operand = gtNewSimdFalseMaskByteNode();
                        operand.SetMorphed(this);
                    }

                    assert(varTypeIsMask(operand.Type));
                    operands[index - 1] = operand;
                }

                tree.ResetHWIntrinsicId(maskVariant, operands);
                tree.Type = TYP_MASK;
                tree.SetMorphed(this);
                tree = gtNewSimdCvtMaskToVectorNode(retType, tree, simdBaseType, simdSize).AsHWIntrinsic();
                tree.SetMorphed(this);
                op1 = tree.GetOp(1);
                op2 = null;
                op3 = null;
            }
        }
#endif

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
#if FEATURE_MASKED_HW_INTRINSICS
                if (varTypeIsMask(retType))
                {
                    cnsNode.AsMskCon().EvaluateUnaryInPlace(oper, isScalar, simdBaseType, simdSize);
                }
                else
#endif
                {
                    if (!cnsNode.AsVecCon().TryEvaluateUnaryInPlace(oper, isScalar, simdBaseType))
                    {
                        return tree;
                    }
                }
                resultNode = cnsNode;
            }
#if FEATURE_MASKED_HW_INTRINSICS
            else if (tree.IsConvertMaskToVector)
            {
                var mskCon = cnsNode.AsMskCon();

#if TARGET_ARM64
                if (retType == TYP_SIMD)
                {
                    simdscalable_t scalableValue = default;
                    if (EvaluateSimdCvtScalableMaskToVector(simdBaseType, ref scalableValue, mskCon.SimdScalableMaskVal))
                    {
                        resultNode = gtNewSimdVconNode(retType, scalableValue);
                    }
                }
                else
#endif
                {
                    simd_t maskVector = default;
                    EvaluateSimdCvtMaskToVector(simdBaseType, maskVector.AsSpan<byte>(), mskCon.SimdMaskVal);

                    var vector = gtNewVconNode(retType);
                    vector.SimdVal = maskVector;
                    resultNode = vector;
                }
            }
#if TARGET_XARCH
            else if (tree.IsConvertVectorToMask)
            {
                resultNode = gtFoldExprConvertVecCnsToMask(tree, cnsNode.AsVecCon());
            }
#endif
#endif
            else
            {
                switch (ni)
                {
                    case NI_Vector_ExtractMostSignificantBits:
#if TARGET_XARCH
                    case NI_X86Base_MoveMask:
                    case NI_AVX_MoveMask:
                    case NI_AVX2_MoveMask:
#elif TARGET_WASM
                    case NI_PackedSimd_Bitmask:
#endif
                    {
                        simdmask_t simdMaskVal = default;

                        switch (simdSize)
                        {
#if TARGET_ARM64
                            case 8:
                            {
                                EvaluateExtractMSB(simdBaseType, ref simdMaskVal, cnsNode.AsVecCon().SimdVal.AsSpan<byte>()[..8]);
                                break;
                            }
#endif
                            case 16:
                            {
                                EvaluateExtractMSB(simdBaseType, ref simdMaskVal, cnsNode.AsVecCon().SimdVal.AsSpan<byte>()[..16]);
                                break;
                            }

#if TARGET_XARCH
                            case 32:
                            {
                                EvaluateExtractMSB(simdBaseType, ref simdMaskVal, cnsNode.AsVecCon().SimdVal.AsSpan<byte>()[..32]);
                                break;
                            }
#endif

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

                        resultNode = gtNewIconNode(TYP_INT, unchecked((int)mask));
                        break;
                    }

#if TARGET_XARCH
                    case NI_AVX512_MoveMask:
                    {
                        var mskCns = cnsNode.AsMskCon();

                        var elemCount = simdSize / simdBaseType.Size;
                        var mask = mskCns.SimdMaskVal.RawBits & simdmask_t.GetBitMask(elemCount);

                        if (varTypeIsInt(retType))
                        {
                            assert(elemCount <= 32);
                            resultNode = gtNewIconNode(TYP_INT, unchecked((int)mask));
                        }
                        else
                        {
                            assert(varTypeIsLong(retType));
                            resultNode = gtNewLconNode((long)(mask));
                        }
                        break;
                    }
#endif

#if !TARGET_WASM
#if TARGET_ARM64
                    case NI_ArmBase_LeadingZeroCount:
#elif TARGET_XARCH
                    case NI_AVX2_LeadingZeroCount:
#endif
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = unchecked((int)cnsNode.AsIntConCommon().IconValue);
                        var result = BitOperations.LeadingZeroCount(unchecked((uint)value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }
#endif

#if TARGET_ARM64
                    case NI_ArmBase_Arm64_LeadingZeroCount:
                    {
                        assert(varTypeIsInt(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;
                        var result = BitOperations.LeadingZeroCount(unchecked((ulong)value));

                        cnsNode.AsIntConCommon().IconValue = unchecked((int)result);
                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }
#elif TARGET_XARCH
                    case NI_AVX2_X64_LeadingZeroCount:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;
                        var result = BitOperations.LeadingZeroCount(unchecked((ulong)value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }
#endif

                    case NI_Vector_AsVector3:
                    case NI_Vector_AsVector128Unsafe:
#if TARGET_XARCH
                    case NI_Vector_AsVector2:
                    case NI_Vector_GetLower:
                    case NI_Vector_GetLower128:
                    case NI_Vector_ToVector256Unsafe:
                    case NI_Vector_ToVector512Unsafe:
#elif TARGET_ARM64
                    case NI_Vector_GetLower:
                    case NI_Vector_ToVector128Unsafe:
#elif TARGET_WASM
                    case NI_Vector_AsVector2:
#else
#error Unsupported hardware-intrinsic folding target
#endif
                    {
                        // These are all going to a smaller type taking the lowest bits
                        // or are unsafely going to a larger type, so we just need to retype
                        // the constant and we're good to go.

                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }

#if TARGET_ARM64
                    case NI_Vector_ToVector128:
                    {
                        assert(retType == TYP_SIMD16);
                        assert(cnsNode.Type == TYP_SIMD8);
                        cnsNode.AsVecCon().SimdVal.v64[1] = default;

                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }
#elif TARGET_XARCH
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
#endif

#if TARGET_ARM64
                    case NI_Vector_GetUpper:
                    {
                        assert(retType == TYP_SIMD8);
                        assert(cnsNode.Type == TYP_SIMD16);
                        cnsNode.AsVecCon().SimdVal.v64[0] = cnsNode.AsVecCon().SimdVal.v64[1];

                        cnsNode.Type = retType;
                        resultNode = cnsNode;
                        break;
                    }
#elif TARGET_XARCH
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
#endif

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
                                resultNode = gtNewIconNode(retType, unchecked((int)result));
                            }
                        }
                        break;
                    }

#if TARGET_ARM64
                    case NI_ArmBase_ReverseElementBits:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = unchecked((uint)cnsNode.AsIntConCommon().IconValue);
                        cnsNode.AsIntConCommon().IconValue = unchecked((int)ReverseArm64Bits(value));
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_ArmBase_Arm64_ReverseElementBits:
                    {
                        assert(varTypeIsLong(retType));

                        var value = unchecked((ulong)cnsNode.AsIntConCommon().IntegralValue);
                        cnsNode.AsIntConCommon().IntegralValue = unchecked((long)ReverseArm64Bits(value));
                        resultNode = cnsNode;
                        break;
                    }
#endif

#if TARGET_XARCH
                    case NI_AVX2_TrailingZeroCount:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = unchecked((int)cnsNode.AsIntConCommon().IconValue);
                        var result = BitOperations.TrailingZeroCount(unchecked((uint)value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_AVX2_X64_TrailingZeroCount:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;
                        var result = BitOperations.TrailingZeroCount(unchecked((ulong)value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_PopCount:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = unchecked((int)cnsNode.AsIntConCommon().IconValue);
                        var result = BitOperations.PopCount(unchecked((uint)value));

                        cnsNode.AsIntConCommon().IconValue = (int)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_X64_PopCount:
                    {
                        assert(varTypeIsLong(retType));

                        var value = cnsNode.AsIntConCommon().IntegralValue;
                        var result = BitOperations.PopCount(unchecked((ulong)value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_BitScanForward:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = unchecked((int)cnsNode.AsIntConCommon().IconValue);

                        if (value == 0)
                        {
                            // bsf is undefined for 0
                            break;
                        }
                        var result = BitOperations.TrailingZeroCount(unchecked((uint)value));

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
                        var result = BitOperations.TrailingZeroCount(unchecked((ulong)value));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }

                    case NI_X86Base_BitScanReverse:
                    {
                        assert(!varTypeIsSmall(retType) && !varTypeIsLong(retType));

                        var value = unchecked((int)cnsNode.AsIntConCommon().IconValue);

                        if (value == 0)
                        {
                            // bsr is undefined for 0
                            break;
                        }
                        var result = (31 - BitOperations.LeadingZeroCount(unchecked((uint)value)));

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
                        var result = (63 - BitOperations.LeadingZeroCount(unchecked((ulong)value)));

                        cnsNode.AsIntConCommon().IntegralValue = (long)(result);
                        resultNode = cnsNode;
                        break;
                    }
#endif

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
#if TARGET_XARCH
                            if ((otherNode.Type == TYP_SIMD16))
                            {
                                if (!HWIntrinsicInfo.IsVariableShift(ni))
                                {
                                    // The xarch shift instructions support taking the shift amount as
                                    // a simd16, in which case they take the shift amount from the lower
                                    // 64-bits.

                                    var shiftAmount = otherNode.AsVecCon().GetElementIntegral(TYP_LONG, 0);

                                    if (unchecked((ulong)shiftAmount) >=
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
#elif TARGET_ARM64
                            var auxType = tree.AuxiliaryType;
                            if ((auxType != TYP_UNKNOWN) && (auxType.Size != simdBaseType.Size))
                            {
                                assert(auxType == TYP_ULONG);
                                assert(tree.Type == TYP_SIMD16);
                                otherNode.AsVecCon().SimdVal =
                                    NarrowAndDuplicateSimdLong(simdBaseType, otherNode.AsVecCon().SimdVal);
                            }
#endif
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
                            var index = unchecked((uint)otherNode.AsIntConCommon().IconValue);

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
                                    resultNode = gtNewIconNode(retType, unchecked((int)result));
                                }
                            }
                            break;
                        }

#if TARGET_ARM64
                        case NI_AdvSimd_MultiplyByScalar:
                        case NI_AdvSimd_Arm64_MultiplyByScalar:
                        {
                            otherNode.Type = retType;

                            if (varTypeIsFloating(simdBaseType))
                            {
                                var scalar = otherNode.AsVecCon().GetElementFloating(simdBaseType, 0);
                                otherNode.AsVecCon().EvaluateBroadcastInPlace(simdBaseType, scalar);
                            }
                            else
                            {
                                assert(varTypeIsIntegral(simdBaseType));
                                var scalar = otherNode.AsVecCon().GetElementIntegral(simdBaseType, 0);
                                otherNode.AsVecCon().EvaluateBroadcastInPlace(simdBaseType, scalar);
                            }

                            cnsNode.AsVecCon().EvaluateBinaryInPlace(GT_MUL, isScalar, simdBaseType,
                                otherNode.AsVecCon());
                            resultNode = cnsNode;
                            break;
                        }
#endif

#if TARGET_ARM64
                        case NI_Vector_WithLower:
                        {
                            assert(retType == TYP_SIMD16);
                            assert(cnsNode.Type == TYP_SIMD16);
                            assert(otherNode.Type == TYP_SIMD8);
                            cnsNode.AsVecCon().SimdVal.v64[0] = otherNode.AsVecCon().SimdVal.v64[0];
                            resultNode = cnsNode;
                            break;
                        }

                        case NI_Vector_WithUpper:
                        {
                            assert(retType == TYP_SIMD16);
                            assert(cnsNode.Type == TYP_SIMD16);
                            assert(otherNode.Type == TYP_SIMD8);
                            cnsNode.AsVecCon().SimdVal.v64[1] = otherNode.AsVecCon().SimdVal.v64[0];
                            resultNode = cnsNode;
                            break;
                        }
#elif TARGET_XARCH
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
#endif

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
#if TARGET_ARM64
                    case NI_Sve_ConvertVectorToMask:
                    {
                        resultNode = gtFoldExprConvertVecCnsToMask(tree, cnsNode.AsVecCon());
                        break;
                    }

                    case NI_AdvSimd_MultiplyByScalar:
                    case NI_AdvSimd_Arm64_MultiplyByScalar:
                    {
                        if (!varTypeIsFloating(simdBaseType))
                        {
                            if (cnsNode == op1)
                            {
                                if (cnsNode.IsVectorZero)
                                {
                                    resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                    break;
                                }
                            }
                            else
                            {
                                assert(cnsNode == op2);
                                if (cnsNode.AsVecCon().IsScalarZero(simdBaseType))
                                {
                                    cnsNode.Type = retType;
                                    cnsNode.AsVecCon().EvaluateBroadcastInPlace(simdBaseType, 0L);
                                    resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (cnsNode == op1)
                            {
                                if (cnsNode.IsVectorNaN(simdBaseType))
                                {
                                    resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                    break;
                                }
                            }
                            else
                            {
                                assert(cnsNode == op2);
                                var value = cnsNode.AsVecCon().GetElementFloating(simdBaseType, 0);
                                if (double.IsNaN(value))
                                {
                                    cnsNode.Type = retType;
                                    cnsNode.AsVecCon().EvaluateBroadcastInPlace(simdBaseType, value);
                                    resultNode = gtWrapWithSideEffects(cnsNode, otherNode, GTF_ALL_EFFECT);
                                    break;
                                }
                            }
                        }

                        if ((cnsNode == op2) && cnsNode.AsVecCon().IsScalarOne(simdBaseType))
                        {
                            resultNode = otherNode;
                        }
                        break;
                    }
#endif

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
#if TARGET_XARCH || TARGET_WASM
                case NI_Vector_ConditionalSelect:
#elif TARGET_ARM64
                case NI_AdvSimd_BitwiseSelect:
#endif
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

#if TARGET_ARM64
                case NI_Sve_ConditionalSelect:
                case NI_Sve_ConditionalSelect_Predicates:
                {
                    assert(varTypeIsMask(op1.Type));

                    if (cnsNode != op1)
                    {
                        break;
                    }

                    if (op1.IsTrueMask(simdBaseType))
                    {
                        if ((op3.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0)
                        {
                            break;
                        }
                        return op2;
                    }

                    if (op1.IsMaskZero)
                    {
                        return gtWrapWithSideEffects(op3, op2, GTF_ALL_EFFECT);
                    }

                    if (op2.Oper.IsCnsVec && op3.Oper.IsCnsVec)
                    {
                        assert(ni == NI_Sve_ConditionalSelect);
                        assert(op2.Type == TYP_SIMD16);
                        assert(op3.Type == TYP_SIMD16);

                        simd_t mask = default;
                        EvaluateSimdCvtMaskToVector(simdBaseType, mask.AsSpan<byte>()[..16],
                            op1.AsMskCon().SimdMaskVal);

                        simd_t result = default;
                        EvaluateBinarySimd(GT_AND, false, simdBaseType, result.AsSpan<byte>()[..16],
                            op2.AsVecCon().SimdVal.AsSpan<byte>()[..16], mask.AsSpan<byte>()[..16], 16);
                        op2.AsVecCon().SimdVal = result;

                        result = default;
                        EvaluateBinarySimd(GT_AND_NOT, false, simdBaseType, result.AsSpan<byte>()[..16],
                            op3.AsVecCon().SimdVal.AsSpan<byte>()[..16], mask.AsSpan<byte>()[..16], 16);
                        op3.AsVecCon().SimdVal = result;

                        op2.AsVecCon().EvaluateBinaryInPlace(GT_OR, false, simdBaseType, op3.AsVecCon());
                        resultNode = op2;
                    }
                    else if (op2.Oper.IsCnsMsk && op3.Oper.IsCnsMsk)
                    {
                        assert(ni == NI_Sve_ConditionalSelect_Predicates);

                        op2.AsMskCon().EvaluateBinaryInPlace(GT_AND, false, simdBaseType, simdSize,
                            op1.AsMskCon());
                        op3.AsMskCon().EvaluateBinaryInPlace(GT_AND_NOT, false, simdBaseType, simdSize,
                            op1.AsMskCon());
                        op2.AsMskCon().EvaluateBinaryInPlace(GT_OR, false, simdBaseType, simdSize,
                            op3.AsMskCon());
                        resultNode = op2;
                    }
                    break;
                }
#endif

                case NI_Vector_WithElement:
                {
                    if ((cnsNode != op1) || !op2.Oper.IsCnsIntOrI || !op3.Oper.IsConst)
                    {
                        break;
                    }

                    var index = unchecked((uint)op2.AsIntConCommon().IconValue);

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

#if TARGET_XARCH
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
#endif

                default:
                {
                    break;
                }
            }
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeIsMask(retType) && !varTypeIsMask(resultNode.Type))
        {
            resultNode = gtNewSimdCvtVectorToMaskNode(retType, resultNode, simdBaseType, simdSize);
            return gtFoldExprHWIntrinsic(resultNode.AsHWIntrinsic());
        }
#endif

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
#if TARGET_ARM64
    internal static TSimd NarrowAndDuplicateSimdLong<TSimd>(var_types baseType, in TSimd value)
        where TSimd : unmanaged
    {
        TSimd result = default;
        var source = value;
        var sourceWords = MemoryMarshal.Cast<TSimd, ulong>(MemoryMarshal.CreateReadOnlySpan(ref source, 1));
        var destination = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));

        switch (baseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                for (var index = 0; index < destination.Length; index++)
                {
                    destination[index] = (byte)Math.Min(sourceWords[index / 8], byte.MaxValue);
                }
                break;
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                var elements = MemoryMarshal.Cast<byte, ushort>(destination);
                for (var index = 0; index < elements.Length; index++)
                {
                    elements[index] = (ushort)Math.Min(sourceWords[index / 4], ushort.MaxValue);
                }
                break;
            }

            case TYP_FLOAT:
            case TYP_INT:
            case TYP_UINT:
            {
                var elements = MemoryMarshal.Cast<byte, uint>(destination);
                for (var index = 0; index < elements.Length; index++)
                {
                    elements[index] = (uint)Math.Min(sourceWords[index / 2], uint.MaxValue);
                }
                break;
            }

            case TYP_DOUBLE:
            case TYP_LONG:
            case TYP_ULONG:
            {
                result = value;
                break;
            }

            default:
            {
                unreached();
                throw new System.Diagnostics.UnreachableException();
            }
        }

        return result;
    }

    internal static uint ReverseArm64Bits(uint value)
    {
        // Reverse each byte's 1-, 2-, and 4-bit groups, then reverse byte order.
        value = ((value & 0x5555_5555u) << 1) | ((value >> 1) & 0x5555_5555u);
        value = ((value & 0x3333_3333u) << 2) | ((value >> 2) & 0x3333_3333u);
        value = ((value & 0x0F0F_0F0Fu) << 4) | ((value >> 4) & 0x0F0F_0F0Fu);
        return BinaryPrimitives.ReverseEndianness(value);
    }

    internal static ulong ReverseArm64Bits(ulong value)
    {
        value = ((value & 0x5555_5555_5555_5555UL) << 1) | ((value >> 1) & 0x5555_5555_5555_5555UL);
        value = ((value & 0x3333_3333_3333_3333UL) << 2) | ((value >> 2) & 0x3333_3333_3333_3333UL);
        value = ((value & 0x0F0F_0F0F_0F0F_0F0FUL) << 4) | ((value >> 4) & 0x0F0F_0F0F_0F0F_0F0FUL);
        return BinaryPrimitives.ReverseEndianness(value);
    }
#endif
}

#if TARGET_ARM64
public readonly partial struct HWIntrinsicInfo
{
    public static NamedIntrinsic GetMaskVariant(NamedIntrinsic id)
    {
        assert((lookupFlags(id) & HW_Flag_HasAllMaskVariant) != 0);

        return id switch
        {
            NI_Sve_And => NI_Sve_And_Predicates,
            NI_Sve_BitwiseClear => NI_Sve_BitwiseClear_Predicates,
            NI_Sve_Xor => NI_Sve_Xor_Predicates,
            NI_Sve_Or => NI_Sve_Or_Predicates,
            NI_Sve_ZipHigh => NI_Sve_ZipHigh_Predicates,
            NI_Sve_ZipLow => NI_Sve_ZipLow_Predicates,
            NI_Sve_UnzipOdd => NI_Sve_UnzipOdd_Predicates,
            NI_Sve_UnzipEven => NI_Sve_UnzipEven_Predicates,
            NI_Sve_TransposeEven => NI_Sve_TransposeEven_Predicates,
            NI_Sve_TransposeOdd => NI_Sve_TransposeOdd_Predicates,
            NI_Sve_ReverseElement => NI_Sve_ReverseElement_Predicates,
            NI_Sve_ConditionalSelect => NI_Sve_ConditionalSelect_Predicates,
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }
}
#endif
#endif
