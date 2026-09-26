// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private unsafe void AnalyzeParentStack(GenTreeStack parentStack, int lclIndex, BasicBlock block)
    {
        assert(!CanIndexEscape(lclIndex));

        var compiler = CompilerInstance;
        var parentIndex = 1;
        var lclNum = IndexToLocal(lclIndex);
        ref var lclDsc = ref compiler.lvaGetDesc(lclNum);

        var keepChecking = true;
        var canEscape = true;
        var edgeAdded = false;
        var isTrivialUse = false;
        var isEnumeratorLocal = lclDsc.lvIsEnumerator;
        var isAddress = parentStack.Peek().Oper is GT_LCL_ADDR;

        while (keepChecking)
        {
            if (parentStack.Count <= parentIndex)
            {
                canEscape = false;
                isTrivialUse = true;
                break;
            }

            var tree = StackNode(parentStack, parentIndex - 1);
            var parent = StackNode(parentStack, parentIndex);
            canEscape = true;
            keepChecking = false;
            isTrivialUse = false;

            JITDUMP($"... V{lclNum:D2} ... checking [{TreeIdForDump(parent):D6}]\n");

            switch (parent.Oper)
            {
                case GT_STORE_LCL_VAR:
                {
                    if (isAddress)
                    {
                        break;
                    }

                    var dstLclNum = parent.AsLclVar().LclNum;
                    if (!IsTrackedLocal(dstLclNum))
                    {
                        canEscape = false;
                        break;
                    }

                    var dstIndex = LocalToIndex(dstLclNum);
                    AddConnGraphEdgeIndex(dstIndex, lclIndex);
                    edgeAdded = true;
                    canEscape = false;
                    _storeAddressToIndexMap[parent] = new StoreInfo(dstIndex, connected: true);
                    break;
                }

                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_GT:
                case GT_LE:
                case GT_GE:
                {
                    canEscape = false;
                    var op = parent.AsOp();
                    var other = (op.Op1 == tree) ? op.Op2 : op.Op1;
                    if (other.IsIntegralConst(0))
                    {
                        isTrivialUse = true;
                    }
                    break;
                }

                case GT_NULLCHECK:
                case GT_ARR_LENGTH:
                case GT_BOUNDS_CHECK:
                {
                    canEscape = false;
                    isTrivialUse = true;
                    break;
                }

                case GT_COMMA:
                {
                    if (parent.AsOp().Op1 == tree)
                    {
                        canEscape = false;
                        isTrivialUse = true;
                        break;
                    }
                    goto case GT_COLON;
                }

                case GT_COLON:
                case GT_QMARK:
                case GT_ADD:
                case GT_FIELD_ADDR:
                case GT_BOX:
                {
                    parentIndex++;
                    keepChecking = true;
                    break;
                }

                case GT_SUB:
                {
                    if (parent.Type is not (TYP_BYREF or TYP_REF))
                    {
                        canEscape = false;
                        break;
                    }

                    isAddress = false;
                    parentIndex++;
                    keepChecking = true;
                    break;
                }

                case GT_INDEX_ADDR:
                {
                    if (tree == parent.AsIndexAddr().Index)
                    {
                        canEscape = false;
                        break;
                    }

                    parentIndex++;
                    keepChecking = true;
                    break;
                }

                case GT_STOREIND:
                case GT_STORE_BLK:
                {
                    if (!IsTrackedType(parent.Type) ||
                        ((parent.Oper is GT_STORE_BLK) && !parent.AsBlk().Layout.HasGCPtr))
                    {
                        canEscape = false;
                        break;
                    }

                    if (tree == parent.AsIndir().Addr)
                    {
                        if (isAddress)
                        {
                            JITDUMP("... store address is local\n");
                            _storeAddressToIndexMap[parent] = new StoreInfo(lclIndex);
                            isTrivialUse = true;
                        }

                        canEscape = false;
                        break;
                    }

                    if (_storeAddressToIndexMap.TryGetValue(parent, out var dstInfo))
                    {
                        assert(dstInfo.Index != BAD_VAR_NUM);
                        assert(!dstInfo.Connected);
                        JITDUMP("... local.field store\n");
                        dstInfo.Connected = true;

                        JITDUMP(" ... Modelled GC store to");
#if DEBUG
                        if (compiler.verbose)
                        {
                            DumpIndex(dstInfo.Index);
                        }
#endif
                        JITDUMP($" at [{TreeIdForDump(parent):D6}]\n");
                        if (isAddress)
                        {
                            AddConnGraphEdgeIndex(dstInfo.Index, _unknownSourceIndex);
                        }
                        else
                        {
                            AddConnGraphEdgeIndex(dstInfo.Index, lclIndex);
                            edgeAdded = true;
                            canEscape = false;
                        }
                    }
                    break;
                }

                case GT_STORE_LCL_FLD:
                {
                    if (!IsTrackedType(tree.Type))
                    {
                        canEscape = false;
                        break;
                    }

                    var dstLclNum = parent.AsLclVarCommon().LclNum;
                    if (IsTrackedLocal(dstLclNum))
                    {
                        JITDUMP($"... local V{dstLclNum:D2}.f store\n");
                        var dstIndex = LocalToIndex(dstLclNum);
                        AddConnGraphEdgeIndex(dstIndex, lclIndex);
                        edgeAdded = true;
                        canEscape = false;
                        _storeAddressToIndexMap[parent] = new StoreInfo(dstIndex, connected: true);
                    }
                    break;
                }

                case GT_IND:
                case GT_BLK:
                {
                    if (!IsTrackedType(parent.Type) ||
                        ((parent.Oper is GT_BLK) && !parent.AsBlk().Layout.HasGCPtr))
                    {
                        canEscape = false;
                        break;
                    }

                    if (_trackFields && isAddress)
                    {
                        JITDUMP("... load local.field\n");
                        parentIndex++;
                        isAddress = false;
                        keepChecking = true;
                    }
                    else
                    {
                        canEscape = false;
                    }
                    break;
                }

                case GT_LCL_FLD:
                {
                    if (!IsTrackedType(parent.Type))
                    {
                        canEscape = false;
                        break;
                    }

                    if (_trackFields && (lclDsc.Type is TYP_STRUCT))
                    {
                        JITDUMP("... load local.field\n");
                        parentIndex++;
                        isAddress = false;
                        keepChecking = true;
                    }
                    else
                    {
                        canEscape = false;
                    }
                    break;
                }

                case GT_CALL:
                {
                    var call = parent.AsCall();
                    if (call.IsHelperCall())
                    {
                        canEscape = !call.HelperNum.IsNoEscape;
                    }
                    else if (call.IsSpecialIntrinsic())
                    {
                        switch (compiler.lookupNamedIntrinsic(call._callMethHnd))
                        {
                            case NI_System_SpanHelpers_ClearWithoutReferences:
                            case NI_System_SpanHelpers_Memmove:
                            case NI_System_SpanHelpers_SequenceEqual:
                            {
                                canEscape = false;
                                break;
                            }

                            case NI_System_SpanHelpers_Fill:
                            {
                                var firstArg = call.Args.GetUserArgByIndex(0);
                                assert(firstArg is not null);
                                if (tree == firstArg.Node)
                                {
                                    canEscape = false;
                                }
                                break;
                            }
                        }
                    }
                    else if (call.IsDelegateInvoke)
                    {
                        var thisArg = call.Args.ThisArg;
                        assert(thisArg is not null);
                        if (tree == thisArg.Node)
                        {
                            JITDUMP("Delegate invoke this...\n");
                            canEscape = false;
                        }
                    }

                    if (isEnumeratorLocal)
                    {
                        JITDUMP($"Enumerator V{lclNum:D2} passed to call...\n");
                        canEscape = !CheckForGuardedUse(block, parent, lclNum);
                    }
                    break;
                }
            }
        }

        if (canEscape && !CanIndexEscape(lclIndex))
        {
#if DEBUG
            if (compiler.verbose)
            {
                DumpIndex(lclIndex);
            }
#endif
            JITDUMP($" first escapes via [{TreeIdForDump(parentStack.Peek()):D6}]..." +
                $"[{TreeIdForDump(StackNode(parentStack, parentIndex)):D6}]\n");
            MarkLclVarAsEscaping(lclNum);
        }

        if (!edgeAdded && !isTrivialUse && !IsIndexUsed(lclIndex))
        {
#if DEBUG
            if (compiler.verbose)
            {
                DumpIndex(lclIndex);
            }
#endif
            JITDUMP($" first used via [{TreeIdForDump(parentStack.Peek()):D6}]\n");
            MarkIndexAsUsed(lclIndex);
        }
    }

    private static GenTree StackNode(GenTreeStack stack, int index)
    {
        foreach (var node in stack)
        {
            if (index-- == 0)
            {
                return node;
            }
        }

        throw new InvalidOperationException("Missing ancestor in object-allocation analysis.");
    }
}
