// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void buildInitialParamDef(in LclVarDsc local, regNumber parameterRegister)
    {
        assert(local.lvLRACandidate);

        var interval = getIntervalForLocalVar(local._varIndex);
        var registerType = local.GetRegisterType();
        var mask = allRegs(registerType);
        if ((parameterRegister != REG_NA) && !stressInitialParamReg())
        {
            assert(parameterRegister < REG_COUNT);
            mask = genSingleTypeRegMask(parameterRegister);
            assignPhysReg(getRegisterRecord(parameterRegister), interval);
#if DEBUG
            _allocationDumpRegisters |= regMaskTP.CreateFromRegNum(parameterRegister, mask);
#endif
        }

        var position = newRefPosition(interval, MinLocation, RefType.RefTypeParamDef, null, mask);
        position.setRegOptional(true);
    }

    private bool stressInitialParamReg()
    {
#if DEBUG
        return _compiler.compStressCompile(Compiler.compStressArea.STRESS_INITIAL_PARAM_REG, 25);
#else
        return false;
#endif
    }

    // currentLiveVariables must contain exactly the register candidates live on entry.
    // Non-GC locals without initlocals can be undefined on an incoming path; retain
    // their stack home rather than inventing an initial register value.
    private void insertZeroInitRefPositions()
    {
        assert(_enregisterLocalVars);
#if DEBUG
        assert(_compiler.fgFirstBB is not null);
        var expectedLiveVariables = VarSetOps.Intersection(
            _compiler, _registerCandidateVars, _compiler.fgFirstBB.bbLiveIn);
        assert(VarSetOps.Equal(_compiler, _currentLiveVariables, expectedLiveVariables));
#endif

        _ = VarSetOps.VisitBits(_compiler, _currentLiveVariables, variableIndex =>
        {
            assert(_compiler.lvaTrackedToVarNum is not null);
            var localNumber = _compiler.lvaTrackedToVarNum[variableIndex];
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            if (!local.lvIsParam && !local.lvIsParamRegTarget && local.lvLRACandidate)
            {
                JITDUMP($"V{localNumber:D2} was live in to first block:");
                var interval = getIntervalForLocalVar(checked((uint)variableIndex));
                if (_compiler.info.compInitMem || varTypeIsGC(local.Type))
                {
                    local.lvMustInit = true;
                    if (_compiler.lvaIsOSRLocal(localNumber))
                    {
                        JITDUMP(" will be initialized by OSR\n");
                        local.lvMustInit = false;
                    }

                    JITDUMP(" creating ZeroInit\n");
                    var position = newRefPosition(interval, MinLocation, RefType.RefTypeZeroInit,
                        null, allRegs(interval.registerType));
                    position.setRegOptional(true);
                }
                else
                {
                    setIntervalAsSpilled(interval);
                    JITDUMP(" marking as spilled\n");
                }
            }

            return true;
        });

        if (_compiler.lvaEnregEHVars)
        {
            _ = VarSetOps.VisitBits(_compiler, _finallyVars, variableIndex =>
            {
                assert(_compiler.lvaTrackedToVarNum is not null);
                var localNumber = _compiler.lvaTrackedToVarNum[variableIndex];
                ref var local = ref _compiler.lvaGetDesc(localNumber);
                if (!local.lvIsParam && !local.lvIsParamRegTarget && local.lvLRACandidate)
                {
                    JITDUMP($"V{localNumber:D2} is a finally var:");
                    var interval = getIntervalForLocalVar(checked((uint)variableIndex));
                    if (_compiler.info.compInitMem || varTypeIsGC(local.Type))
                    {
                        if (interval.recentRefPosition is null)
                        {
                            JITDUMP(" creating ZeroInit\n");
                            var position = newRefPosition(interval, MinLocation, RefType.RefTypeZeroInit,
                                null, allRegs(interval.registerType));
                            position.setRegOptional(true);
                            local.lvMustInit = true;
                        }
                        else
                        {
                            assert(interval.recentRefPosition.refType == RefType.RefTypeZeroInit);
                            JITDUMP(" already ZeroInited\n");
                        }
                    }
                }

                return true;
            });
        }
    }
}
