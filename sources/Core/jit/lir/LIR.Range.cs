// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        public Range(GenTree? firstNode, GenTree? lastNode)
            : base(firstNode, lastNode)
        {
        }

#if DEBUG
        /// <summary>Performs a set of correctness checks on the LIR contained in this range.</summary>
        /// <param name="compiler">A compiler context.</param>
        /// <param name="checkUnusedValues"></param>
        /// <returns>'true' if the LIR for the specified range is legal.</returns>
        public bool CheckLir(Compiler compiler, bool checkUnusedValues = false)
        {
            // This method checks the following properties:
            // - Defs are singly-used
            // - Uses follow defs
            // - Uses are correctly linked into the block
            // - Nodes that do not produce values are not used
            // - Only LIR nodes are present in the block
            //
            // The first four properties are verified by walking the range's LIR in execution order,
            // inserting defs into a set as they are visited, and removing them as they are used. The
            // different cases are distinguished only when an error is detected.

            if (IsEmpty)
            {
                // Nothing more to check.
                return true;
            }

            CheckDoublyLinkedList(FirstNode);

            var unusedDefs = new Dictionary<GenTree, bool>(capacity: 32);
            var previous = null as GenTree;

            foreach (var node in this)
            {
                // Verify that the node is allowed in LIR.
                assert(node.IsLirOp);

                if (node.IsContained)
                {
                    assert(node.CanBeContained);
                    assert(!node.IsUnusedValue);
                }

                // Some nodes should never be marked unused, as they must be contained in the backend.
                // These may be marked as unused during dead code elimination traversal, but they *must* be subsequently removed.
                assert(!node.IsUnusedValue || (node.Oper is not GT_FIELD_LIST and not GT_INIT_VAL));

                // Verify that the REVERSE_OPS flag is not set. NOTE: if we ever decide to reuse the bit assigned to
                // GTF_REVERSE_OPS for an LIR-only flag we will need to move this check to the points at which we
                // insert nodes into an LIR range.
                assert((node.Flags & GTF_REVERSE_OPS) == 0);

                // TODO: validate catch arg stores

                foreach (ref var useEdge in node.UseEdges)
                {
                    var def = useEdge;

                    assert(!checkUnusedValues || !def.IsUnusedValue, "operands should never be marked as unused values");

                    if (!def.IsValue)
                    {
                        // Stack arguments do not produce a value, but they are considered children of the call.
                        // It may be useful to remove these from being call operands, but that may also impact
                        // other code that relies on being able to reach all the operands from a call node.
                        // The argument of a JTRUE doesn't produce a value (just sets a flag).
                        assert(((node.Oper is GT_CALL) && (def.Oper is GT_PUTARG_STK)) ||
                               ((node.Oper is GT_JTRUE) && (def.Type is TYP_VOID) && ((def.Flags & GTF_SET_FLAGS) is not 0)));
                    }
                    else if (!unusedDefs.Remove(def, out var _))
                    {
                        // First, scan backwards and look for a preceding use.
                        for (var prev = node; prev is not null; prev = prev.Prev)
                        {
                            // TODO: dump the users and the def
                            ref var earlierUseEdge = ref prev.GetUseRefOrNullRef(def);
                            assert(Unsafe.IsNullRef(in earlierUseEdge) || Unsafe.AreSame(in earlierUseEdge, in useEdge), "found multiply-used LIR node");
                        }

                        // The def did not precede the use. Check to see if it exists in the block at all.
                        for (var next = node.Next; next is not null; next = next.Next)
                        {
                            // TODO: dump the user and the def
                            assert(next != def, "found def after use");
                        }

                        // The def might not be a node that produces a value.
                        assert(def.IsValue, "found use of a node that does not produce a value");

                        // By this point, the only possibility is that the def is not threaded into the LIR sequence.
                        NO_WAY("found use of a node that is not in the LIR sequence");
                    }
                }

                if (node.IsValue)
                {
                    assert(unusedDefs.TryAdd(node, true));
                }

                previous = node;
            }

            assert(previous == _lastNode);

            // At this point the unusedDefs map should contain only unused values.
            if (checkUnusedValues)
            {
                foreach (var kvp in unusedDefs)
                {
                    var node = kvp.Key;

                    if (!node.IsUnusedValue)
                    {
                        JITDUMP($"[{node.TreeId:D6}] is an unmarked unused value\n");
                        NO_WAY("Found an unmarked unused value");
                    }

                    if (node.IsContained)
                    {
                        JITDUMP($"[{node.TreeId:D6}] is a contained node with no user\n");
                        NO_WAY("A contained node should have a user");
                    }
                }
            }

            var checkLclVarSemanticsHelper = new CheckLclVarSemanticsHelper(compiler, this, unusedDefs);
            assert(checkLclVarSemanticsHelper.Check());

            return true;
        }
#endif

        /// <summary>Deletes a node from this range.</summary>
        /// <param name="node">The node to delete. Must be part of this range.</param>
        /// <remarks>Note that the deleted node must not be used after this function has been called.</remarks>
        public void Delete(GenTree node)
        {
            Remove(node);
            DEBUG_DESTROY_NODE(node);
        }

        /// <summary>Deletes a subrange from this range.</summary>
        /// <param name="firstNode"></param>
        /// <param name="lastNode"></param>
        /// <remarks>
        ///   <para>Both the start and the end of the subrange must be part of this range.</para>
        ///   <para>Note that the deleted nodes must not be used after this function has been called.</para>
        /// </remarks>
        public void Delete(GenTree firstNode, GenTree lastNode)
        {
            Remove(firstNode, lastNode);

            assert(lastNode.Next is null);

#if DEBUG
            // We can't do this in the loop above because it causes `IsPhiNode` to return a false negative for `GT_STORE_LCL_VAR` nodes that participate in phi definitions.
            for (var node = firstNode; node is not null; node = node.Next)
            {
                DEBUG_DESTROY_NODE(node);
            }
#endif
        }

        /// <summary>Deletes a subrange from this range.</summary>
        /// <param name="range">The subrange to delete.</param>
        /// <remarks>
        ///   <para>Both the start and the end of the subrange must be part of this range.</para>
        ///   <para>Note that the deleted nodes must not be used after this function has been called.</para>
        /// </remarks>
        public void Delete(ReadOnlyRange range)
        {
            assert(range.FirstNode is not null);
            assert(range.LastNode is not null);
            Delete(range.FirstNode, range.LastNode);
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
                    if (node.AsUnOp().Op1.Oper is GT_CATCH_ARG)
                    {
                        continue;
                    }
                }

                return node;
            }

            return null;
        }

        /// <summary>Computes the subrange that includes all nodes in the dataflow tree rooted at a particular node.</summary>
        /// <param name="root">The root of the dataflow tree.</param>
        /// <param name="isClosed">true if the returned range contains only nodes in the dataflow tree and false otherwise.</param>
        /// <returns>The computed subrange.</returns>
        public ReadOnlyRange GetTreeRange(GenTree root, out bool isClosed)
            => GetTreeRange(root, out isClosed, out _);

        /// <summary>Computes the subrange that includes all nodes in the dataflow tree rooted at a particular node.</summary>
        /// <param name="root">The root of the dataflow tree.</param>
        /// <param name="isClosed">true if the returned range contains only nodes in the dataflow tree and false otherwise.</param>
        /// <param name="sideEffects">summarizes the side effects contained in the returned range.</param>
        /// <returns>The computed subrange.</returns>
        public ReadOnlyRange GetTreeRange(GenTree root, out bool isClosed, out GenTreeFlags sideEffects)
        {
            // Mark the root of the tree
            root._lirFlags |= Flags.Mark;
            return GetMarkedRange(root, out isClosed, out sideEffects, markCount: 1);
        }

#if DEBUG
        public ReadOnlyRange GetTreeRangeWithFlags(GenTree root, out bool isClosed, out GenTreeFlags sideEffects)
        {
            root._lirFlags |= Flags.Mark;
            return GetMarkedRange(root, out isClosed, out sideEffects, markCount: 1, markFlagsOperands: true);
        }
#endif

        /// <summary>Inserts a node after another node in this range.</summary>
        /// <param name="insertionPoint">The node after which `node` will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="node">The node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: insertionPoint &lt;-&gt; node &lt;-&gt; previous insertionPoint->gtNext</remarks>
        public void InsertAfter(GenTree? insertionPoint, GenTree node)
        {
            assert(node.Next is null);
            assert(node.Prev is null);

            FinishInsertAfter(insertionPoint, node, node);
        }

        /// <summary>Inserts 2 nodes after another node in this range.</summary>
        /// <param name="insertionPoint">The node after which `node` will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="node1">The first node to insert. Must not be part of any range.</param>
        /// <param name="node2">The second node to insert. Must not be part of any range.</param>
        /// <remarks>Resulting Order: insertionPoint &lt;-&gt; node1 &lt;-&gt; node2 &lt;-&gt; previous insertionPoint->gtNext</remarks>
        public void InsertAfter(GenTree? insertionPoint, GenTree node1, GenTree node2)
        {
            assert(node1.Next is null);
            assert(node1.Prev is null);
            assert(node2.Next is null);
            assert(node2.Prev is null);

            node1.Next = node2;
            node2.Prev = node1;

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
            assert(node1.Next is null);
            assert(node1.Prev is null);
            assert(node2.Next is null);
            assert(node2.Prev is null);
            assert(node3.Next is null);
            assert(node3.Prev is null);

            node1.Next = node2;

            node2.Prev = node1;
            node2.Next = node3;

            node3.Prev = node2;

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
            assert(node1.Next is null);
            assert(node1.Prev is null);
            assert(node2.Next is null);
            assert(node2.Prev is null);
            assert(node3.Next is null);
            assert(node3.Prev is null);
            assert(node4.Next is null);
            assert(node4.Prev is null);

            node1.Next = node2;

            node2.Prev = node1;
            node2.Next = node3;

            node3.Prev = node2;
            node3.Next = node4;

            node4.Prev = node3;

            FinishInsertAfter(insertionPoint, node1, node4);
        }

        /// <summary>Inserts a range after another node in `this` range.</summary>
        /// <param name="insertionPoint">The node after which the nodes will be inserted. If non-null, must be part of this range. If null, insert at the beginning of the range.</param>
        /// <param name="range">The range to splice in.</param>
        public void InsertAfter(GenTree? insertionPoint, Range range)
        {
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
            assert(node.Prev is null);
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
            assert(node1.Next is null);
            assert(node1.Prev is null);
            assert(node2.Next is null);
            assert(node2.Prev is null);

            node1.Next = node2;
            node2.Prev = node1;

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
            assert(node1.Next is null);
            assert(node1.Prev is null);
            assert(node2.Next is null);
            assert(node2.Prev is null);
            assert(node3.Next is null);
            assert(node3.Prev is null);

            node1.Next = node2;

            node2.Prev = node1;
            node2.Next = node3;

            node3.Prev = node2;

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
            assert(node1.Next is null);
            assert(node1.Prev is null);
            assert(node2.Next is null);
            assert(node2.Prev is null);
            assert(node3.Next is null);
            assert(node3.Prev is null);
            assert(node4.Next is null);
            assert(node4.Prev is null);

            node1.Next = node2;

            node2.Prev = node1;
            node2.Next = node3;

            node3.Prev = node2;
            node3.Next = node4;

            node4.Prev = node3;

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

        /// <summary>Removes a node from this range.</summary>
        /// <param name="node">The node to remove. Must be part of this range.</param>
        /// <param name="markOperandsUnused">If true, marks the node's operands as unused.</param>
        public void Remove(GenTree node, bool markOperandsUnused = false)
        {
#if DEBUG
            assert(Contains(node));
#endif

            if (markOperandsUnused)
            {
                _ = node.VisitOperands((operand) => {
                    // The operand of JTRUE does not produce a value (just sets the flags).
                    if (operand.IsValue)
                    {
                        operand.IsUnusedValue = true;
                    }
                    return GenTree.VisitResult.Continue;
                });
            }

            var prev = node.Prev;
            var next = node.Next;

            if (prev is not null)
            {
                prev.Next = next;
            }
            else
            {
                assert(node == _firstNode);
                _firstNode = next;
            }

            if (next is not null)
            {
                next.Prev = prev;
            }
            else
            {
                assert(node == _lastNode);
                _lastNode = prev;
            }

            node.Prev = null;
            node.Next = null;
        }

        /// <summary>Removes a subrange from this range.</summary>
        /// <param name="firstNode">The first node in the subrange.</param>
        /// <param name="lastNode">The last node in the subrange.</param>
        /// <remarks>Both the start and the end of the subrange must be part of this range.</remarks>
        public void Remove(GenTree firstNode, GenTree lastNode)
        {
#if DEBUG
            assert(Contains(firstNode));
            assert((firstNode == lastNode) || firstNode.Precedes(lastNode));
#endif

            var prev = firstNode.Prev;
            var next = lastNode.Next;

            if (prev is not null)
            {
                prev.Next = next;
            }
            else
            {
                assert(firstNode == _firstNode);
                _firstNode = next;
            }

            if (next is not null)
            {
                next.Prev = prev;
            }
            else
            {
                assert(lastNode == _lastNode);
                _lastNode = prev;
            }

            firstNode.Prev = null;
            lastNode.Next = null;
        }

        /// <summary>Removes a subrange from this range</summary>
        /// <param name="range">The subrange to remove. Must be part of this range.</param>
        public void Remove(ReadOnlyRange range)
        {
            assert(range.FirstNode is not null);
            assert(range.LastNode is not null);
            Remove(range.FirstNode, range.LastNode);
        }

        /// <summary>Removes a subrange from this range.</summary>
        /// <param name="firstNode">The first node in the subrange.</param>
        /// <param name="lastNode">The last node in the subrange.</param>
        /// <returns>A mutable range containing the removed nodes.</returns>
        /// <remarks>Both the start and the end of the subrange must be part of this range.</remarks>
        public Range RemoveAndGetRange(GenTree firstNode, GenTree lastNode)
        {
            Remove(firstNode, lastNode);
            return new Range(firstNode, lastNode);
        }

        /// <summary>Removes a subrange from this range</summary>
        /// <param name="range">The subrange to remove. Must be part of this range.</param>
        /// <returns>A mutable range containing the removed nodes.</returns>
        public Range RemoveAndGetRange(ReadOnlyRange range)
        {
            assert(range.FirstNode is not null);
            assert(range.LastNode is not null);

            Remove(range);
            return new Range(range.FirstNode, range.LastNode);
        }

        /// <summary>Try to find the use for a given node.</summary>
        /// <param name="node">The node for which to find the corresponding use.</param>
        /// <param name="use">The use of the corresponding node, if any. Invalid if this method returns false.</param>
        /// <returns>Return Value: Returns true if a use was found; false otherwise.</returns>
        public bool TryGetUse(GenTree node, out Use use)
        {
#if DEBUG
            assert(Contains(node));
#endif

            // Don't bother looking for uses of nodes that are not values.
            // If the node is the last node, we won't find a use (and we would
            // end up creating an illegal range if we tried).
            if (node.IsValue && !node.IsUnusedValue && (node != LastNode))
            {
                foreach (var potentialUser in new ReadOnlyRange(node.Next, _lastNode))
                {
                    ref var edge = ref potentialUser.GetUseRefOrNullRef(node);

                    if (!Unsafe.IsNullRef(in edge))
                    {
                        use = new Use(this, ref edge, potentialUser);

                        return true;
                    }
                }
            }

            use = new Use();

            return false;
        }

        /// <summary>Helper function to finalize InsertAfter processing: link the range to insertionPoint. gtNext/gtPrev links between first and last are already set.</summary>
        /// <param name="insertionPoint">The node after which the nodes will be inserted. If non-null, must be part of this range. If null, indicates to insert at the end of the range.</param>
        /// <param name="first">The first node of the range to insert.</param>
        /// <param name="last">The last node of the range to insert.</param>
        /// <remarks>Resulting Order: insertionPoint->gtNext &lt;-&gt; first &lt;-&gt; ... &lt;-&gt; last &lt;-&gt; previous insertionPoint.Next</remarks>
        private void FinishInsertAfter(GenTree? insertionPoint, GenTree first, GenTree last)
        {
            assert(first.Prev is null);
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
                    assert(_firstNode.Prev is null);
                    _firstNode.Prev = last;
                    last.Next = _firstNode;
                }
                _firstNode = first;
            }
            else
            {
#if DEBUG
                assert(Contains(insertionPoint));
#endif

                last.Next = insertionPoint.Next;
                if (last.Next is null)
                {
                    assert(insertionPoint == _lastNode);
                    _lastNode = last;
                }
                else
                {
                    last.Next.Prev = last;
                }

                first.Prev = insertionPoint;
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
            assert(first.Prev is null);
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
                    first.Prev = _lastNode;
                }
                _lastNode = last;
            }
            else
            {
#if DEBUG
                assert(Contains(insertionPoint));
#endif
                first.Prev = insertionPoint.Prev;

                if (first.Prev is null)
                {
                    assert(insertionPoint == _firstNode);
                    _firstNode = first;
                }
                else
                {
                    first.Prev.Next = first;
                }

                last.Next = insertionPoint;
                insertionPoint.Prev = last;
            }
        }

        private ReadOnlyRange GetMarkedRange(GenTree start, out bool isClosed, out GenTreeFlags sideEffects, int markCount, bool markFlagsOperands = false)
        {
            // This method logically uses the following algorithm to compute the
            // range:
            //
            //    worklist = { set }
            //    firstNode = start
            //    isClosed = true
            //
            //    while not worklist.isEmpty:
            //        if not worklist.contains(firstNode):
            //            isClosed = false
            //        else:
            //            for operand in firstNode:
            //                worklist.add(operand)
            //
            //            worklist.remove(firstNode)
            //
            //        firstNode = firstNode.previousNode
            //
            //    return firstNode
            //
            // Instead of using a set for the worklist, the implementation uses the
            // `LIR::Mark` bit of the `GenTree::LIRFlags` field to track whether or
            // not a node is in the worklist.
            //
            // Note also that this algorithm depends LIR nodes being SDSU, SDSU defs
            // and uses occurring in the same block, and correct dataflow (i.e. defs
            // occurring before uses).

            assert(markCount != 0);

            var sawUnmarkedNode = false;
            var sideEffectsInRange = GTF_EMPTY;

            var firstNode = start;
            var lastNode = null as GenTree;

            for (; ; )
            {
                if ((firstNode._lirFlags & Flags.Mark) != 0)
                {
                    lastNode ??= firstNode;

                    // Mark the node's operands
                    _ = firstNode.VisitOperands((operand) => {
                        operand._lirFlags |= Flags.Mark;
                        markCount++;
                        return GenTree.VisitResult.Continue;
                    });

                    if (markFlagsOperands && firstNode.Oper.ConsumesFlags)
                    {
                        var prev = firstNode.Prev;

                        if ((prev is not null) && ((prev.Flags & GTF_SET_FLAGS) != 0) &&
                            ((prev._lirFlags & Flags.Mark) == 0))
                        {
                            prev._lirFlags |= Flags.Mark;
                            markCount++;
                        }
                    }

                    // Unmark the node and update `firstNode`
                    firstNode._lirFlags &= ~Flags.Mark;
                    markCount--;
                }
                else if (lastNode is not null)
                {
                    sawUnmarkedNode = true;
                }

                if (lastNode is not null)
                {
                    sideEffectsInRange |= (firstNode.Flags & GTF_ALL_EFFECT);
                }

                if (markCount == 0)
                {
                    break;
                }

                firstNode = firstNode.Prev;

                // This assert will fail if the dataflow that feeds the root node
                // is incorrect in that it crosses a block boundary or if it involves
                // a use that occurs before its corresponding def.
                assert(firstNode is not null);
            }
            assert(lastNode is not null);

            isClosed = !sawUnmarkedNode;
            sideEffects = sideEffectsInRange;
            return new ReadOnlyRange(firstNode, lastNode);
        }
    }
}
