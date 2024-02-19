// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public unsafe partial class Globals
{
    public static readonly bool FEATURE_PUT_STRUCT_ARG_STK = UNIX_X64_ABI || !TARGET_64BIT || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64;

    public static readonly bool MULTIREG_HAS_SECOND_GC_RET = UNIX_X64_ABI || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64;

    // Arm64 Windows supports FEATURE_ARG_SPLIT, note this is different from
    // the official Arm64 ABI.
    // Case: splitting 16 byte struct between x7 and stack
    // LoongArch64's ABI supports FEATURE_ARG_SPLIT which splitting 16 byte struct between a7 and stack.
    public static readonly bool FEATURE_ARG_SPLIT = TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64;

    public const int REGEN_SHORTCUTS = 0;

    public const int REGEN_CALLPAT = 0;

    /// <summary>Did Jit or Inline succeeded?</summary>
    public const int INFO6 = LL_INFO10000;

    /// <summary>NYI stuff.</summary>
    public const int INFO7 = LL_INFO100000;

    /// <summary>Weird failures.</summary>
    public const int INFO8 = LL_INFO1000000;

    public static CORINFO_OBJECT_HANDLE NO_OBJECT_HANDLE => null;

    public static CORINFO_CLASS_HANDLE NO_CLASS_HANDLE => null;

    public static CORINFO_FIELD_HANDLE NO_FIELD_HANDLE => null;

    public static CORINFO_METHOD_HANDLE NO_METHOD_HANDLE => null;

    public const IL_OFFSET BAD_IL_OFFSET = 0xFFFF_FFFF;

    public const uint BAD_VAR_NUM = uint.MaxValue;

    public const ushort BAD_LCL_OFFSET = ushort.MaxValue;

    // For the following specially handled FIELD_HANDLES we need
    //   values that are negative and have the low two bits zero
    // See eeFindJitDataOffs and eeGetJitDataOffs in Compiler.hpp
    public static CORINFO_FIELD_HANDLE FLD_GLOBAL_DS => (CORINFO_FIELD_HANDLE)(-4);

    public static CORINFO_FIELD_HANDLE FLD_GLOBAL_FS => (CORINFO_FIELD_HANDLE)(-8);

    public static CORINFO_FIELD_HANDLE FLD_GLOBAL_GS => (CORINFO_FIELD_HANDLE)(-12);
}
