// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
using System.Runtime.Intrinsics.X86;

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static NamedIntrinsic lookupIdForFloatComparisonMode(
        NamedIntrinsic intrinsic, FloatComparisonMode comparison, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsFloating(simdBaseType));
        assert((simdSize == 16) || (simdSize == 32) || (simdSize == 64));

        // .NET doesn't differentiate between signalling and non-signalling
        // we disable IEEE 754 exceptions on startup and make it undefined
        // behavior to enable it, so we'll just normalize to the same form

        switch (comparison)
        {
            case FloatComparisonMode.OrderedEqualNonSignaling:
            case FloatComparisonMode.OrderedEqualSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareEqualMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarEqual;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareEqual;
                }
                return NI_X86Base_CompareEqual;
            }

            case FloatComparisonMode.OrderedGreaterThanSignaling:
            case FloatComparisonMode.OrderedGreaterThanNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareGreaterThanMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarGreaterThan;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareGreaterThan;
                }
                return NI_X86Base_CompareGreaterThan;
            }

            case FloatComparisonMode.OrderedGreaterThanOrEqualSignaling:
            case FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareGreaterThanOrEqualMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarGreaterThanOrEqual;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareGreaterThanOrEqual;
                }
                return NI_X86Base_CompareGreaterThanOrEqual;
            }

            case FloatComparisonMode.OrderedLessThanSignaling:
            case FloatComparisonMode.OrderedLessThanNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareLessThanMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarLessThan;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareLessThan;
                }
                return NI_X86Base_CompareLessThan;
            }

            case FloatComparisonMode.OrderedLessThanOrEqualSignaling:
            case FloatComparisonMode.OrderedLessThanOrEqualNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareLessThanOrEqualMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarLessThanOrEqual;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareLessThanOrEqual;
                }
                return NI_X86Base_CompareLessThanOrEqual;
            }

            case FloatComparisonMode.UnorderedNotEqualNonSignaling:
            case FloatComparisonMode.UnorderedNotEqualSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareNotEqualMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarNotEqual;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareNotEqual;
                }
                return NI_X86Base_CompareNotEqual;
            }

            case FloatComparisonMode.UnorderedNotGreaterThanSignaling:
            case FloatComparisonMode.UnorderedNotGreaterThanNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareNotGreaterThanMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarNotGreaterThan;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareNotGreaterThan;
                }
                return NI_X86Base_CompareNotGreaterThan;
            }

            case FloatComparisonMode.UnorderedNotGreaterThanOrEqualSignaling:
            case FloatComparisonMode.UnorderedNotGreaterThanOrEqualNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareNotGreaterThanOrEqualMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarNotGreaterThanOrEqual;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareNotGreaterThanOrEqual;
                }
                return NI_X86Base_CompareNotGreaterThanOrEqual;
            }

            case FloatComparisonMode.UnorderedNotLessThanSignaling:
            case FloatComparisonMode.UnorderedNotLessThanNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareNotLessThanMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarNotLessThan;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareNotLessThan;
                }
                return NI_X86Base_CompareNotLessThan;
            }

            case FloatComparisonMode.UnorderedNotLessThanOrEqualSignaling:
            case FloatComparisonMode.UnorderedNotLessThanOrEqualNonSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareNotLessThanOrEqualMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarNotLessThanOrEqual;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareNotLessThanOrEqual;
                }
                return NI_X86Base_CompareNotLessThanOrEqual;
            }

            case FloatComparisonMode.OrderedNonSignaling:
            case FloatComparisonMode.OrderedSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareOrderedMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarOrdered;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareOrdered;
                }
                return NI_X86Base_CompareOrdered;
            }

            case FloatComparisonMode.UnorderedNonSignaling:
            case FloatComparisonMode.UnorderedSignaling:
            {
                if (intrinsic == NI_AVX512_CompareMask)
                {
                    return NI_AVX512_CompareUnorderedMask;
                }
                else if (intrinsic == NI_AVX_CompareScalar)
                {
                    return NI_X86Base_CompareScalarUnordered;
                }

                assert(intrinsic == NI_AVX_Compare);

                if (simdSize == 32)
                {
                    return NI_AVX_CompareUnordered;
                }
                return NI_X86Base_CompareUnordered;
            }

            default:
            {
                return intrinsic;
            }
        }
    }
}
#endif
