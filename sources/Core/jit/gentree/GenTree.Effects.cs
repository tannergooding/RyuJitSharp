// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public bool NodeOrContainedOperandsMayThrow(Compiler compiler)
    {
        if (MayThrow(compiler))
        {
            return true;
        }
        foreach (var operand in Operands)
        {
            if (operand.IsContained && operand.NodeOrContainedOperandsMayThrow(compiler))
            {
                return true;
            }
        }
        return false;
    }

    public bool RequiresGlobRefFlag(Compiler compiler)
    {
        switch (Oper)
        {
            case GT_LCL_VAR:
            case GT_LCL_FLD:
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                return compiler.lvaGetDesc(AsLclVarCommon().LclNum).IsAddressExposed;
            }
            case GT_IND:
            case GT_BLK:
            {
                return !AsIndir().IsInvariantLoad;
            }
            case GT_STOREIND:
            case GT_STORE_BLK:
            case GT_XADD:
            case GT_XORR:
            case GT_XAND:
            case GT_XCHG:
            case GT_LOCKADD:
            case GT_CMPXCHG:
            case GT_MEMORYBARRIER:
            case GT_KEEPALIVE:
            case GT_LCLHEAP:
            case GT_ASYNC_CONTINUATION:
            case GT_RETURN_SUSPEND:
            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            case GT_NONLOCAL_JMP:
            case GT_SWIFT_ERROR:
            case GT_GCPOLL:
            {
                return true;
            }
            case GT_CALL:
            {
                return AsCall().HasSideEffects(compiler, ignoreExceptions: true);
            }
            case GT_ALLOCOBJ:
            {
                return AsAllocObj().NewHelperHasSideEffects;
            }
#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                var intrinsic = AsHWIntrinsic();
                return intrinsic.IsMemoryLoad() || intrinsic.RequiresAsgFlag || intrinsic.RequiresCallFlag();
            }
#endif
            default:
            {
                assert(!RequiresCallFlag(compiler) || (Oper is GT_INTRINSIC));
                assert((!Oper.IsIndir || (Oper is GT_NULLCHECK)) && !RequiresAsgFlag);
                return false;
            }
        }
    }

    public GenTreeFlags OperEffects(Compiler compiler, out ExceptionSetFlags preciseExceptions)
    {
        var effects = Flags & GTF_ALL_EFFECT;
        if (((effects & GTF_ASG) != 0) && !RequiresAsgFlag)
        {
            effects &= ~GTF_ASG;
        }
        if (((effects & GTF_CALL) != 0) && !RequiresCallFlag(compiler))
        {
            effects &= ~GTF_CALL;
        }
        preciseExceptions = ExceptionSetFlags.None;
        if ((effects & GTF_EXCEPT) != 0)
        {
            preciseExceptions = Exceptions(compiler);
            if (preciseExceptions is ExceptionSetFlags.None)
            {
                effects &= ~GTF_EXCEPT;
            }
        }
        if (((effects & GTF_GLOB_REF) != 0) && !RequiresGlobRefFlag(compiler))
        {
            effects &= ~GTF_GLOB_REF;
        }
        if (((effects & GTF_ORDER_SIDEEFF) != 0) && !SupportsOrderingSideEffect())
        {
            effects &= ~GTF_ORDER_SIDEEFF;
        }
        return effects;
    }
}
