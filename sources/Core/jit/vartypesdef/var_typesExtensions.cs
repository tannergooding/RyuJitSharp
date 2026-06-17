// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class var_typesExtensions
{
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
                assert(s_alignments.Length == (int)(TYP_COUNT));
                return s_alignments[(int)(varType)];
            }
        }

        public var_types_classification Classification
        {
            get
            {
                assert(s_classifications.Length == (int)(TYP_COUNT));
                return s_classifications[(int)(varType)];
            }
        }

        public emitAttr EmitActualSize
        {
            get
            {
                assert(s_emitActualSizes.Length == (int)(TYP_COUNT));
                return s_emitActualSizes[(int)(varType)];
            }
        }

        public emitAttr EmitSize
        {
            get
            {
                assert(s_emitSizes.Length == (int)(TYP_COUNT));
                return s_emitSizes[(int)(varType)];
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
                assert(s_sizes.Length == (int)(TYP_COUNT));
                return s_sizes[(int)(varType)];
            }
        }

        public byte StSz
        {
            get
            {
#if TARGET_ARM64
                // The size of these types cannot be evaluated in static contexts.
                assert(varType is not TYP_SIMD and not TYP_MASK);
#endif

                assert(s_stSzs.Length == (int)(TYP_COUNT));
                return s_stSzs[(int)(varType)];
            }
        }
    }
}
