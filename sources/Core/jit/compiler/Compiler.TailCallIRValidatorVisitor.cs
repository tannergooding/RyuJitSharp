// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private struct TailCallIRValidatorVisitor(Compiler compiler, GenTreeCall tailCall) : IGenTreeVisitor<TailCallIRValidatorVisitor>
    {
        private readonly Compiler _compiler = compiler;
        private readonly GenTreeCall _tailCall = tailCall;
        private readonly GenTreeStack _ancestors = [];
        private int _localNumber = BAD_VAR_NUM;
        private bool _active;

        public static bool DoPostOrder => true;

        public static bool UseExecutionOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            if (!_active)
            {
                if (tree == _tailCall)
                {
                    _active = true;
                }

                return WALK_CONTINUE;
            }

            if (tree.Oper is GT_RETURN)
            {
                assert((tree.Type is TYP_VOID) || ValidateUse(tree.AsUnOp().Op1),
                    "Expected return to be result of tailcall");
                return WALK_ABORT;
            }

            if (tree.Oper is GT_NOP)
            {
                // Self-stores may already have been morphed into NOPs.
            }
            else if (tree.Oper is GT_STORE_LCL_VAR)
            {
                // Result forwarding may span multiple statements or blocks.
                assert(ValidateUse(tree.AsLclVar().Data), "Expected value of store to be result of tailcall");
                _localNumber = tree.AsLclVar().LclNum;
            }
            else if (tree.Oper is GT_LCL_VAR)
            {
                assert(ValidateUse(tree), "Expected use of local to be tailcall value");
            }
            else if (tree.Oper is GT_CAST)
            {
                // Inlining can add small-type normalization already guaranteed
                // by the callee, which a tail call can safely bypass.
                assert(!_compiler.fgCastNeeded(_tailCall, tree.AsCast().CastType) &&
                    ValidateUse(tree.AsCast().CastOp), "Expected normalizing cast of tailcall result");
            }
            else if (IsCommaNop(tree))
            {
                // COMMA(NOP, NOP) also carries no work past the tail call.
            }
            else
            {
                DISPTREE(tree);
                assert(false, "Unexpected tree op after call marked as tailcall");
            }

            return WALK_CONTINUE;
        }

        private static bool IsCommaNop(GenTree node)
        {
            if (node.Oper is not GT_COMMA)
            {
                return false;
            }

            return (node.AsOp().Op1.Oper is GT_NOP) && (node.AsOp().Op2.Oper is GT_NOP);
        }

        private readonly bool ValidateUse(GenTree node)
        {
            if (node.Oper is GT_CAST)
            {
                node = node.AsCast().CastOp;
            }

            if (_localNumber != BAD_VAR_NUM)
            {
                return (node.Oper is GT_LCL_VAR) && (node.AsLclVar().LclNum == _localNumber);
            }

            if (node == _tailCall)
            {
                return true;
            }

            var retBufferArgument = _tailCall.Args.RetBufferArg;
            if (retBufferArgument is not null)
            {
                var retBuffer = retBufferArgument.Node;
                return (retBuffer.Oper is GT_LCL_VAR) &&
                    (retBuffer.AsLclVar().LclNum == _compiler.info.compRetBuffArg) &&
                    (node.Oper is GT_LCL_VAR) && (node.AsLclVar().LclNum == _compiler.info.compRetBuffArg);
            }

            return false;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<TailCallIRValidatorVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
#endif
}
