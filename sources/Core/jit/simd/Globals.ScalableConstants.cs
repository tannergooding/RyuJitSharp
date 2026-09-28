// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && FEATURE_SIMD
using System;
using static RyuJitSharp.SimdScalableKind;

namespace RyuJitSharp;

public static partial class Globals
{
    public static bool TryEvaluateUnarySimdScalable(genTreeOps oper, bool scalar, var_types baseType,
        out simdscalable_t result, in simdscalable_t arg0)
    {
        result = default;
        var elementSize = baseType.Size;
        if ((oper == GT_LZCNT) && (elementSize < sizeof(uint)))
        {
            return false;
        }

        var index = arg0.Index;
        var step = arg0.Step;
        simd8_t resultIndex = default;
        simd8_t resultStep = default;
        simd8_t zero = default;

        // A one-element span reuses the scalar type dispatch, including raw-bit floating operations.
        EvaluateUnarySimd(oper, false, baseType,
            resultIndex.AsSpan<byte>()[..elementSize], index.AsSpan<byte>()[..elementSize]);

        SimdScalableKind resultKind;
        if (scalar)
        {
            resultKind = SimdScalableScalar;
        }
        else if (arg0.IsZero)
        {
            resultKind = SimdScalableRepeated;
        }
        else
        {
            switch (arg0.Kind)
            {
                case SimdScalableRepeated:
                {
                    resultKind = SimdScalableRepeated;
                    break;
                }

                case SimdScalableSequence:
                {
                    if (step.AsSpan<byte>()[..elementSize].SequenceEqual(zero.AsSpan<byte>()[..elementSize]))
                    {
                        resultKind = SimdScalableRepeated;
                        break;
                    }

                    if ((oper != GT_NEG) && ((oper != GT_NOT) || varTypeIsFloating(baseType)))
                    {
                        return false;
                    }

                    // Native computes zero - step, not a sign-bit toggle for floating NaNs and zero.
                    EvaluateBinarySimd(GT_SUB, false, baseType, resultStep.AsSpan<byte>()[..elementSize],
                        zero.AsSpan<byte>()[..elementSize], step.AsSpan<byte>()[..elementSize], elementSize);
                    resultKind = SimdScalableSequence;
                    break;
                }

                case SimdScalableScalar:
                {
                    simd8_t upperValue = default;
                    EvaluateUnarySimd(oper, false, baseType,
                        upperValue.AsSpan<byte>()[..elementSize], zero.AsSpan<byte>()[..elementSize]);
                    if (!upperValue.AsSpan<byte>()[..elementSize].SequenceEqual(zero.AsSpan<byte>()[..elementSize]))
                    {
                        return false;
                    }

                    resultKind = SimdScalableScalar;
                    break;
                }

                default:
                {
                    unreached();
                    throw new FatalJitException("Unexpected scalable vector constant kind.");
                }
            }
        }

        result = new simdscalable_t {
            BaseType = baseType,
            Kind = resultKind,
            Index = resultIndex,
            Step = resultStep,
        };

        return true;
    }
}
#endif
