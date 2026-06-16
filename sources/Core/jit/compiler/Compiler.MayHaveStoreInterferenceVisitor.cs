// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct MayHaveStoreInterferenceVisitor : IGenTreeVisitor<MayHaveStoreInterferenceVisitor>
    {
        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;
        private readonly GenTree _readTree;
        private int _numStoresChecked;

        public MayHaveStoreInterferenceVisitor(Compiler compiler, GenTree readTree)
        {
            _compiler = compiler;
            _ancestors = [];
            _readTree = readTree;
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;

            if ((node.Flags & GTF_ASG) is 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            if (node.Oper.IsLocalStore)
            {
                // Check up to 8 stores before we bail with a conservative
                // answer. Avoids quadratic behavior in case we have a large
                // number of stores (e.g. created by physical promotion or by
                // call args morphing).
                if ((_numStoresChecked >= 8) || _compiler.gtTreeHasLocalRead(_readTree, node.AsLclVarCommon().LclNum))
                {
                    return WALK_ABORT;
                }

                _numStoresChecked++;
            }
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<MayHaveStoreInterferenceVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
