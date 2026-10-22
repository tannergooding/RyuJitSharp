// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class var_typesExtensions
{
    public static int NativeCount => ToNativeOrdinal(TYP_COUNT);

    public static int ToNativeOrdinal(var_types type)
    {
        if (type <= TYP_STRUCT)
        {
            return (int)type;
        }

        if (!Configuration.FEATURE_SIMD)
        {
            switch (type)
            {
                case TYP_UNKNOWN:
                    return 15;
                case TYP_COUNT:
                    return 16;
                default:
                    JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDTypesAreNotEnabledForThisTarget);
                    return default;
            }
        }

        if (Configuration.TARGET_XARCH)
        {
            switch (type)
            {
                case TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 or TYP_SIMD32 or TYP_SIMD64:
                    return (int)type;
                case TYP_MASK:
                    return 20;
                case TYP_UNKNOWN:
                    return 21;
                case TYP_COUNT:
                    return 22;
                default:
                    JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ARM64ScalableVectorTypeIsNotSupportedOnXarch);
                    return default;
            }
        }

        if (Configuration.TARGET_ARM64)
        {
            switch (type)
            {
                case TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16:
                    return (int)type;
                case TYP_SIMD_ARM64:
                    return 18;
                case TYP_MASK:
                    return 19;
                case TYP_UNKNOWN:
                    return 20;
                case TYP_COUNT:
                    return 21;
                default:
                    JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FixedWidth32ByteAnd64ByteVectorsAreNotSupportedOnARM64);
                    return default;
            }
        }

        switch (type)
        {
            case TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16:
                return (int)type;
            case TYP_MASK:
                return 18;
            case TYP_UNKNOWN:
                return 19;
            case TYP_COUNT:
                return 20;
            default:
                JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ArchitectureSpecificSIMDTypesAreNotSupportedForThisTarget);
                return default;
        }
    }

    public static var_types FromNativeOrdinal(int ordinal)
    {
        if ((uint)ordinal <= (uint)TYP_STRUCT)
        {
            return (var_types)ordinal;
        }

        if (!Configuration.FEATURE_SIMD)
        {
            switch (ordinal)
            {
                case 15:
                    return TYP_UNKNOWN;
                case 16:
                    return TYP_COUNT;
                default:
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.NativeVariableTypeOrdinalArgumentIsInvalid, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? ordinal : default));
                    return default;
            }
        }

        if (Configuration.TARGET_XARCH)
        {
            switch (ordinal)
            {
                case 15:
                    return TYP_SIMD8;
                case 16:
                    return TYP_SIMD12;
                case 17:
                    return TYP_SIMD16;
                case 18:
                    return TYP_SIMD32;
                case 19:
                    return TYP_SIMD64;
                case 20:
                    return TYP_MASK;
                case 21:
                    return TYP_UNKNOWN;
                case 22:
                    return TYP_COUNT;
                default:
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.NativeVariableTypeOrdinalArgumentIsInvalid, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? ordinal : default));
                    return default;
            }
        }

        if (Configuration.TARGET_ARM64)
        {
            switch (ordinal)
            {
                case 15:
                    return TYP_SIMD8;
                case 16:
                    return TYP_SIMD12;
                case 17:
                    return TYP_SIMD16;
                case 18:
                    return TYP_SIMD_ARM64;
                case 19:
                    return TYP_MASK;
                case 20:
                    return TYP_UNKNOWN;
                case 21:
                    return TYP_COUNT;
                default:
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.NativeVariableTypeOrdinalArgumentIsInvalid, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? ordinal : default));
                    return default;
            }
        }

        switch (ordinal)
        {
            case 15:
                return TYP_SIMD8;
            case 16:
                return TYP_SIMD12;
            case 17:
                return TYP_SIMD16;
            case 18:
                return TYP_MASK;
            case 19:
                return TYP_UNKNOWN;
            case 20:
                return TYP_COUNT;
            default:
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.NativeVariableTypeOrdinalArgumentIsInvalid, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? ordinal : default));
                return default;
        }
    }

    extension(var_types varType)
    {
        /// <summary>Maps a 'precise' type to an actual type as seen by the VM(for example, 'byte' maps to 'int').</summary>
        /// <returns></returns>
        public var_types ActualType
        {
            get
            {
                assert(s_actualTypes.Length == (int)(TYP_COUNT));
                return s_actualTypes[(int)(varType)];
            }
        }

        public byte Alignment
        {
            get
            {
                var alignments = s_alignments;
                assert(alignments.Length == (int)(TYP_COUNT));
                return alignments[(int)(varType)];
            }
        }

        public var_types_classification Classification
        {
            get
            {
                var classifications = s_classifications;
                assert(classifications.Length == (int)(TYP_COUNT));
                return classifications[(int)(varType)];
            }
        }

        public emitAttr EmitActualSize
        {
            get
            {
                var emitActualSizes = s_emitActualSizes;
                assert(emitActualSizes.Length == (int)(TYP_COUNT));
                return emitActualSizes[(int)(varType)];
            }
        }

        public emitAttr EmitSize
        {
            get
            {
                var emitSizes = s_emitSizes;
                assert(emitSizes.Length == (int)(TYP_COUNT));
                return emitSizes[(int)(varType)];
            }
        }

        public var_types_register Register
        {
            get
            {
                assert(s_registers.Length == (int)(TYP_COUNT));
                return s_registers[(int)(varType)];
            }
        }

        /// <summary>Return the size in bytes of the given type.</summary>
        /// <returns></returns>
        public byte Size
        {
            get
            {
                var sizes = s_sizes;
                assert(sizes.Length == (int)(TYP_COUNT));
                return sizes[(int)(varType)];
            }
        }

        public byte StSz
        {
            get
            {
                if (Configuration.TARGET_ARM64)
                {
                    // The size of these types cannot be evaluated in static contexts.
                    assert(varType is not TYP_SIMD_ARM64 and not TYP_MASK);
                }

                var stackSizes = s_stSzs;
                assert(stackSizes.Length == (int)(TYP_COUNT));
                return stackSizes[(int)(varType)];
            }
        }
    }
}
