// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct LocalsWithAddrOpVisitor : IGenTreeVisitor<LocalsWithAddrOpVisitor>
    {
        public static bool DoLclVarsOnly => true;

        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;
        private readonly Stack<GenTree> _ancestors;

        public LocalsWithAddrOpVisitor(Compiler compiler)
        {
            _compiler = compiler;
            _ancestors = [];
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(use.AsLclVarCommon().LclNum);

            if (varDsc.lvHasLdAddrOp || varDsc.IsAddressExposed)
            {
                return WALK_ABORT;
            }
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<LocalsWithAddrOpVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
