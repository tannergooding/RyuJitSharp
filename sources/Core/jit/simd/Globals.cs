// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
#if TARGET_XARCH
    // SSE2 Shuffle control byte to shuffle vector <W, Z, Y, X>
    // These correspond to shuffle immediate byte in shufps SSE2 instruction.
    public const byte SHUFFLE_XXXX = 0x00; // 00 00 00 00
    public const byte SHUFFLE_XXZX = 0x08; // 00 00 10 00
    public const byte SHUFFLE_XXWW = 0x0F; // 00 00 11 11
    public const byte SHUFFLE_XYZW = 0x1B; // 00 01 10 11
    public const byte SHUFFLE_YXYX = 0x44; // 01 00 01 00
    public const byte SHUFFLE_YWXZ = 0x72; // 01 11 00 10
    public const byte SHUFFLE_YWXW = 0x73; // 01 11 00 11
    public const byte SHUFFLE_YYZZ = 0x5A; // 01 01 10 10
    public const byte SHUFFLE_ZXXX = 0x80; // 10 00 00 00
    public const byte SHUFFLE_ZXXY = 0x81; // 10 00 00 01
    public const byte SHUFFLE_ZZXX = 0xA0; // 10 10 00 00
    public const byte SHUFFLE_ZWXY = 0xB1; // 10 11 00 01
    public const byte SHUFFLE_WYZX = 0xD8; // 11 01 10 00
    public const byte SHUFFLE_WWYY = 0xF5; // 11 11 01 01
#endif
}
