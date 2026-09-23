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
            if (_index == int.MaxValue)
            {
                return false;
            }

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
                    ref var first = ref (tree.IsReverseOp ? ref op.Op2Ref : ref op.Op1Ref);

                    if (first is not null)
                    {
                        return SetCurrent(ref first, 0);
                    }

                    // We can have null op1 and non-null op2 for some nodes, such as GT_LEA.
                    index++;
                }

                if (index == 0)
                {
                    return SetCurrent(ref (tree.IsReverseOp ? ref op.Op1Ref : ref op.Op2Ref), 1);
                }
            }
            else if (oper.IsUnary)
            {
                var unOp = tree.AsUnOp();

                if (index < 0)
                {
                    return SetCurrent(ref unOp.Op1Ref, 0);
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
                            _phiUse = phiUse;

                            return SetCurrent(ref phiUse.NodeRef, index + 1);
                        }

                        break;
                    }

                    case GT_CMPXCHG:
                    {
                        var cmpXchg = tree.AsCmpXchg();

                        if (index < 0)
                        {
                            return SetCurrent(ref cmpXchg.AddrRef, 0);
                        }
                        else if (index == 0)
                        {
                            return SetCurrent(ref cmpXchg.DataRef, 1);
                        }
                        else if (index == 1)
                        {
                            return SetCurrent(ref cmpXchg.ComparandRef, 2);
                        }

                        break;
                    }

#if TARGET_ARM64
                    case GT_SELECT_NEG:
                    case GT_SELECT_INV:
                    case GT_SELECT_INC:
#endif
                    case GT_SELECT:
                    {
                        var conditional = tree.AsConditional();

                        if (index < 0)
                        {
                            return SetCurrent(ref conditional.CondRef, 0);
                        }
                        else if (index == 0)
                        {
                            return SetCurrent(ref conditional.Op1Ref, 1);
                        }
                        else if (index == 1)
                        {
                            return SetCurrent(ref conditional.Op2Ref, 2);
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
                            assert(operands.Length == 2);

                            if (index < 0)
                            {
                                return SetCurrent(ref operands[1], 0);
                            }
                            else if (index == 0)
                            {
                                return SetCurrent(ref operands[0], 1);
                            }
                        }
                        else if ((index + 1) < operands.Length)
                        {
                            return SetCurrent(ref operands[index + 1], index + 1);
                        }

                        break;
                    }
#endif

                    case GT_ARR_ELEM:
                    {
                        var arrElem = tree.AsArrElem();

                        if (index < 0)
                        {
                            return SetCurrent(ref arrElem.ArrObjRef, 0);
                        }
                        else if (index < arrElem.ArrRank)
                        {
                            return SetCurrent(ref arrElem.ArrInds[index], index + 1);
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
                                _callArg = callArg;

                                return SetCurrent(ref callArg.EarlyNodeRef, -1);
                            }

                            // Late arguments have their own order, independent of the early list.
                            _callArg = null;
                            index = 0;
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
                                _callArg = callArg;

                                return SetCurrent(ref callArg.LateNodeRef, 0);
                            }

                            _callArg = null;
                            index = 1;
                        }

                        if (index == 1)
                        {
                            return SetCurrent(ref call.ControlExprRef, 2);
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
                            _fieldListUse = fieldListUse;

                            return SetCurrent(ref fieldListUse.NodeRef, index + 1);
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

            _current = ref Unsafe.NullRef<GenTree?>();
            _index = int.MaxValue;

            return false;
        }

        [MemberNotNullWhen(true, nameof(Current))]
#nullable disable
        private bool SetCurrent([UnscopedRef] ref GenTree operand, int nextIndex)
#nullable restore
        {
            _current = ref operand;
            _index = operand is null ? int.MaxValue : nextIndex;

            return operand is not null;
        }

        public void Reset()
        {
            _current = ref Unsafe.NullRef<GenTree?>();
            _phiUse = null;
            _callArg = null;
            _fieldListUse = null;
            _index = -1;
        }
    }
}
