// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class LIR
{
    /// <summary>Represents a contiguous range of LIR nodes. Provides a variety of variety of utilities that modify the LIR contained in the range.</summary>
    /// <remarks>
    ///   <para>Unlike `ReadOnlyRange`, values of this type may be edited.</para>
    ///   <para>Because it is not a final class, it is possible to slice values of this type; this is especially dangerous when the Range value is actually of type `BasicBlock`.</para>
    ///   <para>As a result, this type is not copyable and it is not possible to view a `BasicBlock` as anything other than a `Range`.</para>
    /// </remarks>
    public class Range : ReadOnlyRange
    {
        /// <summary>Creates a `Range` value given the first and last node in the range.</summary>
        /// <param name="firstNode">The first node in the range.</param>
        /// <param name="lastNode">The last node in the range.</param>
        public Range(GenTree? firstNode, GenTree? lastNode) : base(firstNode, lastNode)
        {
        }

        /// <summary>Returns the first node after all catch arg nodes in this range.</summary>
        /// <returns></returns>
        public GenTree? FirstNonCatchArgNode()
        {
            foreach (var node in this)
            {
                var nodeOper = node.Oper;

                if (nodeOper is GT_CATCH_ARG)
                {
                    continue;
                }
                else if (nodeOper is GT_STORE_LCL_VAR)
                {
                    var op1 = node.AsOp().Op1;
                    assert(op1 is not null);

                    if (op1.Oper is GT_CATCH_ARG)
                    {
                        continue;
                    }
                }

                return node;
            }

            return null;
        }

        /// <summary>Inserts a node after another node in this range.</summary>
        /// <param name="insertionPoint">The node after which `node` will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="node">The node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: insertionPoint &lt;-&gt; node &lt;-&gt; previous insertionPoint->gtNext</remarks>
        public void InsertAfter(GenTree? insertionPoint, GenTree node)
        {
            assert(node is not null);

            assert(node.Next is null);
            assert(node.Previous is null);

            FinishInsertAfter(insertionPoint, node, node);
        }

        /// <summary>Inserts 2 nodes after another node in this range.</summary>
        /// <param name="insertionPoint">The node after which `node` will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="node1">The first node to insert. Must not be part of any range.</param>
        /// <param name="node2">The second node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: insertionPoint &lt;-&gt; node1 &lt;-&gt; node2 &lt;-&gt; previous insertionPoint->gtNext</remarks>
        public void InsertAfter(GenTree? insertionPoint, GenTree node1, GenTree node2)
        {
            assert(node1 is not null);
            assert(node2 is not null);

            assert(node1.Next is null);
            assert(node1.Previous is null);
            assert(node2.Next is null);
            assert(node2.Previous is null);

            node1.Next = node2;
            node2.Previous = node1;

            FinishInsertAfter(insertionPoint, node1, node2);
        }

        /// <summary>Inserts 3 nodes after another node in this range.</summary>
        /// <param name="insertionPoint">The node after which `node` will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="node1">The first node to insert. Must not be part of any range.</param>
        /// <param name="node2">The second node to insert. Must not be part of any range.</param>
        /// <param name="node3">The third node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: insertionPoint &lt;-&gt; node1 &lt;-&gt; node2 &lt;-&gt; node3 &lt;-&gt; previous insertionPoint->gtNext</remarks>
        public void InsertAfter(GenTree insertionPoint, GenTree node1, GenTree node2, GenTree node3)
        {
            assert(node1 is not null);
            assert(node2 is not null);
            assert(node3 is not null);

            assert(node1.Next is null);
            assert(node1.Previous is null);
            assert(node2.Next is null);
            assert(node2.Previous is null);
            assert(node3.Next is null);
            assert(node3.Previous is null);

            node1.Next = node2;

            node2.Previous = node1;
            node2.Next = node3;

            node3.Previous = node2;

            FinishInsertAfter(insertionPoint, node1, node3);
        }

        /// <summary>Inserts 4 nodes after another node in this range.</summary>
        /// <param name="insertionPoint">The node after which `node` will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="node1">The first node to insert. Must not be part of any range.</param>
        /// <param name="node2">The second node to insert. Must not be part of any range.</param>
        /// <param name="node3">The third node to insert. Must not be part of any range.</param>
        /// <param name="node4">The fourth node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: insertionPoint &lt;-&gt; node1 &lt;-&gt; node2 &lt;-&gt; node3 &lt;-&gt; node4 &lt;-&gt; previous insertionPoint->gtNext</remarks>
        public void InsertAfter(GenTree? insertionPoint, GenTree node1, GenTree node2, GenTree node3, GenTree node4)
        {
            assert(node1 is not null);
            assert(node2 is not null);
            assert(node3 is not null);
            assert(node4 is not null);

            assert(node1.Next is null);
            assert(node1.Previous is null);
            assert(node2.Next is null);
            assert(node2.Previous is null);
            assert(node3.Next is null);
            assert(node3.Previous is null);
            assert(node4.Next is null);
            assert(node4.Previous is null);

            node1.Next = node2;

            node2.Previous = node1;
            node2.Next = node3;

            node3.Previous = node2;
            node3.Next = node4;

            node4.Previous = node3;

            FinishInsertAfter(insertionPoint, node1, node4);
        }

        /// <summary>Inserts a range after another node in `this` range.</summary>
        /// <param name="insertionPoint">The node after which the nodes will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="range">The range to splice in.</param>
        public void InsertAfter(GenTree? insertionPoint, Range range)
        {
            assert(range is not null);
            assert(!range.IsEmpty);
            FinishInsertAfter(insertionPoint, range._firstNode, range._lastNode);

#if DEBUG
            range._firstNode = null;
            range._lastNode = null;
#endif
        }

        /// <summary>Inserts a node at the beginning of this range.</summary>
        /// <param name="node">The node to insert. Must not be part of any range.</param>
        public void InsertAtBeginning(GenTree node) => InsertBefore(_firstNode, node);

        /// <summary>Inserts a range at the beginning of `this` range.</summary>
        /// <param name="range">The range to splice in.</param>
        public void InsertAtBeginning(Range range) => InsertBefore(_firstNode, range);

        /// <summary>Inserts a node at the end of this range.</summary>
        /// <param name="node">The node to insert. Must not be part of any range.</param>
        public void InsertAtEnd(GenTree node) => InsertAfter(_lastNode, node);

        /// <summary>Inserts a range at the end of `this` range.</summary>
        /// <param name="range">The range to splice in.</param>
        public void InsertAtEnd(Range range) => InsertAfter(_lastNode, range);

        /// <summary>Inserts a node before another node in this range.</summary>
        /// <param name="insertionPoint">The node before which `node` will be inserted. If non-null, must be part of this range. If null, insert at the end of the range.</param>
        /// <param name="node">The node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: previous insertionPoint->gtPrev &lt;-&gt; node &lt;-&gt; insertionPoint</remarks>
        public void InsertBefore(GenTree? insertionPoint, GenTree node)
        {
            assert(node is not null);

            assert(node.Previous is null);
            assert(node.Next is null);

            FinishInsertBefore(insertionPoint, node, node);
        }

        /// <summary>Inserts 2 nodes before another node in this range.</summary>
        /// <param name="insertionPoint">The node before which the nodes will be inserted. If non-null, must be part of this range. If null, insert at the end of the range.</param>
        /// <param name="node1">The first node to insert. Must not be part of any range.</param>
        /// <param name="node2">The second node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: previous insertionPoint->gtPrev &lt;-&gt; node1 &lt;-&gt; node2 &lt;-&gt; insertionPoint</remarks>
        public void InsertBefore(GenTree? insertionPoint, GenTree node1, GenTree node2)
        {
            assert(node1 is not null);
            assert(node2 is not null);

            assert(node1.Next is null);
            assert(node1.Previous is null);
            assert(node2.Next is null);
            assert(node2.Previous is null);

            node1.Next = node2;
            node2.Previous = node1;

            FinishInsertBefore(insertionPoint, node1, node2);
        }

        /// <summary>Inserts 3 nodes before another node in this range.</summary>
        /// <param name="insertionPoint">The node before which the nodes will be inserted. If non-null, must be part of this range. If null, insert at the end of the range.</param>
        /// <param name="node1">The first node to insert. Must not be part of any range.</param>
        /// <param name="node2">The second node to insert. Must not be part of any range.</param>
        /// <param name="node3">The third node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: previous insertionPoint->gtPrev &lt;-&gt; node1 &lt;-&gt; node2 &lt;-&gt; node3 &lt;-&gt; insertionPoint</remarks>
        public void InsertBefore(GenTree? insertionPoint, GenTree node1, GenTree node2, GenTree node3)
        {
            assert(node1 is not null);
            assert(node2 is not null);
            assert(node3 is not null);

            assert(node1.Next is null);
            assert(node1.Previous is null);
            assert(node2.Next is null);
            assert(node2.Previous is null);
            assert(node3.Next is null);
            assert(node3.Previous is null);

            node1.Next = node2;

            node2.Previous = node1;
            node2.Next = node3;

            node3.Previous = node2;

            FinishInsertBefore(insertionPoint, node1, node3);
        }

        /// <summary>Inserts 4 nodes before another node in this range.</summary>
        /// <param name="insertionPoint">The node before which the nodes will be inserted. If non-null, must be part of this range. If null, insert at the end of the range.</param>
        /// <param name="node1">The first node to insert. Must not be part of any range.</param>
        /// <param name="node2">The second node to insert. Must not be part of any range.</param>
        /// <param name="node3">The third node to insert. Must not be part of any range.</param>
        /// <param name="node4">The fourth node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: previous insertionPoint->gtPrev &lt;-&gt; node1 &lt;-&gt; node2 &lt;-&gt; node3 &lt;-&gt; node4 &lt;-&gt; insertionPoint</remarks>
        public void InsertBefore(GenTree? insertionPoint, GenTree node1, GenTree node2, GenTree node3, GenTree node4)
        {
            assert(node1 is not null);
            assert(node2 is not null);
            assert(node3 is not null);
            assert(node4 is not null);

            assert(node1.Next is null);
            assert(node1.Previous is null);
            assert(node2.Next is null);
            assert(node2.Previous is null);
            assert(node3.Next is null);
            assert(node3.Previous is null);
            assert(node4.Next is null);
            assert(node4.Previous is null);

            node1.Next = node2;

            node2.Previous = node1;
            node2.Next = node3;

            node3.Previous = node2;
            node3.Next = node4;

            node4.Previous = node3;

            FinishInsertBefore(insertionPoint, node1, node4);
        }

        /// <summary>Inserts a range before another node in `this` range.</summary>
        /// <param name="insertionPoint">The node before which the nodes will be inserted. If non-null, must be part of this range. If null, insert at the end of the range.</param>
        /// <param name="range">The range to splice in.</param>
        public void InsertBefore(GenTree? insertionPoint, Range range)
        {
            assert(!range.IsEmpty);
            FinishInsertBefore(insertionPoint, range._firstNode, range._lastNode);

#if DEBUG
            range._firstNode = null;
            range._lastNode = null;
#endif
        }

        /// <summary>Helper function to finalize InsertAfter processing: link the range to insertionPoint. gtNext/gtPrev links between first and last are already set.</summary>
        /// <param name="insertionPoint">The node after which the nodes will be inserted. If non-null, must be part of this range. If null, indicates to insert at the end of the range.</param>
        /// <param name="first">The first node of the range to insert.</param>
        /// <param name="last">The last node of the range to insert.</param>
        /// <remarks>Resulting Order: insertionPoint->gtNext &lt;-&gt; first &lt;-&gt; ... &lt;-&gt; last &lt;-&gt; previous insertionPoint.Next</remarks>
        private void FinishInsertAfter(GenTree? insertionPoint, GenTree first, GenTree last)
        {
            assert(first is not null);
            assert(last is not null);

            assert(first.Previous is null);
            assert(last.Next is null);

            if (insertionPoint is null)
            {
                if (_lastNode is null)
                {
                    _lastNode = last;
                }
                else
                {
                    assert(_firstNode is not null);
                    assert(_firstNode.Previous is null);
                    _firstNode.Previous = last;
                    last.Next = _firstNode;
                }
                _firstNode = first;
            }
            else
            {
                assert(Contains(insertionPoint));

                last.Next = insertionPoint.Next;
                if (last.Next is null)
                {
                    assert(insertionPoint == _lastNode);
                    _lastNode = last;
                }
                else
                {
                    last.Next.Previous = last;
                }

                first.Previous = insertionPoint;
                insertionPoint.Next = first;
            }
        }

        /// <summary>Helper function to finalize InsertBefore processing: link the range to insertionPoint. gtNext/gtPrev links between first and last are already set.</summary>
        /// <param name="insertionPoint">The node before which the nodes will be inserted. If non-null, must be part of this range. If null, indicates to insert at the end of the range.</param>
        /// <param name="first">The first node of the range to insert.</param>
        /// <param name="last">The last node of the range to insert.</param>
        /// <remarks>Resulting Order: previous insertionPoint->gtPrev &lt;-&gt; first &lt;-&gt; ... &lt;-&gt; last &lt;-&gt; insertionPoint</remarks>
        private void FinishInsertBefore(GenTree? insertionPoint, GenTree first, GenTree last)
        {
            assert(first is not null);
            assert(last is not null);

            assert(first.Previous is null);
            assert(last.Next is null);

            if (insertionPoint is null)
            {
                if (_firstNode is null)
                {
                    _firstNode = first;
                }
                else
                {
                    assert(_lastNode is not null);
                    assert(_lastNode.Next is null);
                    _lastNode.Next = first;
                    first.Previous = _lastNode;
                }
                _lastNode = last;
            }
            else
            {
                assert(Contains(insertionPoint));
                first.Previous = insertionPoint.Previous;

                if (first.Previous is null)
                {
                    assert(insertionPoint == _firstNode);
                    _firstNode = first;
                }
                else
                {
                    first.Previous.Next = first;
                }

                last.Next = insertionPoint;
                insertionPoint.Previous = last;
            }
        }
    }
}
