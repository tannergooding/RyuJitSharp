// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public class GenTreeOpCC : GenTreeOp
{
    private GenCondition _condition;

    public GenTreeOpCC(genTreeOps oper, var_types type, GenCondition condition, GenTree op1, GenTree? op2)
        : base(EnsureSupportedOperation(oper), type, op1, op2)
    {
        assert(IsSupportedOperation(oper));

        _condition = condition;
    }
    internal GenTreeOpCC(genTreeOps oper, var_types type, GenCondition condition, GenTree op1, GenTree? op2,
        GenTree source, NodeThreading threading)
        : base(EnsureSupportedOperation(oper), type, op1, op2, source, threading)
    {
        assert(IsSupportedOperation(oper));

        _condition = condition;
    }

    private static bool IsSupportedOperation(genTreeOps oper)
    {
        if (Configuration.TARGET_ARM64)
        {
            return oper is GT_JCMP or GT_JTEST or GT_SELECTCC or GT_CCMP
                or GT_SELECT_INCCC or GT_SELECT_INVCC or GT_SELECT_NEGCC;
        }

        if (Configuration.TARGET_AMD64)
        {
            return oper is GT_JCMP or GT_JTEST or GT_SELECTCC or GT_CCMP;
        }

        return oper is GT_JCMP or GT_JTEST or GT_SELECTCC;
    }

    private static genTreeOps EnsureSupportedOperation(genTreeOps oper)
    {
        if ((oper is GT_CCMP) && !Configuration.TARGET_AMD64 && !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "GT_CCMP is only supported on x64 and ARM64.");
            return default!;
        }

        if ((oper is GT_SELECT_INCCC or GT_SELECT_INVCC or GT_SELECT_NEGCC) && !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage232
            );
            return default!;
        }

        return oper;
    }

    public GenCondition Condition
    {
        get
        {
            return _condition;
        }

        set
        {
            _condition = value;
        }
    }
}
