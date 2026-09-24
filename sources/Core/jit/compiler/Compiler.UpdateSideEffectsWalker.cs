// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    private struct UpdateSideEffectsWalker : IGenTreeVisitor<UpdateSideEffectsWalker>
    {
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;

        internal UpdateSideEffectsWalker(Compiler compiler)
        {
            _compiler = compiler;
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public static bool DoPostOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            use.Flags &= ~(GTF_ASG | GTF_CALL | GTF_EXCEPT);

            if (((use.Flags & GTF_ORDER_SIDEEFF) != 0) && !use.SupportsOrderingSideEffect())
            {
                use.Flags &= ~GTF_ORDER_SIDEEFF;
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.MayThrow(_compiler))
            {
                use.Flags |= GTF_EXCEPT;
            }

            if (use.RequiresAsgFlag)
            {
                use.Flags |= GTF_ASG;
            }

            if (use.RequiresCallFlag(_compiler))
            {
                use.Flags |= GTF_CALL;
            }

            // Children have already propagated their effects. Unlike the
            // single-node helper, this includes their exceptions in the check.
            if (use.Oper.IsIndirOrArrMetaData && ((use.Flags & GTF_EXCEPT) == 0))
            {
                use.Flags |= GTF_IND_NONFAULTING;
            }

            user?.Flags |= use.Flags & GTF_ALL_EFFECT;

            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<UpdateSideEffectsWalker>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
