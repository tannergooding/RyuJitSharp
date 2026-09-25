// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private VARSET_TP _registerCandidateVars = [];
    private VARSET_TP _currentLiveVars = [];

    public void recordVarLocationsAtStartOfBB(BasicBlock block)
    {
        if (!_enregisterLocalVars)
        {
            return;
        }

        JITDUMP($"Recording Var Locations at start of {FMT_BB(block.bbNum)}\n");
        var map = getInVarToRegMap(unchecked((uint)block.bbNum));
        var count = 0;
        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars,
            VarSetOps.Intersection(_compiler, _registerCandidateVars, block.bbLiveIn));

        _ = VarSetOps.VisitBits(_compiler, _currentLiveVars, varIndex =>
        {
            assert(_compiler.lvaTrackedToVarNum is not null);
            assert(map is not null);
            var varNum = _compiler.lvaTrackedToVarNum[varIndex];
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            var oldRegNum = varDsc.RegNum;
            var newRegNum = getVarReg(map, (uint)varIndex);

            if (oldRegNum != newRegNum)
            {
                JITDUMP($"  V{varNum:D2}({_compiler.compRegVarName(oldRegNum)}->{_compiler.compRegVarName(newRegNum)})");
                varDsc.RegNum = newRegNum;
                count++;

                var prevReportedBlock = block.Prev;
                if (!block.IsFirst && block.Prev.isBBCallFinallyPairTail)
                {
                    // Codegen emits the call-finally pair in its head and does not report its tail.
                    prevReportedBlock = block.Prev.Prev;
                }

                if (prevReportedBlock is not null &&
                    VarSetOps.IsMember(_compiler, prevReportedBlock.bbLiveOut, varIndex))
                {
                    assert(_compiler.codeGen is not null);
                    _compiler.codeGen.getVariableLiveKeeper().siUpdateVariableLiveRange(in varDsc, varNum);
                }
            }
            else if (newRegNum != REG_STK)
            {
                JITDUMP($"  V{varNum:D2}({_compiler.compRegVarName(newRegNum)})");
                count++;
            }

            return true;
        });

        if (count == 0)
        {
            JITDUMP("  <none>\n");
        }
        JITDUMP("\n");
    }
}
