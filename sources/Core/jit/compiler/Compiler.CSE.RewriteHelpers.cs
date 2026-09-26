// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optUnmarkCSE(GenTree tree)
    {
        if (!IS_CSE_INDEX(tree._cseNum))
        {
            return true;
        }

        noway_assert(optCSEweight >= 0);

        if (IS_CSE_USE(tree._cseNum))
        {
            var cseNum = GET_CSE_INDEX(tree._cseNum);
            var descriptor = optCSEfindDsc(cseNum);

#if DEBUG
            if (verbose)
            {
                JITDUMP($"Unmark CSE use #{cseNum:D2} at [{tree.TreeId:D6}]: " +
                    $"{descriptor.csdUseCount,3} -> {descriptor.csdUseCount - 1,3}\n");
            }
#endif

            noway_assert(descriptor.csdUseCount > 0);
            if (descriptor.csdUseCount > 0)
            {
                descriptor.csdUseCount -= 1;
                descriptor.csdUseWtCnt = descriptor.csdUseWtCnt < optCSEweight
                    ? 0
                    : descriptor.csdUseWtCnt - optCSEweight;
            }

            tree._cseNum = NO_CSE;
            optCSEunmarks++;

            return true;
        }

        return false;
    }

    private GenTree? optExtractSideEffectsForCSE(GenTree tree)
    {
        var extractor = new CSESideEffectExtractor(this);
        _ = extractor.WalkTree(ref tree, null);

        return extractor.Result;
    }

    private struct CSESideEffectExtractor : IGenTreeVisitor<CSESideEffectExtractor>
    {
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors = [];
        private GenTree? _result;

        public static bool DoPreOrder => true;
        public static bool UseExecutionOrder => true;

        public CSESideEffectExtractor(Compiler compiler)
        {
            _compiler = compiler;
        }

        public readonly GenTree? Result => _result;

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;
            if (_compiler.gtTreeHasSideEffects(node, GTF_PERSISTENT_SIDE_EFFECTS, ignoreCctors: true))
            {
                if (_compiler.gtNodeHasSideEffects(node, GTF_PERSISTENT_SIDE_EFFECTS, ignoreCctors: true))
                {
                    Append(node);
                    return WALK_SKIP_SUBTREES;
                }

                assert((node.Oper is not GT_CALL) || node.AsCall().IsHelperCall());
            }

            if (_compiler.optUnmarkCSE(node))
            {
                assert(!IS_CSE_INDEX(node._cseNum));
                return WALK_CONTINUE;
            }

            assert(IS_CSE_DEF(node._cseNum));
#if DEBUG
            if (_compiler.verbose)
            {
                JITDUMP($"Preserving the CSE def #{GET_CSE_INDEX(node._cseNum):D2} at [{node.TreeId:D6}]\n");
            }
#endif
            Append(node);
            return WALK_SKIP_SUBTREES;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<CSESideEffectExtractor>.WalkTree(ref this, ref use, user, _ancestors);

        private void Append(GenTree node)
        {
            if (_result is null)
            {
                _result = node;
                return;
            }

            var comma = _compiler.gtNewCommaNode(TYP_VOID, _result, node);
            if ((_compiler.vnStore is not null) && _result._vnPair.BothDefined() && node._vnPair.BothDefined())
            {
                var op1Exceptions = _compiler.vnStore.VNPExceptionSet(_result._vnPair);
                comma._vnPair = _compiler.vnStore.VNPWithExc(node._vnPair, op1Exceptions);
            }

            _result = comma;
        }
    }
}
