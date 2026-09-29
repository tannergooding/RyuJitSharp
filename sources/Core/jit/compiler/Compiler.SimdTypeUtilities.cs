// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_SIMD
namespace RyuJitSharp;

public partial class Compiler
{
    public static var_types getUnsignedSimdBaseType(var_types simdBaseType)
    {
        switch (simdBaseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                return TYP_UBYTE;
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                return TYP_USHORT;
            }

            case TYP_INT:
            case TYP_UINT:
            case TYP_FLOAT:
            {
                return TYP_UINT;
            }

            case TYP_LONG:
            case TYP_ULONG:
            case TYP_DOUBLE:
            {
                return TYP_ULONG;
            }

            default:
            {
                unreached();
                return TYP_UNDEF;
            }
        }
    }
}
#endif
