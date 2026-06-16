// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct TreeHasLocalStoreVisitor : IGenTreeVisitor<TreeHasLocalStoreVisitor>
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

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;

            if ((node.Flags & GTF_ASG) is 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            var compiler = _compiler;
            var lclNum = _lclNum;

            var visitResult = node.VisitLocalDefNodes(compiler, (lclDefNode) => {
                var lclDefNodeNum = lclDefNode.AsLclVarCommon().LclNum;

                if (lclDefNodeNum == lclNum)
                {
                    return GenTree.VisitResult.Abort;
                }

                ref var lclDsc = ref compiler.lvaGetDesc(lclDefNodeNum);

                if (lclDsc.lvIsStructField && (lclDefNodeNum == lclDsc.lvParentLcl))
                {
                    return GenTree.VisitResult.Abort;
                }

                if (lclDsc.lvPromoted && (lclDefNodeNum >= lclDsc.lvFieldLclStart) && (lclDefNodeNum < (lclDsc.lvFieldLclStart + lclDsc.lvFieldCnt)))
                {
                    return GenTree.VisitResult.Abort;
                }
                return GenTree.VisitResult.Continue;
            });

            if (visitResult == GenTree.VisitResult.Abort)
            {
                return WALK_ABORT;
            }
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<TreeHasLocalStoreVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
