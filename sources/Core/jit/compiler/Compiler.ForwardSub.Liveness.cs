// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, forwardsub.cpp.

using static RyuJitSharp.GenTreeFlags;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool fgForwardSubHasStoreInterference(Statement defStmt, Statement nextStmt,
        GenTree nextStmtUse)
    {
        assert(defStmt.RootNode.Oper.IsLocalStore);
        assert(nextStmtUse.Oper.IsLocalRead);
        var defNode = defStmt.RootNode.AsLclVarCommon();

        var interferes = false;
        _ = defStmt.VisitLogicalLocalOccurrencesViaLocalsTreeList(defOccurrence => {
            if (defOccurrence.Node == defNode)
            {
                return GenTree.VisitResult.Abort;
            }

            var number = defOccurrence.LclNum;
            ref var descriptor = ref lvaGetDesc(number);
            var parentNumber = descriptor.lvIsStructField ? descriptor.lvParentLcl : BAD_VAR_NUM;

            _ = nextStmt.VisitLogicalLocalOccurrencesViaLocalsTreeList(useOccurrence => {
                if (useOccurrence.Node == nextStmtUse)
                {
                    return GenTree.VisitResult.Abort;
                }
                if (useOccurrence.Node.Oper.IsStore &&
                    ((useOccurrence.LclNum == number) || (useOccurrence.LclNum == parentNumber)))
                {
                    interferes = true;
                    return GenTree.VisitResult.Abort;
                }
                return GenTree.VisitResult.Continue;
            });
            return interferes ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue;
        });

        return interferes;
    }

    private void fgForwardSubUpdateLiveness(GenTree first, GenTree last)
    {
        for (var node = first.Prev; node is not null; node = node.Prev)
        {
            if ((node.Flags & GTF_VAR_DEATH_MASK) == 0)
            {
                continue;
            }

            var number = node.AsLclVarCommon().LclNum;
            ref var descriptor = ref lvaGetDesc(number);
            var parentNumber = descriptor.lvIsStructField ? descriptor.lvParentLcl : BAD_VAR_NUM;
            for (var candidate = first; ; candidate = candidate.Next
                ?? throw new FatalJitException("The inserted local segment is not connected."))
            {
                var newNumber = candidate.AsLclVarCommon().LclNum;
                if (descriptor.lvPromoted)
                {
                    if (newNumber == number)
                    {
                        node.Flags &= ~GTF_VAR_DEATH_MASK;
                        break;
                    }
                    if ((newNumber >= descriptor.lvFieldLclStart) &&
                        (newNumber < descriptor.lvFieldLclStart + descriptor.lvFieldCnt))
                    {
                        var fieldIndex = newNumber - descriptor.lvFieldLclStart;
                        node.Flags &= ~(GenTreeFlags)((int)GTF_VAR_FIELD_DEATH0 << fieldIndex);
                        if ((node.Flags & GTF_VAR_DEATH_MASK) == 0)
                        {
                            break;
                        }
                    }
                }
                else if ((newNumber == number) || (newNumber == parentNumber))
                {
                    node.Flags &= ~GTF_VAR_DEATH;
                    break;
                }

                if (candidate == last)
                {
                    break;
                }
            }
        }
    }

    private void fgForwardSubSpliceLocals(Statement nextStmt, GenTreeLclVarCommon use,
        GenTree? first, GenTree? last)
    {
        var before = use.Prev;
        var after = use.Next;
        if (first is null)
        {
            if (before is null)
            {
                nextStmt.TreeListBegin = after;
            }
            else
            {
                before.Next = after;
            }
            if (after is null)
            {
                nextStmt.TreeListEnd = before;
            }
            else
            {
                after.Prev = before;
            }
        }
        else
        {
            assert(last is not null);
            first.Prev = before;
            last.Next = after;
            if (before is null)
            {
                nextStmt.TreeListBegin = first;
            }
            else
            {
                before.Next = first;
            }
            if (after is null)
            {
                nextStmt.TreeListEnd = last;
            }
            else
            {
                after.Prev = last;
            }

            fgForwardSubUpdateLiveness(first, last);
        }

        use.Prev = null;
        use.Next = null;
    }
}
