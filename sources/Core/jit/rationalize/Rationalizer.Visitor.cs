// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Rationalizer
{
    private struct Visitor : IGenTreeVisitor<Visitor>
    {
        public static bool ComputeStack => true;

        public static bool DoPreOrder => true;

        public static bool DoPostOrder => true;

        public static bool UseExecutionOrder => true;

        private readonly Rationalizer _rationalizer;
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;

        public Visitor(Rationalizer rationalizer, Compiler compiler)
        {
            _rationalizer = rationalizer;
            _compiler = compiler;
            _ancestors = [];
        }

        public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            // Rewrite intrinsics that are not supported by the target back into user calls.
            // This needs to be done before the transition to LIR because it relies on the use
            // of fgMorphArgs, which is designed to operate on HIR. Once this is done for a
            // particular statement, link that statement's nodes into the current basic block.

            var node = use;
            var rationalizer = _rationalizer;

            if (node.Oper is GT_INTRINSIC)
            {
                if (_compiler.IsIntrinsicImplementedByUserCall(node.AsIntrinsic().IntrinsicName))
                {
                    rationalizer.RewriteIntrinsicAsUserCall(ref use, _ancestors);
                }
            }
#if FEATURE_HW_INTRINSICS
            else if (node.Oper.IsHWIntrinsic)
            {
                if (node.AsHWIntrinsic().IsUserCall)
                {
                    rationalizer.RewriteHWIntrinsicAsUserCall(ref use, _ancestors);
                }
            }
#endif

#if TARGET_ARM64
            if (node.Oper is GT_SUB)
            {
                rationalizer.RewriteSubLshDiv(ref use);
            }
#endif

            return Compiler.WALK_CONTINUE;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => _rationalizer.RewriteNode(ref use, _ancestors);

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<Visitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
