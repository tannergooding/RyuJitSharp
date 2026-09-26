// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void verifyResolutionMoveWithLocals(GenTree destination, LsraLocation location)
    {
        assert(isResolutionMove(destination));
        if (destination.Oper is GT_SWAP)
        {
            var first = destination.AsOp().Op1.AsLclVarCommon();
            var second = destination.AsOp().Op2.AsLclVarCommon();
            var firstReg = first.RegNum;
            var secondReg = second.RegNum;
            var firstLocal = _compiler.lvaGetDesc(checked((int)first.LclNum));
            var secondLocal = _compiler.lvaGetDesc(checked((int)second.LclNum));
            var firstInterval = getIntervalForLocalVar(firstLocal._varIndex);
            var secondInterval = getIntervalForLocalVar(secondLocal._varIndex);
            assert(firstInterval.physReg == firstReg && secondInterval.physReg == secondReg);
            firstInterval.physReg = secondReg;
            secondInterval.physReg = firstReg;
            firstInterval.assignedReg = getRegisterRecord(secondReg);
            secondInterval.assignedReg = getRegisterRecord(firstReg);
            firstInterval.assignedReg.assignedInterval = firstInterval;
            secondInterval.assignedReg.assignedInterval = secondInterval;
            if (VERBOSE)
            {
                dumpVerificationResolutionMove(firstInterval, secondReg, location, "Swap");
                dumpVerificationResolutionMove(secondInterval, firstReg, location, "\"");
            }
            return;
        }

        var destinationReg = destination.RegNum;
        regNumber sourceReg;
        GenTreeLclVarCommon localNode;
        if (destination.Oper is GT_COPY)
        {
            localNode = destination.AsCopyOrReload().Op1.AsLclVarCommon();
            sourceReg = localNode.RegNum;
        }
        else
        {
            localNode = destination.AsLclVarCommon();
            if ((localNode.Flags & GTF_SPILLED) != 0)
            {
                sourceReg = REG_STK;
            }
            else
            {
                assert((localNode.Flags & GTF_SPILL) != 0);
                sourceReg = destinationReg;
                destinationReg = REG_STK;
            }
        }

        var interval = getIntervalForLocalVarNode(localNode);
        assert(interval.physReg == sourceReg ||
            (sourceReg == REG_STK && interval.physReg == REG_NA));
        if (sourceReg != REG_STK)
        {
            getRegisterRecord(sourceReg).assignedInterval = null;
        }
        if (destinationReg != REG_STK)
        {
            interval.physReg = destinationReg;
            interval.assignedReg = getRegisterRecord(destinationReg);
            interval.assignedReg.assignedInterval = interval;
            interval.isActive = true;
        }
        else
        {
            interval.physReg = REG_NA;
            interval.assignedReg = null;
            interval.isActive = false;
        }

        if (VERBOSE)
        {
            Compiler.printTreeId(destination);
            jitprintf(" ");
            dumpVerificationResolutionMove(interval, destinationReg, location, "Move");
        }
    }

    private void dumpVerificationResolutionMove(Interval interval, regNumber register,
        LsraLocation location, string action)
    {
        jitprintf(new string(' ', 9));
        dumpAllocationLocation(location, 0);
        jitprintf(getAllocationIntervalName(interval));
        jitprintf($"  {action,-4}          {register.Name,-4} ");
        dumpAllocationRegisterRecords();
    }
}
#endif
