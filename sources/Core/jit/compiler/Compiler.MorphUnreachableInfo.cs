// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed class MorphUnreachableInfo
    {
        private readonly BitVecTraits _traits;
        private readonly BitVec _unreachable;

        internal MorphUnreachableInfo(Compiler compiler)
        {
            assert(compiler._dfsTree is not null);
            _traits = new BitVecTraits(compiler, compiler._dfsTree.PostOrderCount);
            _unreachable = BitVecOps.MakeEmpty(_traits);
        }

        internal void SetUnreachable(BasicBlock block)
            => BitVecOps.AddElemD(_traits, _unreachable, block.bbPostorderNum);

        internal bool IsUnreachable(BasicBlock block)
            => BitVecOps.IsMember(_traits, _unreachable, block.bbPostorderNum);
    }
}
