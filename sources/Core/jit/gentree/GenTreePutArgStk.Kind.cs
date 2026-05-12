// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class GenTreePutArgStk : GenTreeUnOp
{
    /// <summary>During codegen time, what code sequence we will be using to encode this operation.</summary>
    public enum Kind : byte
    {
        // TODO-Throughput: The following information should be obtained from the child block node.

        Invalid,
        RepInstr,
        PartialRepInstr,
        Unroll,
        Push,
    }
}
