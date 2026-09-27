// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class AsyncTransformation
    {
        private enum AsyncSaveSet
        {
            All,
            UnmutatedLocals,
            MutatedLocals,
        }

        private BasicBlock CreateSuspensionBlock(BasicBlock block, int stateNum)
        {
            BasicBlock suspension;
            if (_lastSuspensionBB is null)
            {
                if (_sharedReturnBB is not null)
                {
                    suspension = _compiler.fgNewBBbefore(BBJ_RETURN, _sharedReturnBB, false);
                }
                else
                {
                    _lastSuspensionBB = _compiler.fgLastBBInMainFunction();
                    suspension = _compiler.fgNewBBafter(BBJ_RETURN, _lastSuspensionBB, false);
                }
            }
            else
            {
                suspension = _compiler.fgNewBBafter(BBJ_RETURN, _lastSuspensionBB, false);
            }

            suspension.clearTryIndex();
            suspension.clearHndIndex();
            suspension.inheritWeightPercentage(block, 0);
            _lastSuspensionBB = suspension;
            JITDUMP($"  Creating suspension {FMT_BB(suspension.bbNum)} for state {stateNum}\n");

            return suspension;
        }

        private BasicBlock CreateResumptionBlock(BasicBlock remainder, int stateNum)
        {
            _lastResumptionBB ??= _compiler.fgLastBBInMainFunction();

            var resumption = _compiler.fgNewBBafter(BBJ_ALWAYS, _lastResumptionBB, true);
            var remainderEdge = _compiler.fgAddRefPred(remainder, resumption);
            resumption.bbSetRunRarely();
            resumption.CopyFlags(remainder, BBF_PROF_WEIGHT);
            resumption.SetKindAndTargetEdge(BBJ_ALWAYS, remainderEdge);
            resumption.clearTryIndex();
            resumption.clearHndIndex();
            resumption.SetFlags(BBF_ASYNC_RESUMPTION);
            _lastResumptionBB = resumption;
            JITDUMP($"  Creating resumption {FMT_BB(resumption.bbNum)} for state {stateNum}\n");

            return resumption;
        }

        private AsyncSaveSet GetLocalSaveSet(in LclVarDsc descriptor, VARSET_TP mutatedSinceResumption)
        {
            if (descriptor.lvPromoted)
            {
                for (var i = 0; i < descriptor.lvFieldCnt; i++)
                {
                    ref var field = ref _compiler.lvaGetDesc(descriptor.lvFieldLclStart + i);
                    if (!field.lvTracked || VarSetOps.IsMember(_compiler, mutatedSinceResumption, field._varIndex))
                    {
                        return AsyncSaveSet.MutatedLocals;
                    }
                }

                return AsyncSaveSet.UnmutatedLocals;
            }

            assert(!descriptor.lvIsStructField ||
                (_compiler.lvaGetPromotionType(descriptor.lvParentLcl) is PROMOTION_TYPE_INDEPENDENT));

            if (!descriptor.lvTracked ||
                VarSetOps.IsMember(_compiler, mutatedSinceResumption, descriptor._varIndex))
            {
                return AsyncSaveSet.MutatedLocals;
            }

            return AsyncSaveSet.UnmutatedLocals;
        }
    }
}
