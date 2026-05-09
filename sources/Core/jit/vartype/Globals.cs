// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_64BIT
    public const var_types TYP_I_IMPL = TYP_LONG;

    public const var_types TYP_U_IMPL = TYP_ULONG;
#else
    public const var_types TYP_I_IMPL = TYP_INT;

    public const var_types TYP_U_IMPL = TYP_UINT;
#endif

    public const byte SIZE_UNKNOWN = byte.MaxValue;

    public const emitAttr GCS = EA_GCREF;

    public const emitAttr BRS = EA_BYREF;

    public const emitAttr EPS = EA_PTRSIZE;

    public const emitAttr EAU = EA_UNKNOWN;

    public const byte SZU = SIZE_UNKNOWN;

    public const byte PS = TARGET_POINTER_SIZE;

    public const byte PST = TARGET_POINTER_SIZE / sizeof(int);

#if TARGET_64BIT
    public const var_types_classification VTF_I32 = VTF_ANY;

    public const var_types_classification VTF_I64 = VTF_I;
#else
    public const var_types_classification VTF_I32 = VTF_I;

    public const var_types_classification VTF_I64 = VTF_ANY;
#endif

    public static bool varTypeIsSimd(var_types v)
    {
#if FEATURE_SIMD
        return (v.Classification & VTF_VEC) != 0;
#else
        return false;
#endif
    }

    public static bool varTypeIsMask(var_types v)
    {
#if FEATURE_MASKED_HW_INTRINSICS
        return v == TYP_MASK;
#else
        return false;
#endif
    }

    public static bool varTypeIsSimdOrMask(var_types vt) => varTypeIsSimd(vt) || varTypeIsMask(vt);

    public static bool varTypeIsIntegral(var_types vt) => (vt.Classification & VTF_INT) != 0;

    public static bool varTypeIsIntegralOrI(var_types vt) => (vt.Classification & (VTF_INT | VTF_I)) != 0;

    public static bool varTypeIsUnsigned(var_types vt) => (vt.Classification & VTF_UNS) != 0;

    public static bool varTypeIsSigned(var_types vt) => varTypeIsIntegralOrI(vt) && !varTypeIsUnsigned(vt);

    // If "vt" represents an unsigned integral type, returns the corresponding signed integral type, otherwise returns the original type.
    public static var_types varTypeToSigned(var_types vt) => varTypeIsUnsigned(vt) ? (vt - 1) : vt;

    // If "vt" represents a signed integral type, returns the corresponding unsigned integral type, otherwise returns the original type.
    public static var_types varTypeToUnsigned(var_types vt) => (varTypeIsIntegral(vt) && !varTypeIsUnsigned(vt)) ? (vt + 1) : vt;

    public static bool varTypeIsFloating(var_types vt) => (vt.Classification & VTF_FLT) != 0;

    public static bool varTypeIsArithmetic(var_types vt) => (vt.Classification & (VTF_INT | VTF_FLT)) != 0;

    public static bool varTypeIsGC(var_types vt) => vt is TYP_REF or TYP_BYREF;

    public static bool varTypeIsI(var_types vt) => (vt.Classification & VTF_I) != 0;

    public static bool varTypeIsEnregisterable(var_types vt) => vt != TYP_STRUCT;

    public static bool varTypeIsByte(var_types vt) => vt is TYP_BYTE or TYP_UBYTE;

    public static bool varTypeIsShort(var_types vt) => vt is TYP_SHORT or TYP_USHORT;

    public static bool varTypeIsSmall(var_types vt) => vt is >= TYP_BYTE and <= TYP_USHORT;

    public static bool varTypeIsIntOrI(var_types vt)
    {
#if TARGET_64BIT
        return vt is TYP_INT or TYP_I_IMPL;
#else
        return vt is TYP_INT;
#endif
    }

    public static bool genActualTypeIsInt(var_types vt) => vt is >= TYP_BYTE && vt <= TYP_UINT;

    public static bool genActualTypeIsIntOrI(var_types vt) => vt is >= TYP_BYTE && vt <= TYP_U_IMPL;

    public static bool varTypeIsLong(var_types vt) => vt is TYP_LONG or TYP_ULONG;

    public static bool varTypeIsInt(var_types vt) => vt is TYP_INT or TYP_UINT;

    public static bool varTypeIsMultiReg(var_types vt)
    {
#if TARGET_64BIT
        return false;
#else
        return vt is TYP_LONG;
#endif
    }

    public static bool varTypeIsSingleReg(var_types vt)
    {
#if TARGET_64BIT
        return true;
#else
        return vt is not TYP_LONG;
#endif
    }

    public static bool varTypeIsComposite(var_types vt) => !varTypeIsArithmetic(vt) && (vt != TYP_VOID);

    // Is this type promotable?
    // In general only structs are promotable.
    // However, a SIMD type, e.g. TYP_SIMD may be handled as either a struct, OR a
    // fully-promoted register type.
    // On 32-bit systems longs are split into an upper and lower half, and they are
    // handled as if they are structs with two integer fields.
    public static bool varTypeIsPromotable(var_types vt)
    {
#if TARGET_32BIT
        return varTypeIsStruct(vt) || varTypeIsLong(vt);
#else
        return varTypeIsStruct(vt);
#endif
    }

    public static bool varTypeIsStruct(var_types vt) => (vt.Classification & VTF_S) != 0;

    public static bool varTypeUsesSameRegType(var_types vt, var_types vu) => vt.Register == vu.Register;

    public static bool varTypeUsesIntReg(var_types vt) => vt.Register == VTR_INT;

    public static bool varTypeUsesFloatReg(var_types vt) => vt.Register == VTR_FLOAT;

    public static bool varTypeUsesMaskReg(var_types vt)
    {
        // The technically correct check is:
        //     return GetRegister(vt) == VTR_MASK;
        //
        // However, we only have one type that uses VTR_MASK today
        // and so its quite a bit cheaper to just check that directly

#if FEATURE_MASKED_HW_INTRINSICS
        assert((vt == TYP_MASK) || (vt.Register != VTR_MASK));
        return vt == TYP_MASK;
#else
        assert(GetRegister(vt) != VTR_MASK);
        return false;
#endif
    }

    public static bool varTypeUsesFloatArgReg(var_types vt)
    {
#if TARGET_ARM64
        // Arm64 passes SIMD types in floating point registers.
        // Exception: Windows arm64 native varargs passes them using general-purpose (integer) registers or
        // by value on the stack, or split between registers and stack.
        return varTypeUsesFloatReg(vt);
#else
        // Other targets pass them as regular structs - by reference or by value.
        return varTypeIsFloating(vt);
#endif
    }

    /// <summary>Determine if the type is a valid HFA type</summary>
    /// <param name="vt">the type of interest</param>
    /// <returns>Returns true iff the type is a valid HFA type.</returns>
    /// <remarks>
    ///   <para>This should only be called with the return value from GetHfaType().</para>
    ///   <para>The only valid values are TYP_UNDEF, for which this returns false, TYP_FLOAT, TYP_DOUBLE, or (ARM64-only) TYP_SIMD*.</para>
    /// </remarks>
    public static bool varTypeIsValidHfaType(var_types vt)
    {
        if (GlobalJitOptions.compFeatureHfa)
        {
            assert((vt == TYP_UNDEF) || varTypeUsesFloatArgReg(vt));
            return vt != TYP_UNDEF;
        }
        else
        {
            return false;
        }
    }

    /// <summary>Determine whether the type has an unknown size</summary>
    /// <param name="vt">the type of interest</param>
    /// <returns>Returns true iff the type has size equal to SIZE_UNKNOWN</returns>
    public static bool varTypeHasUnknownSize(var_types vt) => vt.Size == SIZE_UNKNOWN;
}
