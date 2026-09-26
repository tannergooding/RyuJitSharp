// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class RangeCheck
{
    private const int MaxSearchDepth = 100;
    internal const int MaxVisitBudget = 8192;

    private readonly Compiler _compiler;
    private readonly Dictionary<GenTree, Range> _rangeMap = [];
    private readonly Dictionary<GenTree, BasicBlock?> _searchPath = [];
    private ValueNum _preferredBound = ValueNumStore.NoVN;
    private int _visitBudget = MaxVisitBudget;
    private bool _updateStmt;

    public RangeCheck(Compiler compiler)
    {
        _compiler = compiler;
    }

    public void SetBudget(int budget)
    {
        _visitBudget = budget;
    }

    private bool IsOverBudget => _visitBudget <= 0;
}
