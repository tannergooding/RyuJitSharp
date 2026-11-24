// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct MayHaveStoreInterferenceVisitor : IGenTreeVisitor<MayHaveStoreInterferenceVisitor>, ILocalDefVisitor
    {
        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;
        private readonly GenTree _readTree;
        private int _numStoresChecked;
        private bool _hasDefsInNode;

        public MayHaveStoreInterferenceVisitor(Compiler compiler, GenTree readTree)
        {
            _compiler = compiler;
            _readTree = readTree;
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;

            if ((node.Flags & GTF_ASG) is 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            _hasDefsInNode = false;
            var visitor = this;
            if (node.VisitLogicalLocalDefs(_compiler, ref visitor) is GenTree.VisitResult.Abort)
            {
                return WALK_ABORT;
            }
            _hasDefsInNode = visitor._hasDefsInNode;
            if (_hasDefsInNode)
            {
                _numStoresChecked++;
            }
            return WALK_CONTINUE;
        }

        public GenTree.VisitResult Visit<TDef>(TDef def)
            where TDef : struct, ILocalDef
        {
            if (!_hasDefsInNode && (_numStoresChecked >= 8))
            {
                return GenTree.VisitResult.Abort;
            }

            _hasDefsInNode = true;
            return _compiler.gtTreeHasLocalRead(_readTree, def.LclNum)
                ? GenTree.VisitResult.Abort
                : GenTree.VisitResult.Continue;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<MayHaveStoreInterferenceVisitor>.WalkTree(ref this, ref use, user);
    }
}
