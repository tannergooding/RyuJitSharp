// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.StructFloatFieldInfoFlags;
using System;

namespace RyuJitSharp;

/// <summary>Used on LoongArch64 architecture by <c>getLoongArch64PassStructInRegisterFlags</c> and <c>getRISCV64PassStructInRegisterFlags</c> API to convey struct argument passing information.</summary>
[Flags]
public enum StructFloatFieldInfoFlags
{
    // For structs with no more than two fields and a total struct size no larger
    // than two pointers:
    //
    // The lowest four bits denote the floating-point info:
    //   bit 0: `1` means there is only one float or double field within the struct.
    //   bit 1: `1` means only the first field is floating-point type.
    //   bit 2: `1` means only the second field is floating-point type.
    //   bit 3: `1` means the two fields are both floating-point type.
    // The bits[5:4] denoting whether the field size is 8-bytes:
    //   bit 4: `1` means the first field's size is 8.
    //   bit 5: `1` means the second field's size is 8.
    //
    // Note that bit 0 and 3 cannot both be set.

    /// <summary>Structs are not passed using the float register(s).</summary>
    STRUCT_NO_FLOAT_FIELD = 0x0,

    STRUCT_FLOAT_FIELD_ONLY_ONE = 0x1,

    STRUCT_FLOAT_FIELD_ONLY_TWO = 0x8,

    STRUCT_FLOAT_FIELD_FIRST = 0x2,

    STRUCT_FLOAT_FIELD_SECOND = 0x4,

    STRUCT_FIRST_FIELD_SIZE_IS8 = 0x10,

    STRUCT_SECOND_FIELD_SIZE_IS8 = 0x20,

    STRUCT_FIRST_FIELD_DOUBLE = (STRUCT_FLOAT_FIELD_FIRST | STRUCT_FIRST_FIELD_SIZE_IS8),

    STRUCT_SECOND_FIELD_DOUBLE = (STRUCT_FLOAT_FIELD_SECOND | STRUCT_SECOND_FIELD_SIZE_IS8),

    STRUCT_FIELD_TWO_DOUBLES = (STRUCT_FIRST_FIELD_SIZE_IS8 | STRUCT_SECOND_FIELD_SIZE_IS8 | STRUCT_FLOAT_FIELD_ONLY_TWO),

    STRUCT_MERGE_FIRST_SECOND = (STRUCT_FLOAT_FIELD_FIRST | STRUCT_FLOAT_FIELD_ONLY_TWO),

    STRUCT_MERGE_FIRST_SECOND_8 = (STRUCT_FLOAT_FIELD_FIRST | STRUCT_FLOAT_FIELD_ONLY_TWO | STRUCT_SECOND_FIELD_SIZE_IS8),

    STRUCT_HAS_FLOAT_FIELDS_MASK = (STRUCT_FLOAT_FIELD_FIRST | STRUCT_FLOAT_FIELD_SECOND | STRUCT_FLOAT_FIELD_ONLY_TWO | STRUCT_FLOAT_FIELD_ONLY_ONE),

    STRUCT_HAS_8BYTES_FIELDS_MASK = (STRUCT_FIRST_FIELD_SIZE_IS8 | STRUCT_SECOND_FIELD_SIZE_IS8),
}
