// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerFusedMultiplyOp(GenTreeHWIntrinsic node)
    {
#if TARGET_XARCH
        assert(node.Operands.Length == 3);
        var negated = false;
        var subtract = false;
        var isScalar = false;
        var intrinsic = node.HWIntrinsicId;

        switch (intrinsic)
        {
            case NI_AVX2_MultiplyAdd:
            case NI_AVX512_FusedMultiplyAdd:
            {
                break;
            }

            case NI_AVX2_MultiplyAddScalar:
            case NI_AVX512_FusedMultiplyAddScalar:
            {
                isScalar = true;
                break;
            }

            case NI_AVX2_MultiplyAddNegated:
            case NI_AVX512_FusedMultiplyAddNegated:
            {
                negated = true;
                break;
            }

            case NI_AVX2_MultiplyAddNegatedScalar:
            case NI_AVX512_FusedMultiplyAddNegatedScalar:
            {
                negated = true;
                isScalar = true;
                break;
            }

            case NI_AVX2_MultiplySubtract:
            case NI_AVX512_FusedMultiplySubtract:
            {
                subtract = true;
                break;
            }

            case NI_AVX2_MultiplySubtractScalar:
            case NI_AVX512_FusedMultiplySubtractScalar:
            {
                subtract = true;
                isScalar = true;
                break;
            }

            case NI_AVX2_MultiplySubtractNegated:
            case NI_AVX512_FusedMultiplySubtractNegated:
            {
                subtract = true;
                negated = true;
                break;
            }

            case NI_AVX2_MultiplySubtractNegatedScalar:
            case NI_AVX512_FusedMultiplySubtractNegatedScalar:
            {
                subtract = true;
                negated = true;
                isScalar = true;
                break;
            }

            default:
            {
                throw new FatalJitException("Fused multiply lowering requires an FMA intrinsic.");
            }
        }

        Span<bool> negatedArgs = stackalloc bool[3];
        negatedArgs.Clear();
        for (var index = 1; index <= 3; index++)
        {
            var arg = node.GetOp(index);
            if (isScalar && (arg.Oper is GT_NEG))
            {
                // Scalar CreateScalarUnsafe wrappers are already removed, exposing bare negations.
                var operand = arg.AsUnOp().Op1;
                BlockRange().Remove(arg);
                operand.IsContained = false;
                node.SetOp(index, operand);
                negatedArgs[index - 1] ^= true;
                continue;
            }

            if (isScalar && (index == 1))
            {
                // Scalar FMA copies the first operand's upper lanes, including their negation.
                continue;
            }

            if (!arg.Oper.IsHWIntrinsic)
            {
                continue;
            }

            var hwArg = arg.AsHWIntrinsic();
            if (hwArg.GetOperForHWIntrinsicId(out _) is not GT_XOR)
            {
                continue;
            }

            var mask = hwArg.GetOp(2);
            if (!mask.IsContained)
            {
                continue;
            }

            // XOR is bitwise: interpret its mask using the FMA type, not the XOR's base type.
            if (mask.IsVectorNegativeZero(node.SimdBaseType))
            {
                BlockRange().Remove(hwArg);
                BlockRange().Remove(mask);
                var operand = hwArg.GetOp(1);
                operand.IsContained = false;
                node.SetOp(index, operand);
                negatedArgs[index - 1] ^= true;
            }
        }

        negated ^= negatedArgs[0];
        negated ^= negatedArgs[1];
        subtract ^= negatedArgs[2];

        if (intrinsic >= FIRST_NI_AVX512)
        {
            if (negated)
            {
                if (subtract)
                {
                    intrinsic = isScalar ? NI_AVX512_FusedMultiplySubtractNegatedScalar : NI_AVX512_FusedMultiplySubtractNegated;
                }
                else
                {
                    intrinsic = isScalar ? NI_AVX512_FusedMultiplyAddNegatedScalar : NI_AVX512_FusedMultiplyAddNegated;
                }
            }
            else if (subtract)
            {
                intrinsic = isScalar ? NI_AVX512_FusedMultiplySubtractScalar : NI_AVX512_FusedMultiplySubtract;
            }
            else
            {
                intrinsic = isScalar ? NI_AVX512_FusedMultiplyAddScalar : NI_AVX512_FusedMultiplyAdd;
            }
        }
        else if (negated)
        {
            if (subtract)
            {
                intrinsic = isScalar ? NI_AVX2_MultiplySubtractNegatedScalar : NI_AVX2_MultiplySubtractNegated;
            }
            else
            {
                intrinsic = isScalar ? NI_AVX2_MultiplyAddNegatedScalar : NI_AVX2_MultiplyAddNegated;
            }
        }
        else if (subtract)
        {
            intrinsic = isScalar ? NI_AVX2_MultiplySubtractScalar : NI_AVX2_MultiplySubtract;
        }
        else
        {
            intrinsic = isScalar ? NI_AVX2_MultiplyAddScalar : NI_AVX2_MultiplyAdd;
        }

        node.ChangeHWIntrinsicId(intrinsic);
#else
        throw new NotImplementedException("Non-xarch fused multiply lowering is not ported.");
#endif
    }
}
