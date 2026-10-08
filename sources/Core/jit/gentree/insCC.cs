// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

// x86 condition-code encodings used by CTEST/CCMP; parity is not represented.
public enum insCC : uint
{
    INS_CC_O = 0x0,
    INS_CC_NO = 0x1,

    INS_CC_B = 0x2,
    INS_CC_C = 0x2,
    INS_CC_NAE = 0x2,

    INS_CC_NB = 0x3,
    INS_CC_NC = 0x3,
    INS_CC_AE = 0x3,

    INS_CC_E = 0x4,
    INS_CC_Z = 0x4,

    INS_CC_NE = 0x5,
    INS_CC_NZ = 0x5,

    INS_CC_BE = 0x6,
    INS_CC_NA = 0x6,

    INS_CC_NBE = 0x7,
    INS_CC_A = 0x7,

    INS_CC_S = 0x8,
    INS_CC_NS = 0x9,

    INS_CC_TRUE = 0xA, // Always evaluates to true.
    INS_CC_FALSE = 0xB, // Always evaluates to false.

    INS_CC_L = 0xC,
    INS_CC_NGE = 0xC,

    INS_CC_NL = 0xD,
    INS_CC_GE = 0xD,

    INS_CC_LE = 0xE,
    INS_CC_NG = 0xE,

    INS_CC_NLE = 0xF,
    INS_CC_G = 0xF,
}
#endif
