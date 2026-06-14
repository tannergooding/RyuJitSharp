// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private partial struct SideEffectExtractor : IGenTreeVisitor<SideEffectExtractor>
    {
        public static bool DoPreOrder => true;

        public static bool UseExecutionOrder => true;

        private GenTree? _result;
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;
        private readonly GenTreeFlags _flags;

        public SideEffectExtractor(Compiler compiler, GenTreeFlags flags)
        {
            _compiler = compiler;
            _ancestors = [];
            _flags = flags;
        }

        public readonly GenTree? Result => _result;

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;
            var compiler = _compiler;
            var flags = _flags;

            if (!compiler.gtTreeHasSideEffects(node, flags))
            {
                return WALK_SKIP_SUBTREES;
            }

            if (compiler.gtNodeHasSideEffects(node, flags))
            {
                if (node.Oper.IsBlk && !node.Oper.IsStoreBlk)
                {
#if DEBUG
                    JITDUMP($"Replace an unused BLK node [{node.TreeId:D6}] with a NULLCHECK\n");
#endif

                    var unOp = node.AsUnOp();
                    node = compiler.gtNewNullCheck(unOp.Op1);

                    node.SetIndirExceptionFlags(compiler);
                    use = node;
                }

                Append(node);
                return WALK_SKIP_SUBTREES;
            }

            if (node.Oper is GT_QMARK)
            {
                // Visit children out of order so we know if we can completely remove the qmark.
                // We cannot modify the condition if we cannot completely remove the qmark, so we cannot visit it first.

                var prevSideEffects = _result;

                var qmark = node.AsQmark();
                var colon = qmark.Op2.AsColon();

                _result = null;
                _ = WalkTree(ref colon.Op1Ref, colon);
                var thenSideEffects = _result;

                _result = null;
                _ = WalkTree(ref colon.Op2Ref, colon);
                var elseSideEffects = _result;

                _result = prevSideEffects;

                if ((thenSideEffects is null) && (elseSideEffects is null))
                {
                    _ = WalkTree(ref qmark.Op1Ref, qmark);
                }
                else
                {
                    colon.Op1 = (thenSideEffects is not null) ? thenSideEffects : compiler.gtNewNothingNode();
                    colon.Op2 = (elseSideEffects is not null) ? elseSideEffects : compiler.gtNewNothingNode();

                    qmark.Type = TYP_VOID;
                    colon.Type = TYP_VOID;

                    Append(qmark);
                }

                return WALK_SKIP_SUBTREES;
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<SideEffectExtractor>.WalkTree(ref this, ref use, user, _ancestors);

        public void Append(GenTree node)
        {
            var result = _result;

            if (result is not null)
            {
                var compiler = _compiler;

                var comma = compiler.gtNewCommaNode(TYP_VOID, result, node);
                comma.SetMorphed(compiler);

                // Both should have value numbers defined for both or for neither
                // one (unless we are remorphing, in which case a prior transform
                // involving either node may have discarded or otherwise
                // invalidated the value numbers).
                assert((result._vnPair.BothDefined() == node._vnPair.BothDefined()) || !compiler.fgGlobalMorph);

                // TODO: Port SideEffectExtractor.Append once vnStore is ported
                // // Set the ValueNumber 'gtVNPair' for the new GT_COMMA node
                // if ((compiler.vnStore is not null) && result._vnPair.BothDefined() && node._vnPair.BothDefined())
                // {
                //     ValueNumPair op1Exceptions = compiler.vnStore.VNPExceptionSet(result._vnPair);
                //     comma->gtVNPair = compiler.vnStore.VNPWithExc(node._vnPair, op1Exceptions);
                // }

                node = comma;
            }
            _result = node;
        }
    }
}
