// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void resolveLocalRef(BasicBlock? block, GenTreeLclVar? treeNode, RefPosition currentRefPosition)
    {
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
        if ((block is null) != (treeNode is null) || !_enregisterLocalVars)
        {
            throw new FatalJitException("Local register resolution requires matching block/node ownership and enregistered locals.");
        }
        assert((block is null) == (treeNode is null));
        assert(_enregisterLocalVars);

        var interval = currentRefPosition.getInterval();
        assert(interval.isLocalVar);
        interval.recentRefPosition = currentRefPosition;
        ref var local = ref interval.getLocalVar(_compiler);

        // The extended-lifetime stress pass repairs real last uses separately for codegen.
        if ((treeNode is not null) && !extendLifetimes())
        {
            treeNode.SetLastUse(checked((int)currentRefPosition.getMultiRegIdx()), currentRefPosition.lastUse);

            if ((currentRefPosition.registerAssignment != SRBM_NONE) &&
                (interval.physReg == REG_NA) && currentRefPosition.RegOptional() &&
                currentRefPosition.lastUse && (currentRefPosition.refType is RefType.RefTypeUse))
            {
                var inMap = getInVarToRegMap(_currentBlockNumber)
                    ?? throw new FatalJitException("Contained local resolution requires an incoming variable map.");
                assert(getVarReg(inMap, local._varIndex) == REG_STK);
                currentRefPosition.registerAssignment = SRBM_NONE;
                writeLocalReg(treeNode, interval.varNum, REG_NA);
            }
        }

        if (currentRefPosition.registerAssignment == SRBM_NONE)
        {
            assert(currentRefPosition.RegOptional() && interval.isSpilled);
            local.RegNum = REG_STK;
            if ((interval.assignedReg is not null) &&
                ReferenceEquals(interval.assignedReg.assignedInterval, interval))
            {
                clearAssignedInterval(interval.assignedReg);
            }
            interval.assignedReg = null;
            interval.physReg = REG_NA;
            interval.isActive = false;

            if (currentRefPosition.refType is RefType.RefTypeUse)
            {
                if (treeNode is null)
                {
                    throw new FatalJitException("A contained local use requires its tree node.");
                }
                if (!treeNode.IsMultiReg)
                {
                    treeNode.IsContained = true;
                }
            }
            return;
        }

        // A copy register serves this reference without changing the local's
        // home; a move transfers the home to the newly assigned register.
        var assignedReg = currentRefPosition.assignedReg();
        var homeReg = assignedReg;
        if (!currentRefPosition.copyReg)
        {
            var oldReg = interval.physReg;
            if ((oldReg != REG_NA) && (assignedReg != oldReg))
            {
                var oldRecord = getRegisterRecord(oldReg);
                if (ReferenceEquals(oldRecord.assignedInterval, interval))
                {
                    clearAssignedInterval(oldRecord);
                }
            }
        }

        if ((currentRefPosition.refType is RefType.RefTypeUse) && !currentRefPosition.reload &&
            (interval.physReg == REG_NA))
        {
            var inMap = getInVarToRegMap(_currentBlockNumber)
                ?? throw new FatalJitException("Reload resolution requires an incoming variable map.");
            assert(getVarReg(inMap, local._varIndex) == REG_STK);
            currentRefPosition.reload = true;
        }

        var reload = currentRefPosition.reload;
        var spillAfter = currentRefPosition.spillAfter;
        var writeThru = currentRefPosition.writeThru;
        if (reload)
        {
            assert(currentRefPosition.refType is not RefType.RefTypeDef);
            assert(interval.isSpilled);
            local.RegNum = REG_STK;
            if (!spillAfter)
            {
                interval.physReg = assignedReg;
            }

            if (treeNode is not null)
            {
                treeNode.Flags |= GTF_SPILLED;
                if (treeNode.IsMultiReg)
                {
                    treeNode.SetRegSpillFlagByIdx(GTF_SPILLED, currentRefPosition.multiRegIdx);
                }
                if (spillAfter)
                {
                    if (currentRefPosition.RegOptional())
                    {
                        interval.physReg = REG_NA;
                        writeLocalReg(treeNode, interval.varNum, REG_NA);
                        treeNode.Flags &= ~GTF_SPILLED;
                        treeNode.IsContained = true;
                        assert(!treeNode.IsMultiReg);
                    }
                    else
                    {
                        treeNode.Flags |= GTF_SPILL;
                        if (treeNode.IsMultiReg)
                        {
                            treeNode.SetRegSpillFlagByIdx(GTF_SPILL, currentRefPosition.multiRegIdx);
                        }
                    }
                }
            }
            else
            {
                assert(currentRefPosition.refType is RefType.RefTypeExpUse);
            }
        }
        else if (spillAfter && !RefTypeIsUse(currentRefPosition.refType) &&
            (treeNode is not null) &&
            (!treeNode.IsMultiReg || treeNode.Data.IsMultiRegNode))
        {
            // A pure definition can write directly to its stack home; a multi-reg
            // store with a scalar source still needs its extraction register.
            assert(interval.isSpilled);
            local.RegNum = REG_STK;
            interval.physReg = REG_NA;
            writeLocalReg(treeNode, interval.varNum, REG_NA);
            if (currentRefPosition.singleDefSpill)
            {
                local.lvSpillAtSingleDef = true;
            }
        }
        else
        {
            if (currentRefPosition.copyReg || currentRefPosition.moveReg)
            {
                if (treeNode is null)
                {
                    throw new FatalJitException("A copied or moved local reference requires its tree node.");
                }
                writeLocalReg(treeNode, interval.varNum, interval.physReg);

                if (currentRefPosition.copyReg)
                {
                    homeReg = interval.physReg;
                }
                else
                {
                    assert(interval.isSplit);
                    interval.physReg = assignedReg;
                }

                // Codegen handles fixed-register copies itself. Interference
                // copies and home-register moves require an explicit GT_COPY.
                if (!currentRefPosition.isFixedRegRef || currentRefPosition.moveReg)
                {
                    var owningBlock = block
                        ?? throw new FatalJitException("A copied local reference requires its owning block.");
                    insertCopyOrReload(owningBlock, treeNode, currentRefPosition.getMultiRegIdx(), currentRefPosition);
                }
            }
            else
            {
                interval.physReg = assignedReg;
                if (!interval.isSpilled && !interval.isSplit)
                {
                    if (local.RegNum != REG_STK)
                    {
                        if (local.RegNum != assignedReg)
                        {
                            setIntervalAsSplitMinimal(interval);
                            local.RegNum = REG_STK;
                        }
                    }
                    else
                    {
                        local.RegNum = assignedReg;
                    }
                }
            }

            if (spillAfter)
            {
                if (treeNode is not null)
                {
                    treeNode.Flags |= GTF_SPILL;
                    if (treeNode.IsMultiReg)
                    {
                        treeNode.SetRegSpillFlagByIdx(GTF_SPILL, currentRefPosition.multiRegIdx);
                    }
                }
                assert(interval.isSpilled);
                interval.physReg = REG_NA;
                local.RegNum = REG_STK;
            }

            if (writeThru && (treeNode is not null))
            {
                // EH write-through definitions stay available in a register while
                // also writing the stack copy needed by exception edges.
                treeNode.Flags |= GTF_SPILL;
                if (!currentRefPosition.lastUse)
                {
                    treeNode.Flags |= GTF_SPILLED;
                    if (treeNode.IsMultiReg)
                    {
                        treeNode.SetRegSpillFlagByIdx(GTF_SPILLED, currentRefPosition.multiRegIdx);
                    }
                }
            }

            if (currentRefPosition.singleDefSpill && (treeNode is not null))
            {
                // Write the stack copy at the sole definition while keeping
                // the value live in its register.
                treeNode.Flags |= GTF_SPILL | GTF_SPILLED;
                if (treeNode.IsMultiReg)
                {
                    treeNode.SetRegSpillFlagByIdx(GTF_SPILLED, currentRefPosition.multiRegIdx);
                }
                local.lvSpillAtSingleDef = true;
            }
        }

        var homeRecord = getRegisterRecord(homeReg);
        if (spillAfter || currentRefPosition.lastUse)
        {
            interval.isActive = false;
            interval.assignedReg = null;
            interval.physReg = REG_NA;
            clearAssignedInterval(homeRecord);
        }
        else
        {
            interval.isActive = true;
            interval.assignedReg = homeRecord;
            updateAssignedInterval(homeRecord, interval);
        }
#else
        throw new FatalJitException("Local register resolution is not implemented outside Windows AMD64.");
#endif
    }
}
