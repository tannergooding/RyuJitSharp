// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if DEBUG
    private void checkLastUses(BasicBlock block)
    {
        if (VERBOSE)
        {
            JITDUMP($"\n\nCHECKING LAST USES for {FMT_BB(block.bbNum)}, liveout=");
            dumpConvertedVarSet(_compiler, block.bbLiveOut);
            JITDUMP("\n==============================\n");
        }

        var keepAliveLocal = _compiler.lvaKeepAliveAndReportThis()
            ? _compiler.info.compThisArg
            : BAD_VAR_NUM;
        if (keepAliveLocal != BAD_VAR_NUM)
        {
            assert(!_compiler.info.compIsStatic);
        }
        var computedLive = VarSetOps.MakeCopy(_compiler, block.bbLiveOut);
        var foundDifference = false;
        for (var index = refPositions.Count - 1; refPositions[index].refType is not RefType.RefTypeBB; index--)
        {
            var position = refPositions[index];
            assert(position.refType is not RefType.RefTypeParamDef and not RefType.RefTypeZeroInit);
            if (!position.isIntervalRef() || !position.getInterval().isLocalVar)
            {
                continue;
            }

            var interval = position.getInterval();
            var variableIndex = checked((int)interval.getVarIndex(_compiler));
            var localNumber = interval.varNum;
            var tree = position.treeNode;
            assert(tree is not null || position.refType is RefType.RefTypeExpUse or RefType.RefTypeDummyDef);
            if (!VarSetOps.IsMember(_compiler, computedLive, variableIndex) && (localNumber != keepAliveLocal))
            {
                // Extended LSRA lifetimes clear RefPosition.lastUse, but codegen still
                // requires the node's actual local death to be recorded.
                if (extendLifetimes())
                {
                    tree?.AsLclVar().SetLastUse(position.multiRegIdx, true);
                }
                else if (!position.lastUse)
                {
                    JITDUMP($"missing expected last use of V{localNumber:D2} @{position.nodeLocation}\n");
                    foundDifference = true;
                }

                VarSetOps.AddElemD(_compiler, computedLive, variableIndex);
            }
            else if (position.lastUse)
            {
                JITDUMP($"unexpected last use of V{localNumber:D2} @{position.nodeLocation}\n");
                foundDifference = true;
            }
            else if (extendLifetimes() && (tree is not null))
            {
                tree.AsLclVar().SetLastUse(position.multiRegIdx, false);
            }

            if (position.refType is RefType.RefTypeDef or RefType.RefTypeDummyDef)
            {
                VarSetOps.RemoveElemD(_compiler, computedLive, variableIndex);
            }
        }

        var missing = VarSetOps.Diff(_compiler, block.bbLiveIn, computedLive);
        if (block.HasPotentialEHSuccs(_compiler))
        {
            var handlerLive = VarSetOps.MakeEmpty(_compiler);
            var memoryLiveness = default(MemoryKindSet);
            _compiler.fgAddHandlerLiveVars(block, handlerLive, ref memoryLiveness);
            VarSetOps.DiffD(_compiler, missing, handlerLive);
        }

        var trackedToLocal = _compiler.lvaTrackedToVarNum
            ?? throw new FatalJitException("Last-use verification requires tracked local mapping.");
        _ = VarSetOps.VisitBits(_compiler, missing, variableIndex => {
            if (getTrackedLocal(variableIndex).lvLRACandidate)
            {
                JITDUMP($"{FMT_BB(block.bbNum)}: V{trackedToLocal[variableIndex]:D2} is in LiveIn set, but not computed live.\n");
                foundDifference = true;
            }
            return true;
        });

        VarSetOps.DiffD(_compiler, computedLive, block.bbLiveIn);
        _ = VarSetOps.VisitBits(_compiler, computedLive, variableIndex => {
            if (getTrackedLocal(variableIndex).lvLRACandidate)
            {
                JITDUMP($"{FMT_BB(block.bbNum)}: V{trackedToLocal[variableIndex]:D2} is computed live, but not in LiveIn set.\n");
                foundDifference = true;
            }
            return true;
        });
        assert(!foundDifference);
    }

    private void stressSetRandomParameterPreferences()
    {
        var random = new CLRRandom(_compiler.info.compMethodHash());
        var codeGen = _compiler.codeGen
            ?? throw new FatalJitException("Parameter preference stress requires initialized codegen state.");
        var integerRegisters = codeGen.CalleeRegArgMaskLiveIn & new regMaskTP(_rbmAllInt);
        var floatingRegisters = codeGen.CalleeRegArgMaskLiveIn & new regMaskTP(_rbmAllFloat);

        for (var variableIndex = 0; variableIndex < _compiler.lvaTrackedCount; variableIndex++)
        {
            ref var local = ref getTrackedLocal(variableIndex);
            if (!local.lvIsParam || !local.lvLRACandidate)
            {
                continue;
            }

            var interval = getIntervalForLocalVar(checked((uint)variableIndex));
            var registers = interval.registerType == TYP_FLOAT
                ? floatingRegisters
                : integerRegisters;
            var count = 0;
            for (var registerIndex = (int)REG_FIRST; registerIndex < (int)REG_COUNT; registerIndex++)
            {
                if (registers.IsSet((regNumber)registerIndex))
                {
                    count++;
                }
            }
            if (count == 0)
            {
                continue;
            }

            var bitIndex = random.Next(count);
            var preferredRegister = REG_NA;
            for (var registerIndex = (int)REG_FIRST; registerIndex < (int)REG_COUNT; registerIndex++)
            {
                var register = (regNumber)registerIndex;
                if (registers.IsSet(register) && (bitIndex-- == 0))
                {
                    preferredRegister = register;
                    break;
                }
            }
            assert(preferredRegister is not REG_NA);
            var chosenMask = regMaskTP.CreateFromRegNum(preferredRegister, genSingleTypeRegMask(preferredRegister));
            if (interval.registerType == TYP_FLOAT)
            {
                floatingRegisters &= ~chosenMask;
            }
            else
            {
                integerRegisters &= ~chosenMask;
            }
            interval.mergeRegisterPreferences(genSingleTypeRegMask(preferredRegister));
        }
    }

    private void validateIntervals()
    {
        if (!_enregisterLocalVars)
        {
            return;
        }

        JITDUMP("\n------------\n");
        JITDUMP("REFPOSITIONS DURING VALIDATE INTERVALS (RefPositions per interval)\n");
        JITDUMP("------------\n\n");
        for (var variableIndex = 0; variableIndex < _compiler.lvaTrackedCount; variableIndex++)
        {
            if (!getTrackedLocal(variableIndex).lvLRACandidate)
            {
                continue;
            }

            var interval = getIntervalForLocalVar(checked((uint)variableIndex));
            var defined = false;
            uint lastUseBlock = 0;
            JITDUMP("-----------------\n");
            for (var position = interval.firstRefPosition;
                position is not null; position = position.nextRefPosition)
            {
                if (VERBOSE)
                {
                    position.dump(this);
                }

                var referenceType = position.refType;
                if (!defined && RefTypeIsUse(referenceType) && (lastUseBlock == position.bbNum) &&
                    !position.lastUse)
                {
                    if (!string.IsNullOrEmpty(_compiler.info.compMethodName))
                    {
                        JITDUMP($"{_compiler.info.compMethodName}: ");
                    }
                    JITDUMP($"LocalVar V{interval.varNum:D2}: undefined use at {position.nodeLocation}\n");
                    assert(false);
                }
                if (interval.isSingleDef && RefTypeIsDef(referenceType))
                {
                    assert(position == interval.firstRefPosition);
                }
                if (position.lastUse)
                {
                    defined = false;
                    lastUseBlock = position.bbNum;
                }
                if (RefTypeIsDef(referenceType))
                {
                    defined = true;
                }
            }
        }
    }
#endif
}
