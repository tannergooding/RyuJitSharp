// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe GenTree? LowerDirectCall(GenTreeCall call)
    {
        noway_assert((call._callType is CT_USER_FUNC) || call.IsHelperCall());

        void* address;
        InfoAccessType accessType;
        var helper = call.HelperNum;

#if FEATURE_READYTORUN
        if (call._entryPoint.addr is not null)
        {
            accessType = call._entryPoint.accessType;
            address = call._entryPoint.addr;
        }
        else
#endif
        if (call.IsHelperCall())
        {
            noway_assert(helper is not CORINFO_HELP_UNDEF);
            var lookup = CompilerInstance.compGetHelperFtn(helper);
            address = lookup.addr;
            accessType = lookup.accessType;
        }
        else
        {
            noway_assert(helper is CORINFO_HELP_UNDEF);
            var accessFlags = CORINFO_ACCESS_ANY;
            if (call.IsSameThis)
            {
                accessFlags |= CORINFO_ACCESS_THIS;
            }
            if (!call.NeedsNullCheck)
            {
                accessFlags |= CORINFO_ACCESS_NONNULL;
            }

            CORINFO_CONST_LOOKUP lookup;
            CompilerInstance.info.compCompHnd->getFunctionEntryPoint(call._callMethHnd, &lookup, accessFlags);
            address = lookup.addr;
            accessType = lookup.accessType;
        }

        GenTree? result = null;
        switch (accessType)
        {
            case IAT_VALUE:
            {
                if (!IsCallTargetInRange(address) || call.IsTailCallViaJitHelper)
                {
                    result = AddrGen((nint)address);
                }
                else
                {
                    call._directCallAddress = address;
                }
                break;
            }

            case IAT_PVALUE:
            {
#if !TARGET_WASM
                // Reuse the existing indirection-cell argument instead of duplicating its load.
                if (call.IndirectionCellArgKind is WellKnownArg.None)
#endif
                {
                    var cellAddress = AddrGen((nint)address);
#if DEBUG
                    cellAddress.TargetHandle = (nint)call._callMethHnd;
#endif
                    result = CompilerInstance.gtNewIndir(TYP_I_IMPL, cellAddress, GTF_IND_NONFAULTING);
                }
                break;
            }

            case IAT_PPVALUE:
            {
                // Morph should expand this early enough to expose the invariant load to hoisting/CSE.
                assert(false, "IAT_PPVALUE case in LowerDirectCall");
                noway_assert(helper is CORINFO_HELP_UNDEF);
                result = Ind(Ind(AddrGen((nint)address)));
                break;
            }

            case IAT_RELPVALUE:
            {
                var cellAddress = AddrGen((nint)address);
                result = new GenTreeOp(GT_ADD, TYP_I_IMPL, Ind(cellAddress), AddrGen((nint)address));
                break;
            }

            default:
            {
                noway_assert(false, "Bad accessType");
                break;
            }
        }
        return result;
    }

    private GenTreeIntCon AddrGen(nint address) => CompilerInstance.gtNewIconHandleNode(address, GTF_ICON_FTN_ADDR);

    private GenTreeIndir Ind(GenTree tree, var_types type = TYP_I_IMPL) => CompilerInstance.gtNewIndir(type, tree);

    private static unsafe bool IsCallTargetInRange(void* address)
    {
#if TARGET_XARCH
        return true;
#else
        NYI("Lowering.IsCallTargetInRange outside xarch");
        fatal(CORJIT_IMPLLIMITATION);
        return false;
#endif
    }

    private void LegalizeArgPlacement(GenTreeCall call)
    {
        var numMarked = MarkCallPutArgAndFieldListNodes(call);
#if DEBUG && !FEATURE_FIXED_OUT_ARGS
        var nextPushOffset = uint.MaxValue;
#endif
        var current = call.Prev;
        while (numMarked > 0)
        {
            assert(current is not null);
            if ((current._lirFlags & LIR.Flags.Mark) != 0)
            {
                numMarked--;
                current._lirFlags &= ~LIR.Flags.Mark;
#if DEBUG && !FEATURE_FIXED_OUT_ARGS
                if (current.Oper is GT_PUTARG_STK)
                {
                    assert(nextPushOffset > current.AsPutArgStk().ArgOffset);
                    nextPushOffset = (uint)current.AsPutArgStk().ArgOffset;
                }
#endif
            }

            if (current.Oper is GT_CALL)
            {
                break;
            }
            current = current.Prev;
        }

        if (numMarked == 0)
        {
            return;
        }

        assert(current is not null);
#if DEBUG
        JITDUMP($"Call [{call.TreeId:D6}] has {numMarked} PUTARG nodes that interfere with [{current.TreeId:D6}]; will move them after it\n");
#endif
        var insertionPoint = current;
        var block = _block;
        assert(block is not null);

        while (numMarked > 0)
        {
            assert(current is not null);
            var previous = current.Prev;
            if ((current._lirFlags & LIR.Flags.Mark) != 0)
            {
                numMarked--;
                current._lirFlags &= ~LIR.Flags.Mark;
#if !FEATURE_FIXED_OUT_ARGS
                if (current.Oper is GT_FIELD_LIST or GT_PUTARG_REG)
#endif
                {
#if DEBUG
                    JITDUMP($"Relocating [{current.TreeId:D6}] after [{insertionPoint.TreeId:D6}]\n");
#endif
                    // Walking backward and inserting after one fixed point preserves argument order.
                    block.Remove(current);
                    block.InsertAfter(insertionPoint, current);
                }
            }
            current = previous;
        }

        JITDUMP("Final result after legalization:\n");
        DISPTREERANGE(block, call);
    }

    private static nuint MarkCallPutArgAndFieldListNodes(GenTreeCall call)
    {
        nuint numMarked = 0;
        foreach (var arg in call.Args.Args)
        {
            if (arg.EarlyNode is GenTree earlyNode)
            {
                numMarked += MarkPutArgAndFieldListNodes(earlyNode);
            }
            if (arg.LateNode is GenTree lateNode)
            {
                numMarked += MarkPutArgAndFieldListNodes(lateNode);
            }
        }
        return numMarked;
    }

    private static nuint MarkPutArgAndFieldListNodes(GenTree node)
    {
#if !HAS_FIXED_REGISTER_SET
        if (!node.Oper.IsPutArg && !node.Oper.IsFieldList)
        {
            return 0;
        }
#endif
        assert(node.Oper.IsPutArg || node.Oper.IsFieldList);
        assert((node._lirFlags & LIR.Flags.Mark) == 0);
        node._lirFlags |= LIR.Flags.Mark;

        nuint result = 1;
        if (node.Oper.IsFieldList)
        {
            foreach (var use in node.AsFieldList().Uses)
            {
                result += MarkPutArgAndFieldListNodes(use.Node);
            }
        }
        return result;
    }
}
