// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public class GenTreeOpCC : GenTreeOp
{
    private GenCondition _condition;

    public GenTreeOpCC(genTreeOps oper, var_types type, GenCondition condition, GenTree op1, GenTree op2)
        : base(oper, type, op1, op2)
    {
#if TARGET_ARM64
        assert(oper is GT_JCMP or GT_JTEST or GT_SELECTCC or GT_CCMP or GT_SELECT_INCCC or GT_SELECT_INVCC or GT_SELECT_NEGCC);
#else
        assert(oper is GT_JCMP or GT_JTEST or GT_SELECTCC or GT_CCMP);
#endif

        _condition = condition;
    }

    public GenCondition Condition => _condition;
}
