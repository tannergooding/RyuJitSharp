// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public RangeCheck GetRangeCheck(int customBudget = 0)
    {
        optRangeCheck ??= new RangeCheck(this);
        optRangeCheck.SetBudget(customBudget > 0 ? customBudget : RangeCheck.MaxVisitBudget);
        return optRangeCheck;
    }
}
