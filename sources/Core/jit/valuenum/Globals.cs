// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    // We use a unique prefix character when printing value numbers in dumps:  i.e.  $1c0
    // This define is used with string concatenation to put this in printf format strings
    public const string FMT_VN = "${0:x}";

    // This is the constant value used for the default value of _mapSelectBudget. used by JitVNMapSelBudget
    public const int DEFAULT_MAP_SELECT_BUDGET = 100;
}
