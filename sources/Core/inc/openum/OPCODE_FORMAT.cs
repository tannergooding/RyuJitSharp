// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.OPCODE_FORMAT;

namespace RyuJitSharp;

public enum OPCODE_FORMAT
{
    /// <summary>no inline args</summary>
    InlineNone = 0,

    /// <summary>local variable       (U2 (U1 if Short on))</summary>
    InlineVar = 1,

    /// <summary>an signed integer    (I4 (I1 if Short on))</summary>
    InlineI = 2,

    /// <summary>a real number        (R8 (R4 if Short on))</summary>
    InlineR = 3,

    /// <summary>branch target        (I4 (I1 if Short on))</summary>
    InlineBrTarget = 4,

    InlineI8 = 5,

    /// <summary>method token (U4)</summary>
    InlineMethod = 6,

    /// <summary>field token  (U4)</summary>
    InlineField = 7,

    /// <summary>type token   (U4)</summary>
    InlineType = 8,

    /// <summary>string TOKEN (U4)</summary>
    InlineString = 9,

    /// <summary>signature tok (U4)</summary>
    InlineSig = 10,

    /// <summary>ldptr token  (U4)</summary>
    InlineRVA = 11,

    /// <summary>a meta-data token of unknown type (U4)</summary>
    InlineTok = 12,

    /// <summary>count (U4), pcrel1 (U4) .... pcrelN (U4)</summary>
    InlineSwitch = 13,

    /// <summary>count (U1), var1 (U2) ... varN (U2)</summary>
    InlinePhi = 14,

    // WATCH OUT we are close to the limit here, if you add
    // more enumerations you need to change ShortIline definition below

    // The extended enumeration also encodes the size in the IL stream

    /// <summary>if this bit is set, the format is the 'short' format</summary>
    ShortInline = 16,

    /// <summary>mask these off to get primary enumeration above</summary>
    PrimaryMask = (ShortInline - 1),

    ShortInlineVar = (ShortInline + InlineVar),

    ShortInlineI = (ShortInline + InlineI),

    ShortInlineR = (ShortInline + InlineR),

    ShortInlineBrTarget = (ShortInline + InlineBrTarget),

    /// <summary>This is only used internally.  It means the 'opcode' is two byte instead of 1</summary>
    InlineOpcode = (ShortInline + InlineNone),
}
