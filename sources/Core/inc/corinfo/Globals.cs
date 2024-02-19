// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public unsafe partial class Globals
{
    // For System V on the CLR type system number of registers to pass in and return a struct is the same.
    // The CLR type system allows only up to 2 eight-bytes to be passed in registers. There is no SSEUP classification types.

    public const int CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_PASS_IN_REGISTERS = 2;

    public const int CLR_SYSTEMV_MAX_EIGHTBYTES_COUNT_TO_RETURN_IN_REGISTERS = 2;

    public const int CLR_SYSTEMV_MAX_STRUCT_BYTES_TO_PASS_IN_REGISTERS = 16;

    /// <summary>The maximum number of indirections used by runtime lookups.</summary>
    /// <remarks>This accounts for up to 2 indirections to get at a dictionary followed by a possible spill slot.</remarks>
    public const int CORINFO_MAXINDIRECTIONS = 4;

    public const int CORINFO_USEHELPER = 0xFFFF;

    public const int CORINFO_USENULL = 0xFFFE;

    public const int CORINFO_NO_SIZE_CHECK = 0xFFFF;

    public const int CORINFO_ACCESS_ALLOWED_MAX_ARGS = 4;

    /// <summary>Indicates that the CORINFO_VIRTUALCALL_VTABLE lookup needn't do a chunk indirection</summary>
    public const int CORINFO_VIRTUALCALL_NO_CHUNK = unchecked((int)0xFFFF_FFFF);

    /// <summary>This is used to indicate that a finally has been called "locally" by the try block.</summary>
    public const int LCL_FINALLY_MARK = 0xFC;

    /// <summary>MethTable.</summary>
    public static readonly int SIZEOF__CORINFO_Object = TARGET_POINTER_SIZE;

    public const int CORINFO_Array_MaxLength = 0x7FFFFFC7;

    public const int CORINFO_String_MaxLength = 0x3FFFFFDF;

    public static readonly int OFFSETOF__CORINFO_Array__length = SIZEOF__CORINFO_Object;

    public static readonly int OFFSETOF__CORINFO_Array__data = OFFSETOF__CORINFO_Array__length
                                                             + TARGET_POINTER_SIZE; // length

    public const int OFFSETOF__CORINFO_TypedReference__dataPtr = 0;

    public static readonly int OFFSETOF__CORINFO_TypedReference__type = OFFSETOF__CORINFO_TypedReference__dataPtr
                                                                      + TARGET_POINTER_SIZE; // dataPtr

    public static readonly int OFFSETOF__CORINFO_String__stringLen = SIZEOF__CORINFO_Object;

    public static readonly int OFFSETOF__CORINFO_String__chars = OFFSETOF__CORINFO_String__stringLen
                                                               + sizeof(uint); // stringLen

    public const int OFFSETOF__CORINFO_NullableOfT__hasValue = 0;

    public const int OFFSETOF__CORINFO_Span__reference = 0;

    public static readonly int OFFSETOF__CORINFO_Span__length = TARGET_POINTER_SIZE;

    public const int MAX_EnC_HANDLER_NESTING_LEVEL = 6;

    // It would be nicer to use existing IMAGE_REL_XXX constants instead of defining our own here...
    public const int IMAGE_REL_BASED_REL32 = 0x10;

    public const int IMAGE_REL_BASED_THUMB_BRANCH24  = 0x13;

    public const int IMAGE_REL_SECREL                = 0x104;

    // Linux x64 - GD model
    public const int IMAGE_REL_TLSGD = 0x105;

    // Linux arm64 - TLSDESC  (dynamic)
    public const int IMAGE_REL_AARCH64_TLSDESC_ADR_PAGE21   = 0x107;

    public const int IMAGE_REL_AARCH64_TLSDESC_LD64_LO12    = 0x108;

    public const int IMAGE_REL_AARCH64_TLSDESC_ADD_LO12     = 0x109;

    public const int IMAGE_REL_AARCH64_TLSDESC_CALL = 0x10A;

    // The identifier for ARM32-specific PC-relative address
    // computation corresponds to the following instruction
    // sequence:
    //  l0: movw rX, #imm_lo  // 4 byte
    //  l4: movt rX, #imm_hi  // 4 byte
    //  l8: add  rX, pc <- after this instruction rX = relocTarget
    //
    // Program counter at l8 is address of l8 + 4
    // Address of relocated movw/movt is l0
    // So, imm should be calculated as the following:
    //  imm = relocTarget - (l8 + 4) = relocTarget - (l0 + 8 + 4) = relocTarget - (l_0 + 12)
    // So, the value of offset correction is 12
    //
    public const int IMAGE_REL_BASED_REL_THUMB_MOV32_PCREL = 0x14;

    public static readonly bool USE_PER_FRAME_PINVOKE_INIT = TARGET_64BIT;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CorInfoType strip(CorInfoTypeWithMod val)
    {
        return (CorInfoType)(val & CORINFO_TYPE_MASK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCallerPop(CorInfoCallConvExtension callConv)
    {
        Debug.Assert(TARGET_X86);

        if (UNIX_X86_ABI)
        {
            return callConv is CorInfoCallConvExtension.Managed
                            or CorInfoCallConvExtension.C
                            or CorInfoCallConvExtension.CMemberFunction;
        }
        else
        {
            return callConv is CorInfoCallConvExtension.C
                            or CorInfoCallConvExtension.CMemberFunction;
        }
    }

    /// <summary>Determines whether or not this calling convention is an instance method calling convention.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool callConvIsInstanceMethodCallConv(CorInfoCallConvExtension callConv)
    {
        return callConv is CorInfoCallConvExtension.Thiscall
                        or CorInfoCallConvExtension.CMemberFunction
                        or CorInfoCallConvExtension.StdcallMemberFunction
                        or CorInfoCallConvExtension.FastcallMemberFunction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool dontInline(CorInfoInline val)
    {
        return (val < 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_CONTEXT_HANDLE METHOD_BEING_COMPILED_CONTEXT() => (CORINFO_CONTEXT_HANDLE)1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_CONTEXT_HANDLE MAKE_CLASSCONTEXT(CORINFO_CLASS_HANDLE c) => (CORINFO_CONTEXT_HANDLE)((nuint)c | (nuint)CORINFO_CONTEXTFLAGS_CLASS);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_CONTEXT_HANDLE MAKE_METHODCONTEXT(CORINFO_METHOD_HANDLE m) => (CORINFO_CONTEXT_HANDLE)((nuint)m | (nuint)CORINFO_CONTEXTFLAGS_METHOD);
}
