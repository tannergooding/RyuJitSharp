// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial struct GenTreeOperandsList
{
    public struct Enumerator : IEnumerator<GenTree>
    {
        private readonly GenTree _tree;
        private GenTree? _current;
        private GenTreePhi.Use? _phiUse;
        private CallArg? _callArg;
        private GenTreeFieldList.Use? _fieldListUse;
        private int _index;

        public Enumerator(GenTree tree)
        {
            _tree = tree;
            _index = -1;
        }

#nullable disable
        public readonly GenTree Current => _current;
#nullable restore

        [MemberNotNullWhen(true, nameof(Current))]
        public bool MoveNext()
        {
            var current = null as GenTree;
            var index = _index;

            var tree = _tree;
            var oper = tree.Oper;

            if (oper.IsLeaf)
            {
                // nothing to do
            }
            else if (oper.IsBinary)
            {
                var op = tree.AsOp();

                if (index < 0)
                {
                    current = tree.IsReverseOp ? op.Op2 : op.Op1;

                    if (current is null)
                    {
                        // We can have null op1 and non-null op2 for some nodes, such as GT_LEA
                        index++;
                    }
                }

                if (index == 0)
                {
                    current = tree.IsReverseOp ? op.Op1 : op.Op2;
                }
            }
            else if (oper.IsUnary)
            {
                var unOp = tree.AsUnOp();

                if (index < 0)
                {
                    current = unOp.Op1;
                }
            }
            else
            {
                assert(oper.IsSpecial);

                switch (oper)
                {
                    case GT_PHI:
                    {
                        var phiUse = _phiUse;

                        if (phiUse is not null)
                        {
                            phiUse = phiUse.Next;
                        }
                        else
                        {
                            phiUse = tree.AsPhi().FirstUse;
                        }

                        if (phiUse is not null)
                        {
                            current = phiUse.Node;
                            _phiUse = phiUse;
                        }
                        break;
                    }

                    case GT_CMPXCHG:
                    {
                        var cmpXchg = tree.AsCmpXchg();

                        if (index < 0)
                        {
                            current = cmpXchg.Addr;
                        }
                        else if (index == 0)
                        {
                            current = cmpXchg.Data;
                        }
                        else if (index == 1)
                        {
                            current = cmpXchg.Comparand;
                        }
                        break;
                    }

                    case GT_SELECT:
                    {
                        var conditional = tree.AsConditional();

                        if (index < 0)
                        {
                            current = conditional.Cond;
                        }
                        else if (index == 0)
                        {
                            current = conditional.Op1;
                        }
                        else if (index == 1)
                        {
                            current = conditional.Op2;
                        }
                        break;
                    }

#if FEATURE_HW_INTRINSICS
                    case GT_HWINTRINSIC:
                    {
                        var hwintrinsic = tree.AsHWIntrinsic();
                        var operands = hwintrinsic.Operands;

                        if (tree.IsReverseOp)
                        {
                            if (index < 0)
                            {
                                current = operands[1];
                            }
                            else if (index == 0)
                            {
                                current = operands[0];
                            }
                        }
                        else if ((index + 1) < operands.Length)
                        {
                            current = operands[index + 1];
                        }
                        break;
                    }
#endif

                    case GT_ARR_ELEM:
                    {
                        var arrElem = tree.AsArrElem();

                        if (index < 0)
                        {
                            current = arrElem.ArrObj;
                        }
                        else if (index < arrElem.ArrRank)
                        {
                            current = arrElem.ArrInds[index];
                        }
                        break;
                    }

                    case GT_CALL:
                    {
                        var call = tree.AsCall();
                        var callArg = _callArg;

                        if (index < 0)
                        {
                            if (callArg is not null)
                            {
                                callArg = callArg.Next;
                            }
                            else
                            {
                                callArg = call.Args.Head;
                            }

                            while ((callArg is not null) && (callArg.EarlyNode is null))
                            {
                                callArg = callArg.Next;
                            }

                            if (callArg is not null)
                            {
                                current = callArg.EarlyNode;
                                _callArg = callArg;
                            }

                            if (current is null)
                            {
                                index++;
                            }
                            else
                            {
                                // we don't know how many early args we have, so we decrement
                                // to ensure the latter increment keeps it at `-1` until we
                                // finish enumerating all early args

                                index--;
                            }
                        }

                        if (index == 0)
                        {
                            if (callArg is not null)
                            {
                                callArg = callArg.LateNext;
                            }
                            else
                            {
                                callArg = call.Args.LateHead;
                            }

                            if (callArg is not null)
                            {
                                current = callArg.LateNode;
                                _callArg = callArg;
                            }

                            if (current is null)
                            {
                                index++;
                            }
                            else
                            {
                                // same again for late args, but this time moving to the control
                                // expression when we finish enumerating all late args
                                index--;
                            }
                        }

                        if (index == 1)
                        {
                            current = call.ControlExpr;
                        }
                        break;
                    }

                    case GT_FIELD_LIST:
                    {
                        var fieldListUse = _fieldListUse;

                        if (fieldListUse is not null)
                        {
                            fieldListUse = fieldListUse.Next;
                        }
                        else
                        {
                            fieldListUse = tree.AsFieldList().Uses.Head;
                        }

                        if (fieldListUse is not null)
                        {
                            current = fieldListUse.Node;
                            _fieldListUse = fieldListUse;
                        }
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }

            var succeeded = false;

            if (current is not null)
            {
                _current = current;
                _index = index + 1;
                succeeded = true;
            }
            return succeeded;
        }

        public void Reset()
        {
            _current = null;
            _index = -1;
        }

        readonly object IEnumerator.Current => Current;

        readonly void IDisposable.Dispose() { }
    }
}
