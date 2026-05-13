// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GenCondition
{
    public const CodeKind OperMask = CodeKind.OperMask;
    public const CodeKind Unsigned = CodeKind.Unsigned;
    public const CodeKind Unordered = CodeKind.Unordered;
    public const CodeKind Float = CodeKind.Float;

    public const CodeKind NONE = CodeKind.NONE;

    public const CodeKind SLT = CodeKind.SLT;
    public const CodeKind SLE = CodeKind.SLE;
    public const CodeKind SGE = CodeKind.SGE;
    public const CodeKind SGT = CodeKind.SGT;
    public const CodeKind S = CodeKind.S;
    public const CodeKind NS = CodeKind.NS;

    public const CodeKind EQ = CodeKind.EQ;
    public const CodeKind NE = CodeKind.NE;
    public const CodeKind ULT = CodeKind.ULT;
    public const CodeKind ULE = CodeKind.ULE;
    public const CodeKind UGE = CodeKind.UGE;
    public const CodeKind UGT = CodeKind.UGT;
    public const CodeKind C = CodeKind.C;
    public const CodeKind NC = CodeKind.NC;

    public const CodeKind FEQ = CodeKind.FEQ;
    public const CodeKind FNE = CodeKind.FNE;
    public const CodeKind FLT = CodeKind.FLT;
    public const CodeKind FLE = CodeKind.FLE;
    public const CodeKind FGE = CodeKind.FGE;
    public const CodeKind FGT = CodeKind.FGT;
    public const CodeKind O = CodeKind.O;
    public const CodeKind NO = CodeKind.NO;

    public const CodeKind FEQU = CodeKind.FEQU;
    public const CodeKind FNEU = CodeKind.FNEU;
    public const CodeKind FLTU = CodeKind.FLTU;
    public const CodeKind FLEU = CodeKind.FLEU;
    public const CodeKind FGEU = CodeKind.FGEU;
    public const CodeKind FGTU = CodeKind.FGTU;
    public const CodeKind P = CodeKind.P;
    public const CodeKind NP = CodeKind.NP;

    public enum CodeKind : byte
    {
        OperMask = 7,
        Unsigned = 8,
        Unordered = Unsigned,
        Float = 16,

        // 0 would be the encoding of "signed EQ" but since equality is sign insensitive
        // we'll use 0 as invalid/uninitialized condition code. This will also leave 1
        // as a spare code.
        NONE = 0,

        SLT = 2,
        SLE = 3,
        SGE = 4,
        SGT = 5,
        S = 6,
        NS = 7,

        EQ = Unsigned | 0,      // = 8
        NE = Unsigned | 1,      // = 9
        ULT = Unsigned | SLT,   // = 10
        ULE = Unsigned | SLE,   // = 11
        UGE = Unsigned | SGE,   // = 12
        UGT = Unsigned | SGT,   // = 13
        C = Unsigned | S,       // = 14
        NC = Unsigned | NS,     // = 15

        FEQ = Float | 0,        // = 16
        FNE = Float | 1,        // = 17
        FLT = Float | SLT,      // = 18
        FLE = Float | SLE,      // = 19
        FGE = Float | SGE,      // = 20
        FGT = Float | SGT,      // = 21
        O = Float | S,          // = 22
        NO = Float | NS,        // = 23

        FEQU = Unordered | FEQ, // = 24
        FNEU = Unordered | FNE, // = 25
        FLTU = Unordered | FLT, // = 26
        FLEU = Unordered | FLE, // = 27
        FGEU = Unordered | FGE, // = 28
        FGTU = Unordered | FGT, // = 29
        P = Unordered | O,      // = 30
        NP = Unordered | NO,    // = 31
    }
}
