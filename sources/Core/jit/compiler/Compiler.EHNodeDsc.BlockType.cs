// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public partial class EHNodeDsc
    {
        public const EHBlockType TryNode = EHBlockType.TryNode;
        public const EHBlockType FilterNode = EHBlockType.FilterNode;
        public const EHBlockType HandlerNode = EHBlockType.HandlerNode;
        public const EHBlockType FinallyNode = EHBlockType.FinallyNode;
        public const EHBlockType FaultNode = EHBlockType.FaultNode;

        public enum EHBlockType
        {
            TryNode,
            FilterNode,
            HandlerNode,
            FinallyNode,
            FaultNode
        }
    }
}
