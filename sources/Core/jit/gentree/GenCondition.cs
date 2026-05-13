// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly partial struct GenCondition
{
    private static ReadOnlySpan<CodeKind> s_codes => [EQ, NE, SLT, SLE, SGE, SGT, EQ, NE, NC, C];

    private static readonly string[] s_names = [
        "NONE", "???",  "SLT",  "SLE",  "SGE",  "SGT",  "S", "NS",
        "UEQ",  "UNE",  "ULT",  "ULE",  "UGE",  "UGT",  "C", "NC",
        "FEQ",  "FNE",  "FLT",  "FLE",  "FGE",  "FGT",  "O", "NO",
        "FEQU", "FNEU", "FLTU", "FLEU", "FGEU", "FGTU", "P", "NP"
    ];

    private static ReadOnlySpan<CodeKind> s_reverseCodes => [
           //  EQ    NE    LT    LE    GE    GT    F   NF
           NONE, NONE, SGE,  SGT,  SLT,  SLE,  NS, S,
            NE,   EQ,   UGE,  UGT,  ULT,  ULE,  NC, C,
            FNEU, FEQU, FGEU, FGTU, FLTU, FLEU, NO, O,
            FNE,  FEQ,  FGE,  FGT,  FLT,  FLE,  NP, P,
    ];

    private static ReadOnlySpan<CodeKind> s_swapCodes => [
        //  EQ    NE    LT    LE    GE    GT    F  NF
            NONE, NONE, SGT,  SGE,  SLE,  SLT,  S, NS,
            EQ,   NE,   UGT,  UGE,  ULE,  ULT,  C, NC,
            FEQ,  FNE,  FGT,  FGE,  FLE,  FLT,  O, NO,
            FEQU, FNEU, FGTU, FGEU, FLEU, FLTU, P, NP,
    ];

    private readonly CodeKind _code;

    public GenCondition(CodeKind code)
    {
        _code = code;
    }

    public CodeKind Code => _code;

    public bool IsFlag => (_code & OperMask) >= S;

    public bool IsFloat => !IsFlag && ((_code & Float) != 0);

    public bool IsUnsigned => _code is >= ULT and <= UGT;

    public bool IsUnordered => !IsFlag && ((_code & (Float | Unordered)) == (Float | Unordered));

    public string Name => s_names[(int)(_code)];

#if TARGET_XARCH
    // Indicate whether the condition should be swapped in order to avoid generating
    // multiple branches. This happens for certain floating point conditions on XARCH,
    // see GenConditionDesc and its associated mapping table for more details.
    public bool PreferSwap => _code is FLT or FLE or FGTU or FGEU;
#else
    public bool PreferSwap => false;
#endif

    public static GenCondition FromFloatRelop(GenTreeOp relop)
    {
        assert(varTypeIsFloating(relop.Op1.Type) && varTypeIsFloating(relop.Op2.Type));
        return FromFloatRelop(relop.Oper, isUnordered: (relop.Flags & GTF_RELOP_NAN_UN) != 0);
    }

    public static GenCondition FromFloatRelop(genTreeOps oper, bool isUnordered)
    {
        assert(oper.IsCompare);

        var code = (CodeKind)(oper - GT_EQ);
        assert(code <= SGT);
        code |= Float;

        if (isUnordered)
        {
            code |= Unordered;
        }
        return new GenCondition(code);
    }

    public static GenCondition FromIntegralRelop(GenTreeOp relop)
    {
        assert(!varTypeIsFloating(relop.Op1.Type) && !varTypeIsFloating(relop.Op2.Type));
        return FromIntegralRelop(relop.Oper, relop.IsUnsigned);
    }

    public static GenCondition FromIntegralRelop(genTreeOps oper, bool isUnsigned)
    {
        assert(oper.IsCompare);
        var code = s_codes[oper - GT_EQ];

        if (isUnsigned || ((int)(code) <= 1))
        {
            // EQ/NE are treated as unsigned
            code |= Unsigned;
        }
        return new GenCondition(code);
    }

    public static GenCondition FromRelop(GenTreeOp relop)
    {
        assert(relop.Oper.IsCompare);

        if (varTypeIsFloating(relop.Op1.Type))
        {
            return FromFloatRelop(relop);
        }
        else
        {
            return FromIntegralRelop(relop);
        }
    }

    public static GenCondition Reverse(GenCondition condition)
    {
        var reverseCode = s_reverseCodes[(int)(condition._code)];
        return new GenCondition(reverseCode);
    }

    public static GenCondition Swap(GenCondition condition)
    {
        var swapCode = s_swapCodes[(int)(condition._code)];
        return new GenCondition(swapCode);
    }
}
