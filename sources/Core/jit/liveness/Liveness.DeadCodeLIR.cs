// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    internal bool TryRemoveDeadStoreLIR(GenTree store, GenTreeLclVarCommon local, BasicBlock block)
    {
        // These structs are reported to GC as untracked; removing their explicit
        // initialization could expose uninitialized references.
        if ((local.Flags & GTF_VAR_USEASG) == 0)
        {
            ref var descriptor = ref _compiler.lvaGetDesc(local.LclNum);
            if (descriptor.lvHasExplicitInit && (descriptor.Type is TYP_STRUCT) &&
                descriptor.HasGCPtr && (descriptor.lvRefCnt() > 1))
            {
#if DEBUG
                if (_compiler.verbose)
                {
                    jitprintf($"Not removing a potential explicit init [{store.TreeId:D6}] of V{local.LclNum:D2}\n");
                }
#endif
                return false;
            }
        }

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"Removing dead {(store.Oper.IsIndir ? "indirect store" : "local store")}:\n");
            _compiler.gtDispTree(store, topOnly: true);
        }
#endif
        block.Remove(store);
        _compiler.fgStmtRemoved = true;

        return true;
    }

    private bool TryRemoveNonLocalLIR(GenTree node, LIR.Range range)
    {
        if (!TLiveness.EliminateDeadCode)
        {
            return false;
        }

        assert(!node.Oper.IsLocal);
        if (!node.IsValue || node.IsUnusedValue)
        {
            assert(!node.RequiresAsgFlag && (node.Oper is not GT_CALL));
            if (((node.Flags & GTF_SET_FLAGS) == 0) && !node.MayThrow(_compiler) &&
                CanUncontainOrRemoveOperands(node))
            {
#if DEBUG
                if (_compiler.verbose)
                {
                    jitprintf("Removing dead node:\n");
                    _compiler.gtDispTree(node, topOnly: true);
                }
#endif
                _ = node.VisitOperands(operand => {
                    operand.IsUnusedValue = true;
                    return GenTree.VisitResult.Continue;
                });

                if (node.Oper.ConsumesFlags)
                {
                    var previous = node.Prev;
                    assert(previous is not null);
                    if ((previous.Flags & GTF_SET_FLAGS) != 0)
                    {
                        previous.Flags &= ~GTF_SET_FLAGS;
                    }
                }
                range.Remove(node);

                return true;
            }
        }

        return false;
    }

    private bool CanUncontainOrRemoveOperands(GenTree node)
    {
#if FEATURE_HW_INTRINSICS
        return node.VisitOperands(operand =>
            operand.IsContained && (operand.Oper is GT_HWINTRINSIC) &&
            ((operand.Flags & GTF_HW_EM_OP) != 0) && operand.NodeOrContainedOperandsMayThrow(_compiler)
                ? GenTree.VisitResult.Abort
                : GenTree.VisitResult.Continue) is not GenTree.VisitResult.Abort;
#else
        return true;
#endif
    }
}
