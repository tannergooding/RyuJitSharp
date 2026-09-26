// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void finalizeLocalRegisterAssignments()
    {
        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            if (!local.lvLRACandidate)
            {
                local.RegNum = REG_STK;
                continue;
            }

            var interval = getIntervalForLocalVar(local._varIndex);
            var firstReference = interval.firstRefPosition;
            if (local.lvIsParam || local.lvIsParamRegTarget)
            {
                var parameterReference = firstReference
                    ?? throw new FatalJitException("A register parameter requires an initial reference.");
                var initialMask = parameterReference.registerAssignment;
                var initialReg = (initialMask == SRBM_NONE || parameterReference.spillAfter)
                    ? REG_STK : genRegNumFromMask(initialMask, interval.registerType);
                local.ArgInitReg = initialReg;
                JITDUMP($"  Set V{localNumber:D2} argument initial register to {initialReg.Name}\n");
                assert(local.lvIsRegArg || !_compiler.lvaIsFieldOfDependentlyPromotedStruct(in local));
            }

            if (local.RegNum == REG_STK || interval.isSpilled || interval.isSplit)
            {
                local.lvRegister = false;
                var firstActualReference = firstReference is null
                    ? null : skipExposedLocalUses(firstReference);
                if (firstActualReference is null)
                {
                    local.lvLRACandidate = false;
                    local.lvOnFrame = local.lvRefCnt() != 0;
                }
                else
                {
                    if (!interval.isSpilled)
                    {
                        local.lvOnFrame = false;
                    }
                    if (firstActualReference.registerAssignment == SRBM_NONE || firstActualReference.spillAfter)
                    {
                        assert(firstActualReference.spillAfter || firstActualReference.RegOptional() ||
                            firstActualReference.refType is not RefType.RefTypeDef and not RefType.RefTypeUse);
                        local.RegNum = REG_STK;
                    }
                    else
                    {
                        local.RegNum = firstActualReference.assignedReg();
                    }
                }
            }
            else
            {
                assert(firstReference is not null);
                local.lvRegister = true;
                local.lvOnFrame = false;
#if DEBUG
                var assignment = genSingleTypeRegMask(local.RegNum);
                assert(!interval.isSpilled && !interval.isSplit);
                for (var reference = interval.firstRefPosition; reference is not null; reference = reference.nextRefPosition)
                {
                    if (reference.registerAssignment != SRBM_NONE && !reference.copyReg && !reference.moveReg &&
                        reference.refType is not RefType.RefTypeExpUse)
                    {
                        assert(reference.registerAssignment == assignment);
                    }
                }
#endif
            }
        }
    }

    private static RefPosition? skipExposedLocalUses(RefPosition reference)
    {
        var current = reference;
        while (current.refType is RefType.RefTypeExpUse)
        {
            var next = current.nextRefPosition;
            if (next is null)
            {
                return null;
            }
            current = next;
        }
        return current;
    }

#if DEBUG
    private void dumpLocalResolutionBoundaries()
    {
        jitprintf("-----------------------\nRESOLVING BB BOUNDARIES\n-----------------------\n");
        jitprintf("Resolution Candidates: ");
        dumpConvertedVarSet(_compiler, _resolutionCandidateVars);
        jitprintf($"\nHas {(_hasCriticalEdges ? "" : "No ")}Critical Edges\n\n");
        jitprintf("Prior to Resolution\n");
        foreach (var block in _compiler.Blocks)
        {
            jitprintf($"\n{FMT_BB(block.bbNum)}");
            if (block.hasEHBoundaryIn)
            {
                JITDUMP("  EH flow in");
            }
            if (block.hasEHBoundaryOut)
            {
                JITDUMP("  EH flow out");
            }
            jitprintf("\nuse: ");
            dumpConvertedVarSet(_compiler, block.bbVarUse);
            jitprintf("\ndef: ");
            dumpConvertedVarSet(_compiler, block.bbVarDef);
            jitprintf("\n in: ");
            dumpConvertedVarSet(_compiler, block.bbLiveIn);
            jitprintf("\nout: ");
            dumpConvertedVarSet(_compiler, block.bbLiveOut);
            jitprintf("\n");
            dumpInVarToRegMap(block);
            dumpOutVarToRegMap(block);
        }
        jitprintf("\n\n");
    }
#endif
}
