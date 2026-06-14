// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public ref struct TreeHasLocalReadVisitor : IGenTreeVisitor<TreeHasLocalReadVisitor>
    {
        public static bool DoLclVarsOnly => true;

        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;
        private readonly Stack<GenTree> _ancestors;
        private readonly ref LclVarDsc _lclDsc;
        private readonly int _lclNum;

        public TreeHasLocalReadVisitor(Compiler compiler, int lclNum)
        {
            _compiler = compiler;
            _ancestors = [];
            _lclDsc = ref _compiler.lvaGetDesc(lclNum);
            _lclNum = lclNum;
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;

            if (node.Oper.IsLocalRead)
            {
                var lclNum = node.AsLclVarCommon().LclNum;

                if (lclNum == _lclNum)
                {
                    return WALK_ABORT;
                }

                if (_lclDsc.lvIsStructField && (lclNum == _lclDsc.lvParentLcl))
                {
                    return WALK_ABORT;
                }

                if (_lclDsc.lvPromoted && (lclNum >= _lclDsc.lvFieldLclStart) && (lclNum < (_lclDsc.lvFieldLclStart + _lclDsc.lvFieldCnt)))
                {
                    return WALK_ABORT;
                }
            }
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<TreeHasLocalReadVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
