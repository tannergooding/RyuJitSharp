// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct LocalsWithAddrOpVisitor : IGenTreeVisitor<LocalsWithAddrOpVisitor>, ILocalDefVisitor
    {
        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;

        public LocalsWithAddrOpVisitor(Compiler compiler)
        {
            _compiler = compiler;
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.Oper.IsAnyLocal)
            {
                ref var local = ref _compiler.lvaGetDesc(use.AsLclVarCommon().LclNum);
                return HasAddrOp(local) ? WALK_ABORT : WALK_CONTINUE;
            }

            var visitor = this;
            return use.VisitLogicalLocalDefs(_compiler, ref visitor) is GenTree.VisitResult.Abort
                ? WALK_ABORT : WALK_CONTINUE;
        }

        private static bool HasAddrOp(in LclVarDsc varDsc)
            => varDsc.lvHasLdAddrOp || varDsc.IsAddressExposed;

        public readonly GenTree.VisitResult Visit<TDef>(TDef def)
            where TDef : struct, ILocalDef
        {
            ref var varDsc = ref _compiler.lvaGetDesc(def.LclNum);
            return HasAddrOp(varDsc) ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<LocalsWithAddrOpVisitor>.WalkTree(ref this, ref use, user);
    }
}
