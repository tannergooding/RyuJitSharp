// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal partial struct LocalAddressVisitor
{
    // A managed stack cannot retain GenTree** or managed byrefs. Identify the
    // owning slot instead, so replacement updates that exact use even when two
    // operands refer to the same node. The owner's operand list stays stable
    // until its child values have been consumed.
    internal struct Value
    {
        private readonly Statement? _root;
        private readonly GenTree? _owner;
        private readonly int _operandIndex;
        private int _lclNum;
        private uint _offset;

#if DEBUG
        private bool _consumed;
#endif

        internal Value(Statement statement, ref GenTree use, GenTree? owner)
        {
            _owner = owner;
            _lclNum = BAD_VAR_NUM;

            if (owner is null)
            {
                assert(Unsafe.AreSame(ref use, ref statement.RootNodeRef));
                _root = statement;
                return;
            }

            if (owner.Oper.IsUnary)
            {
                assert(Unsafe.AreSame(ref use, ref owner.AsUnOp().Op1Ref));
                _operandIndex = -1;
                return;
            }

            if (owner.Oper.IsBinary)
            {
                var op = owner.AsOp();

                if (Unsafe.AreSame(ref use, ref op.Op1Ref))
                {
                    _operandIndex = -2;
                }
                else
                {
                    assert(Unsafe.AreSame(ref use, ref op.Op2Ref));
                    _operandIndex = -3;
                }
                return;
            }

            var index = 0;

            foreach (ref var operand in owner.UseEdges)
            {
                if (Unsafe.AreSame(ref use, ref operand))
                {
                    _operandIndex = index;
                    return;
                }
                index++;
            }

            unreached();
        }

        internal readonly ref GenTree Use
        {
            get
            {
                if (_owner is null)
                {
                    assert(_root is not null);
                    return ref _root.RootNodeRef;
                }

                switch (_operandIndex)
                {
                    case -1:
                    {
                        return ref _owner.AsUnOp().Op1Ref;
                    }

                    case -2:
                    {
                        return ref _owner.AsOp().Op1Ref;
                    }

                    case -3:
                    {
                        return ref _owner.AsOp().Op2Ref;
                    }
                }

                var index = 0;

                foreach (ref var operand in _owner.UseEdges)
                {
                    if (index == _operandIndex)
                    {
                        return ref operand;
                    }
                    index++;
                }

                unreached();
                return ref Unsafe.NullRef<GenTree>();
            }
        }

        internal readonly GenTree Node => Use;

        internal readonly bool IsUnknown => !IsAddress;

        internal readonly bool IsAddress => _lclNum != BAD_VAR_NUM;

        internal readonly int LclNum
        {
            get
            {
                assert(IsAddress);
                return _lclNum;
            }
        }

        internal readonly uint Offset
        {
            get
            {
                assert(IsAddress);
                return _offset;
            }
        }

        internal readonly bool IsSameAddress(in Value other)
        {
            assert(IsAddress && other.IsAddress);
            return (LclNum == other.LclNum) && (Offset == other.Offset);
        }

        internal void Address(GenTreeLclFld lclAddr)
        {
            assert(IsUnknown && (lclAddr.Oper is GT_LCL_ADDR));
            _lclNum = lclAddr.LclNum;
            _offset = lclAddr.LclOffs;
        }

        internal void Address(int lclNum, uint lclOffs)
        {
            assert(IsUnknown);
            _lclNum = lclNum;
            _offset = lclOffs;
        }

        internal bool AddOffset(ref Value value, uint addOffset)
        {
            assert(IsUnknown);

            if (value.IsAddress)
            {
                var newOffset = (ulong)value._offset + addOffset;

                if (newOffset > uint.MaxValue)
                {
                    return false;
                }

                _lclNum = value._lclNum;
                _offset = (uint)newOffset;
            }

            value.Consume();
            return true;
        }

        [Conditional("DEBUG")]
#if DEBUG
        internal void Consume()
#else
        internal readonly void Consume()
#endif
        {
#if DEBUG
            assert(!_consumed);
            _consumed = true;
#endif
        }

#if DEBUG
        internal readonly bool IsConsumed => _consumed;
#endif
    }
}
