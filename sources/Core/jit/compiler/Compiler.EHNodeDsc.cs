// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public sealed partial class EHNodeDsc
    {
        /// <summary>kind of EH block</summary>
        public EHBlockType ehnBlockType;

        /// <summary>IL offset of start of the EH block</summary>
        public IL_OFFSET ehnStartOffset;

        /// <summary>IL offset past end of the EH block</summary>
        /// <remarks>TODO: looks like verInsertEhNode() sets this to the last IL offset, not "one past the last one", i.e., the range Start to End is inclusive</remarks>
        public IL_OFFSET ehnEndOffset;

        /// <summary>next (non-nested) block in sequential order</summary>
        public EHNodeDsc? ehnNext;

        /// <summary>leftmost nested block</summary>
        public EHNodeDsc? ehnChild;

        private EHNodeDsc? _anonymous;

        /// <summary>the corresponding try node</summary>
        public EHNodeDsc? ehnTryNode
        {
            get
            {
                return _anonymous;
            }

            set
            {
                _anonymous = value;
            }
        }

        /// <summary>the corresponding handler node</summary>
        public EHNodeDsc? ehnHandlerNode
        {
            get
            {
                return _anonymous;
            }

            set
            {
                _anonymous = value;
            }
        }

        /// <summary>if this is a try node and has a filter, otherwise 0</summary>
        public EHNodeDsc? ehnFilterNode;

        /// <summary>if blockType=tryNode, start offset and end offset is same,</summary>
        public EHNodeDsc? ehnEquivalent;

        public bool ehnIsTryBlock => ehnBlockType == TryNode;

        public bool ehnIsFilterBlock => ehnBlockType == FilterNode;

        public bool ehnIsHandlerBlock => ehnBlockType == HandlerNode;

        public bool ehnIsFinallyBlock => ehnBlockType == FinallyNode;

        public bool ehnIsFaultBlock => ehnBlockType == FaultNode;

        /// <summary>returns true if there is any overlap between the two nodes</summary>
        /// <param name="node1"></param>
        /// <param name="node2"></param>
        /// <returns></returns>
        public static bool ehnIsOverlap(EHNodeDsc node1, EHNodeDsc node2)
        {
            if (node1.ehnStartOffset < node2.ehnStartOffset)
            {
                return (node1.ehnEndOffset >= node2.ehnStartOffset);
            }
            else
            {
                return (node1.ehnStartOffset <= node2.ehnEndOffset);
            }
        }

        // fails with BADCODE if inner is not completely nested inside outer
        public static bool ehnIsNested(EHNodeDsc inner, EHNodeDsc outer)
            => (inner.ehnStartOffset >= outer.ehnStartOffset) && (inner.ehnEndOffset <= outer.ehnEndOffset);

        public void ehnSetTryNodeType()
        {
            ehnBlockType = TryNode;
        }

        public void ehnSetFilterNodeType()
        {
            ehnBlockType = FilterNode;
        }

        public void ehnSetHandlerNodeType()
        {
            ehnBlockType = HandlerNode;
        }

        public void ehnSetFinallyNodeType()
        {
            ehnBlockType = FinallyNode;
        }

        public void ehnSetFaultNodeType()
        {
            ehnBlockType = FaultNode;
        }
    }
}
