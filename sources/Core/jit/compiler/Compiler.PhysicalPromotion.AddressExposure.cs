// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, gentree.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    private bool PhysicalPromotionHasAddressExposedLocals(GenTree tree)
    {
        var visitor = new PhysicalPromotionAddressExposedVisitor(this);
        return visitor.WalkTree(ref tree, null) is WALK_ABORT;
    }

    private struct PhysicalPromotionAddressExposedVisitor(Compiler compiler)
        : IGenTreeVisitor<PhysicalPromotionAddressExposedVisitor>, ILocalDefVisitor
    {
        public static bool DoPreOrder => true;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<PhysicalPromotionAddressExposedVisitor>.WalkTree(ref this, ref use, user);

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.Oper.IsAnyLocal)
            {
                return compiler.lvaGetDesc(use.AsLclVarCommon().LclNum).IsAddressExposed
                    ? WALK_ABORT : WALK_CONTINUE;
            }

            var visitor = this;
            return use.VisitLogicalLocalDefs(compiler, ref visitor) is GenTree.VisitResult.Abort
                ? WALK_ABORT : WALK_CONTINUE;
        }

        public readonly GenTree.VisitResult Visit<TDef>(TDef def)
            where TDef : struct, ILocalDef
            => compiler.lvaGetDesc(def.LclNum).IsAddressExposed
                ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue;

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;
    }
}
