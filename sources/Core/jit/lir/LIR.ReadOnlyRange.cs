// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial class LIR
{
    /// <summary>
    ///   <para>Represents a contiguous range of LIR nodes that may be a subrange of a containing range.</para>para>
    ///   <para>Provides a small set of utilities for iteration.</para>
    ///   <para>Instances of this type are primarily created by and provided to analysis and utility methods on LIR.Range.</para>
    /// </summary>
    public partial class ReadOnlyRange : IEnumerable<GenTree>
    {
        // Although some pains have been taken to help guard against the existence
        // of invalid subranges, it remains possible to create them. For example,
        // consider the following:
        //
        //     // View the block as a range
        //     LIR.Range& blockRange = LIR.AsRange(block);
        //
        //     // Create a read only range from from it.
        //     LIR.ReadOnlyRange readRange = blockRange;
        //
        //     // Remove the last node from the block
        //     blockRange.Remove(blockRange.LastNode());
        //
        // After the removal of the last node in the block, the last node of
        // readRange is no longer linked to any of the other nodes in readRange. Due
        // to issues such as the above, some care must be taken in order to
        // ensure that ranges are not used once they have been invalidated.

        private protected GenTree? _firstNode;
        private protected GenTree? _lastNode;

        /// <summary>Creates a `ReadOnlyRange` value given the first and last node in the range.</summary>
        /// <param name="firstNode">The first node in the range.</param>
        /// <param name="lastNode">The last node in the range.</param>
        public ReadOnlyRange(GenTree? firstNode, GenTree? lastNode)
        {
            _firstNode = firstNode;
            _lastNode = lastNode;

#if DEBUG
            assert((firstNode is null) == (lastNode is null));
            assert((firstNode == lastNode) || (Contains(lastNode!)));
#endif
        }

        /// <summary>Returns the first node in the range.</summary>
        public GenTree? FirstNode => _firstNode;

        /// <summary>Returns true if the range is empty; false otherwise.</summary>
        [MemberNotNullWhen(false, [nameof(_firstNode), nameof(_lastNode), nameof(FirstNode), nameof(LastNode)])]
        public bool IsEmpty
        {
            get
            {
                assert((_firstNode is null) == (_lastNode is null));
                return _firstNode is null;
            }
        }

        /// <summary>Returns the last node in the range.</summary>
        public GenTree? LastNode => _lastNode;

        public Enumerator GetEnumerator() => new Enumerator(_firstNode);

        public ReverseEnumerator GetReverseEnumerator() => new ReverseEnumerator(_lastNode);

#if DEBUG
        /// <summary>Indicates whether or not this range contains a given node.</summary>
        /// <param name="node">The node to find.</param>
        /// <returns>True if this range contains the given node; false otherwise.</returns>
        public bool Contains(GenTree node)
        {
            // TODO-LIR: derive this from the # of nodes in the function as well as
            // the debug level. Checking small functions is pretty cheap; checking
            // large functions is not.
            if (JitConfig.JitExpensiveDebugCheckLevel < 2)
            {
                return true;
            }

            var found = false;

            foreach (var n in this)
            {
                if (n == node)
                {
                    found = true;
                    break;
                }
            }

            return found;
        }
#endif

        IEnumerator<GenTree> IEnumerable<GenTree>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
