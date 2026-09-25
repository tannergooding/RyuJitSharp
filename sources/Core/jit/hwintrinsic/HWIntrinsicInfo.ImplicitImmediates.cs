// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using FloatComparisonMode = System.Runtime.Intrinsics.X86.FloatComparisonMode;

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static int lookupIval(Compiler compiler, NamedIntrinsic intrinsic, var_types baseType)
    {
        switch (intrinsic)
        {
            case NI_X86Base_CompareEqual:
            case NI_X86Base_CompareScalarEqual:
            case NI_AVX_CompareEqual:
            case NI_AVX512_CompareEqualMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    return (int)FloatComparisonMode.OrderedEqualNonSignaling;
                }
                // Integer equality has dedicated instructions with no immediate.
                break;
            }

            case NI_X86Base_CompareGreaterThan:
            case NI_X86Base_CompareScalarGreaterThan:
            case NI_AVX_CompareGreaterThan:
            case NI_AVX512_CompareGreaterThanMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    // Without AVX, lowering swaps operands and changes the intrinsic ID.
                    assert(compiler.compIsaSupportedDebugOnly(InstructionSet_AVX));
                    return (int)FloatComparisonMode.OrderedGreaterThanSignaling;
                }
                if ((intrinsic == NI_AVX512_CompareGreaterThanMask) && varTypeIsUnsigned(baseType))
                {
                    return (int)IntComparisonMode.GreaterThan;
                }
                break;
            }

            case NI_X86Base_CompareLessThan:
            case NI_X86Base_CompareScalarLessThan:
            case NI_AVX_CompareLessThan:
            case NI_AVX512_CompareLessThanMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    return (int)FloatComparisonMode.OrderedLessThanSignaling;
                }
                if (intrinsic == NI_AVX512_CompareLessThanMask)
                {
                    return (int)IntComparisonMode.LessThan;
                }
                break;
            }

            case NI_X86Base_CompareGreaterThanOrEqual:
            case NI_X86Base_CompareScalarGreaterThanOrEqual:
            case NI_AVX_CompareGreaterThanOrEqual:
            case NI_AVX512_CompareGreaterThanOrEqualMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    assert(compiler.compIsaSupportedDebugOnly(InstructionSet_AVX));
                    return (int)FloatComparisonMode.OrderedGreaterThanOrEqualSignaling;
                }
                assert(intrinsic == NI_AVX512_CompareGreaterThanOrEqualMask);
                return (int)IntComparisonMode.GreaterThanOrEqual;
            }

            case NI_X86Base_CompareLessThanOrEqual:
            case NI_X86Base_CompareScalarLessThanOrEqual:
            case NI_AVX_CompareLessThanOrEqual:
            case NI_AVX512_CompareLessThanOrEqualMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    return (int)FloatComparisonMode.OrderedLessThanOrEqualSignaling;
                }
                assert(intrinsic == NI_AVX512_CompareLessThanOrEqualMask);
                return (int)IntComparisonMode.LessThanOrEqual;
            }

            case NI_X86Base_CompareNotEqual:
            case NI_X86Base_CompareScalarNotEqual:
            case NI_AVX_CompareNotEqual:
            case NI_AVX512_CompareNotEqualMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    return (int)FloatComparisonMode.UnorderedNotEqualNonSignaling;
                }
                assert(intrinsic == NI_AVX512_CompareNotEqualMask);
                return (int)IntComparisonMode.NotEqual;
            }

            case NI_X86Base_CompareNotGreaterThan:
            case NI_X86Base_CompareScalarNotGreaterThan:
            case NI_AVX_CompareNotGreaterThan:
            case NI_AVX512_CompareNotGreaterThanMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    assert(compiler.compIsaSupportedDebugOnly(InstructionSet_AVX));
                    return (int)FloatComparisonMode.UnorderedNotGreaterThanSignaling;
                }
                assert(intrinsic == NI_AVX512_CompareNotGreaterThanMask);
                return (int)IntComparisonMode.LessThanOrEqual;
            }

            case NI_X86Base_CompareNotLessThan:
            case NI_X86Base_CompareScalarNotLessThan:
            case NI_AVX_CompareNotLessThan:
            case NI_AVX512_CompareNotLessThanMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    return (int)FloatComparisonMode.UnorderedNotLessThanSignaling;
                }
                assert(intrinsic == NI_AVX512_CompareNotLessThanMask);
                return (int)IntComparisonMode.GreaterThanOrEqual;
            }

            case NI_X86Base_CompareNotGreaterThanOrEqual:
            case NI_X86Base_CompareScalarNotGreaterThanOrEqual:
            case NI_AVX_CompareNotGreaterThanOrEqual:
            case NI_AVX512_CompareNotGreaterThanOrEqualMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    assert(compiler.compIsaSupportedDebugOnly(InstructionSet_AVX));
                    return (int)FloatComparisonMode.UnorderedNotGreaterThanOrEqualSignaling;
                }
                assert(intrinsic == NI_AVX512_CompareNotGreaterThanOrEqualMask);
                return (int)IntComparisonMode.LessThan;
            }

            case NI_X86Base_CompareNotLessThanOrEqual:
            case NI_X86Base_CompareScalarNotLessThanOrEqual:
            case NI_AVX_CompareNotLessThanOrEqual:
            case NI_AVX512_CompareNotLessThanOrEqualMask:
            {
                if (varTypeIsFloating(baseType))
                {
                    return (int)FloatComparisonMode.UnorderedNotLessThanOrEqualSignaling;
                }
                assert(intrinsic == NI_AVX512_CompareNotLessThanOrEqualMask);
                return (int)IntComparisonMode.GreaterThan;
            }

            case NI_X86Base_CompareOrdered:
            case NI_X86Base_CompareScalarOrdered:
            case NI_AVX_CompareOrdered:
            case NI_AVX512_CompareOrderedMask:
            {
                assert(varTypeIsFloating(baseType));
                return (int)FloatComparisonMode.OrderedNonSignaling;
            }

            case NI_X86Base_CompareUnordered:
            case NI_X86Base_CompareScalarUnordered:
            case NI_AVX_CompareUnordered:
            case NI_AVX512_CompareUnorderedMask:
            {
                assert(varTypeIsFloating(baseType));
                return (int)FloatComparisonMode.UnorderedNonSignaling;
            }

            case NI_X86Base_Ceiling:
            case NI_X86Base_CeilingScalar:
            case NI_AVX_Ceiling:
            case NI_X86Base_RoundToPositiveInfinity:
            case NI_X86Base_RoundToPositiveInfinityScalar:
            case NI_AVX_RoundToPositiveInfinity:
            {
                assert(varTypeIsFloating(baseType));
                return (int)FloatRoundingMode.ToPositiveInfinity;
            }

            case NI_X86Base_Floor:
            case NI_X86Base_FloorScalar:
            case NI_AVX_Floor:
            case NI_X86Base_RoundToNegativeInfinity:
            case NI_X86Base_RoundToNegativeInfinityScalar:
            case NI_AVX_RoundToNegativeInfinity:
            {
                assert(varTypeIsFloating(baseType));
                return (int)FloatRoundingMode.ToNegativeInfinity;
            }

            case NI_X86Base_RoundCurrentDirection:
            case NI_X86Base_RoundCurrentDirectionScalar:
            case NI_AVX_RoundCurrentDirection:
            {
                assert(varTypeIsFloating(baseType));
                return (int)FloatRoundingMode.CurrentDirection;
            }

            case NI_X86Base_RoundToNearestInteger:
            case NI_X86Base_RoundToNearestIntegerScalar:
            case NI_AVX_RoundToNearestInteger:
            {
                assert(varTypeIsFloating(baseType));
                return (int)FloatRoundingMode.ToNearestInteger;
            }

            case NI_X86Base_RoundToZero:
            case NI_X86Base_RoundToZeroScalar:
            case NI_AVX_RoundToZero:
            {
                assert(varTypeIsFloating(baseType));
                return (int)FloatRoundingMode.ToZero;
            }
        }

        return -1;
    }
}
#endif
