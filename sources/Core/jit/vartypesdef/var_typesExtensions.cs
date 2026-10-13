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
            return type switch {
                TYP_UNKNOWN => 15,
                TYP_COUNT => 16,
                _ => throw new PlatformNotSupportedException("SIMD types are not enabled for this target."),
            };
        }

        if (Configuration.TARGET_XARCH)
        {
            return type switch {
                TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 or TYP_SIMD32 or TYP_SIMD64 => (int)type,
                TYP_MASK => 20,
                TYP_UNKNOWN => 21,
                TYP_COUNT => 22,
                _ => throw new PlatformNotSupportedException(
                    "The ARM64 scalable-vector type is not supported on xarch."),
            };
        }

        if (Configuration.TARGET_ARM64)
        {
            return type switch {
                TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 => (int)type,
                TYP_SIMD_ARM64 => 18,
                TYP_MASK => 19,
                TYP_UNKNOWN => 20,
                TYP_COUNT => 21,
                _ => throw new PlatformNotSupportedException(
                    "Fixed-width 32-byte and 64-byte vectors are not supported on ARM64."),
            };
        }

        return type switch {
            TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 => (int)type,
            TYP_MASK => 18,
            TYP_UNKNOWN => 19,
            TYP_COUNT => 20,
            _ => throw new PlatformNotSupportedException(
                "Architecture-specific SIMD types are not supported for this target."),
        };
    }

    public static var_types FromNativeOrdinal(int ordinal)
    {
        if ((uint)ordinal <= (uint)TYP_STRUCT)
        {
            return (var_types)ordinal;
        }

        if (!Configuration.FEATURE_SIMD)
        {
            return ordinal switch {
                15 => TYP_UNKNOWN,
                16 => TYP_COUNT,
                _ => throw new InvalidOperationException($"Native variable type ordinal {ordinal} is invalid."),
            };
        }

        if (Configuration.TARGET_XARCH)
        {
            return ordinal switch {
                15 => TYP_SIMD8,
                16 => TYP_SIMD12,
                17 => TYP_SIMD16,
                18 => TYP_SIMD32,
                19 => TYP_SIMD64,
                20 => TYP_MASK,
                21 => TYP_UNKNOWN,
                22 => TYP_COUNT,
                _ => throw new InvalidOperationException($"Native variable type ordinal {ordinal} is invalid."),
            };
        }

        if (Configuration.TARGET_ARM64)
        {
            return ordinal switch {
                15 => TYP_SIMD8,
                16 => TYP_SIMD12,
                17 => TYP_SIMD16,
                18 => TYP_SIMD_ARM64,
                19 => TYP_MASK,
                20 => TYP_UNKNOWN,
                21 => TYP_COUNT,
                _ => throw new InvalidOperationException($"Native variable type ordinal {ordinal} is invalid."),
            };
        }

        return ordinal switch {
            15 => TYP_SIMD8,
            16 => TYP_SIMD12,
            17 => TYP_SIMD16,
            18 => TYP_MASK,
            19 => TYP_UNKNOWN,
            20 => TYP_COUNT,
            _ => throw new InvalidOperationException($"Native variable type ordinal {ordinal} is invalid."),
        };
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

#if DEBUG
        public string Name
        {
            get
            {
                assert(s_names.Length == (int)(TYP_COUNT));
                return s_names[(int)(varType)];
            }
        }
#else
        public string Name => varType.ToString();
#endif

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
