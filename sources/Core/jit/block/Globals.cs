// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public const int EXPSET_SZ = 64;

    /// <summary>We use the following format when printing the BasicBlock number: bbNum </summary>
    public const string FMT_BB = "BB{0:D2}";

    /// <summary>Use this format for loop indices</summary>
    public const string FMT_LP = "L{0:D2}";

    /// <summary>Use this format for profile weights</summary>
    public const string FMT_WT = "{0:G7}";

    /// <summary>Use this format for profile weights where we want to conserve horizontal space, at the expense of displaying less precision.</summary>
    public const string FMT_WT_NARROW = "{0:G3}";

#if DEBUG
    public static readonly string[] bbKindNames = [
        "BBJ_EHFINALLYRET",
        "BBJ_EHFAULTRET",
        "BBJ_EHFILTERRET",
        "BBJ_EHCATCHRET",
        "BBJ_THROW",
        "BBJ_RETURN",
        "BBJ_ALWAYS",
        "BBJ_LEAVE",
        "BBJ_CALLFINALLY",
        "BBJ_CALLFINALLYRET",
        "BBJ_COND",
        "BBJ_SWITCH",
        "BBJ_COUNT",
    ];

    public static readonly string[] memoryKindNames = [
        "ByrefExposed",
        "GcHeap",
    ];
#endif

    // Special values for bbCatchType, which is normally a class token of the catch handler.
    // These special values will not collide with real tokens.

    public const int BBCT_NONE = unchecked((int)(0x00000000));

    public const int BBCT_FAULT = unchecked((int)(0xFFFFFFFC));

    public const int BBCT_FINALLY = unchecked((int)(0xFFFFFFFD));

    public const int BBCT_FILTER = unchecked((int)(0xFFFFFFFE));

    public const int BBCT_FILTER_HANDLER = unchecked((int)(0xFFFFFFFF));

    /// <summary>how much a normal execute once block weighs</summary>
    public const weight_t BB_UNITY_WEIGHT = 100.0;

    /// <summary>how much a normal execute once block weighs</summary>
    public const uint BB_UNITY_WEIGHT_UNSIGNED = 100;

    /// <summary>synthetic profile scale factor for loops</summary>
    public const weight_t BB_LOOP_WEIGHT_SCALE = 8.0;

    public const weight_t BB_ZERO_WEIGHT = 0.0;

    /// <summary>Upper bound for cold weights; used during block layout</summary>
    public const weight_t BB_COLD_WEIGHT           = 0.01;

    /// <summary>maximum finite weight -- needs rethinking.</summary>
    public const weight_t BB_MAX_WEIGHT = float.MaxValue;

    /// <summary>base# to use when we have none</summary>
    public const uint NO_BASE_TMP = uint.MaxValue;

    public const ushort MAX_XCPTN_INDEX = ushort.MaxValue - 1;
}
