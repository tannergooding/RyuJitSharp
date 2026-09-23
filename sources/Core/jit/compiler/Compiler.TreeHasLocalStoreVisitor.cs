// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct TreeHasLocalStoreVisitor : IGenTreeVisitor<TreeHasLocalStoreVisitor>, ILocalDefVisitor
    {
        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;
        private readonly int _lclNum;

        public TreeHasLocalStoreVisitor(Compiler compiler, int lclNum)
        {
            _compiler = compiler;
            _ancestors = [];
            _lclNum = lclNum;

            assert(!_compiler.lvaGetDesc(lclNum).IsAddressExposed);
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;

            if ((node.Flags & GTF_ASG) is 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            if (node.VisitLogicalLocalDefs(_compiler, ref this) == GenTree.VisitResult.Abort)
            {
                return WALK_ABORT;
            }

            return WALK_CONTINUE;
        }

        public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            var lclNum = def.LclNum;

            if (lclNum == _lclNum)
            {
                return GenTree.VisitResult.Abort;
            }

            ref var lclDsc = ref _compiler.lvaGetDesc(_lclNum);

            if (lclDsc.lvPromoted && (lclNum >= lclDsc.lvFieldLclStart) && (lclNum < lclDsc.lvFieldLclStart + lclDsc.lvFieldCnt))
            {
                return GenTree.VisitResult.Abort;
            }

            return GenTree.VisitResult.Continue;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<TreeHasLocalStoreVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
