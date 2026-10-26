// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private struct RecordedVarLocationsVisitor : IBitSetVisitor<RecordedVarLocationsVisitor>
    {
        private readonly LinearScan _linearScan;
        private readonly BasicBlock _block;
        private readonly regNumberSmall[]? _map;
        private int _count;

        public RecordedVarLocationsVisitor(LinearScan linearScan, BasicBlock block, regNumberSmall[]? map)
        {
            _linearScan = linearScan;
            _block = block;
            _map = map;
            _count = 0;
        }

        public readonly int Count => _count;

        public bool Visit(int varIndex)
        {
            assert(_linearScan._compiler.lvaTrackedToVarNum is not null);
            assert(_map is not null);
            var varNum = _linearScan._compiler.lvaTrackedToVarNum[varIndex];
            ref var varDsc = ref _linearScan._compiler.lvaGetDesc(varNum);
            var oldRegNum = varDsc.RegNum;
            var newRegNum = _linearScan.getVarReg(_map, checked((uint)varIndex));

            if (oldRegNum != newRegNum)
            {
                JITDUMP($"  V{varNum:D2}({_linearScan._compiler.compRegVarName(oldRegNum)}->{_linearScan._compiler.compRegVarName(newRegNum)})");
                varDsc.RegNum = newRegNum;
                _count++;

                var prevReportedBlock = _block.Prev;
                if (!_block.IsFirst && _block.Prev.isBBCallFinallyPairTail)
                {
                    // Codegen emits the call-finally pair in its head and does not report its tail.
                    prevReportedBlock = _block.Prev.Prev;
                }

                if (prevReportedBlock is not null &&
                    VarSetOps.IsMember(_linearScan._compiler, prevReportedBlock.bbLiveOut, varIndex))
                {
                    assert(_linearScan._compiler.codeGen is not null);
                    _linearScan._compiler.codeGen.getVariableLiveKeeper().siUpdateVariableLiveRange(in varDsc, varNum);
                }
            }
            else if (newRegNum != REG_STK)
            {
                JITDUMP($"  V{varNum:D2}({_linearScan._compiler.compRegVarName(newRegNum)})");
                _count++;
            }

            return true;
        }
    }

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
        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars,
            VarSetOps.Intersection(_compiler, _registerCandidateVars, block.bbLiveIn));

        var visitor = new RecordedVarLocationsVisitor(this, block, map);
        _ = VarSetOps.VisitBits(_compiler, _currentLiveVars, ref visitor);

        if (visitor.Count == 0)
        {
            JITDUMP("  <none>\n");
        }
        JITDUMP("\n");
    }
}
