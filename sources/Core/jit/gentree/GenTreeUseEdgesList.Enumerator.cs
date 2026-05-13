// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct GenTreeUseEdgesList
{
    public ref struct Enumerator
    {
        private readonly GenTree _tree;
        private ref GenTree? _current;
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
        public readonly ref GenTree Current => ref _current;
#nullable restore

        [MemberNotNullWhen(true, nameof(Current))]
        public bool MoveNext()
        {
            ref var current = ref Unsafe.NullRef<GenTree>();
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
                    current = ref (tree.IsReverseOp ? ref op.Op2Ref : ref op.Op1Ref);

                    if (current is null)
                    {
                        // We can have null op1 and non-null op2 for some nodes, such as GT_LEA
                        index++;
                    }
                }

                if (index == 0)
                {
                    current = ref (tree.IsReverseOp ? ref op.Op1Ref : ref op.Op2Ref);
                }
            }
            else if (oper.IsUnary)
            {
                var unOp = tree.AsUnOp();

                if (index < 0)
                {
                    current = ref unOp.Op1Ref;
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
                            current = ref phiUse.NodeRef;
                            _phiUse = phiUse;
                        }
                        break;
                    }

                    case GT_CMPXCHG:
                    {
                        var cmpXchg = tree.AsCmpXchg();

                        if (index < 0)
                        {
                            current = ref cmpXchg.AddrRef;
                        }
                        else if (index == 0)
                        {
                            current = ref cmpXchg.DataRef;
                        }
                        else if (index == 1)
                        {
                            current = ref cmpXchg.ComparandRef;
                        }
                        break;
                    }

                    case GT_SELECT:
                    {
                        var conditional = tree.AsConditional();

                        if (index < 0)
                        {
                            current = ref conditional.CondRef;
                        }
                        else if (index == 0)
                        {
                            current = ref conditional.Op1Ref;
                        }
                        else if (index == 1)
                        {
                            current = ref conditional.Op2Ref;
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
                                current = ref operands[1]!;
                            }
                            else if (index == 0)
                            {
                                current = ref operands[0]!;
                            }
                        }
                        else if ((index + 1) < operands.Length)
                        {
                            current = ref operands[index + 1]!;
                        }
                        break;
                    }
#endif

                    case GT_ARR_ELEM:
                    {
                        var arrElem = tree.AsArrElem();

                        if (index < 0)
                        {
                            current = ref arrElem.ArrObjRef;
                        }
                        else if (index < arrElem.ArrRank)
                        {
                            current = ref arrElem.ArrInds[index]!;
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
                                current = ref callArg.EarlyNodeRef;
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
                                current = ref callArg.LateNodeRef;
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
                            current = ref call.ControlExprRef;
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
                            current = ref fieldListUse.NodeRef;
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
                _current = ref current;
                _index = index + 1;
                succeeded = true;
            }
            return succeeded;
        }

        public void Reset()
        {
            _current = ref Unsafe.NullRef<GenTree?>();
            _index = -1;
        }
    }
}
