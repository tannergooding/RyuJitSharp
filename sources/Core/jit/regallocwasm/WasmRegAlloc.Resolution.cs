// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class WasmRegAlloc
{
    private readonly HashSet<GenTree> _nodesWithInternalRegisters = [with(ReferenceEqualityComparer.Instance)];

    private void ResolveReferences()
    {
        var trackedIndexByLocalNumber = new int[_compiler.lvaCount];
        Array.Fill(trackedIndexByLocalNumber, -1);
        for (var trackedIndex = 0; trackedIndex < _compiler.lvaTrackedCount; trackedIndex++)
        {
            var localNumber = _compiler.lvaTrackedIndexToLclNum(trackedIndex);
            trackedIndexByLocalNumber[localNumber] = trackedIndex;
        }

        int GetTrackedIndex(int localNumber)
        {
            var trackedIndex = trackedIndexByLocalNumber[localNumber];
            return trackedIndex >= 0
                ? trackedIndex
                : throw new InvalidOperationException($"Local V{localNumber} has no tracked-register index.");
        }

        var temporaryRegMap = new TemporaryRegBank[(int)WasmValueType.Count];
        for (var type = WasmValueType.First; type < WasmValueType.Count; type = (WasmValueType)((uint)type + 1))
        {
            ref var temporaryRegisters = ref _temporaryRegs[(int)type];
            ref var allocatedTemporaryRegisters = ref temporaryRegMap[(int)type];
            assert(temporaryRegisters.Count == 0);

            allocatedTemporaryRegisters.Count = temporaryRegisters.MaxCount;
            if (allocatedTemporaryRegisters.Count == 0)
            {
                continue;
            }

            var registers = new regNumber[checked((int)allocatedTemporaryRegisters.Count)];
            allocatedTemporaryRegisters.Registers = registers;
            for (var index = 0; index < allocatedTemporaryRegisters.Count; index++)
            {
                registers[index] = AllocateVirtualRegister(type);
            }
        }

        for (var funcInfoIndex = _perFuncletData.Length - 1; funcInfoIndex >= 0; funcInfoIndex--)
        {
            ref var funcInfo = ref _compiler.compFuncInfos[funcInfoIndex];
            _currentFunclet = (int)funcInfo.GetFuncletIdx(_compiler);

            var virtualToPhysicalRegMap = new PhysicalRegBank[(int)WasmValueType.Count];
            for (var type = WasmValueType.First; type < WasmValueType.Count; type = (WasmValueType)((uint)type + 1))
            {
                virtualToPhysicalRegMap[(int)type].DeclaredCount = _virtualRegs[(int)type].Count;
            }

            uint indexBase = 0;
            var inFunclet = funcInfo.funKind != FuncKind.FUNC_ROOT;
            var data = GetFuncletData(_currentFunclet);
            var stackPointerVirtualReg = data.StackPointerReg;
            var framePointerVirtualReg = data.FramePointerReg;

            switch (funcInfo.funKind)
            {
                case FuncKind.FUNC_ROOT:
                {
                    for (var argLocalNumber = 0; argLocalNumber < _compiler.info.compArgsCount; argLocalNumber++)
                    {
                        var abiInfo = _compiler.lvaGetParameterAbiInfo(argLocalNumber);
                        foreach (var segment in abiInfo.Segments)
                        {
                            if (!segment.IsPassedInRegister)
                            {
                                continue;
                            }

                            var argRegister = segment.Register;
                            var argIndex = regNumberExtensions.UnpackWasmReg(argRegister, out var argType);
                            indexBase = uint.Max(indexBase, unchecked(argIndex + 1));

                            ref var argLocal = ref _compiler.lvaGetDesc(argLocalNumber);
                            if ((argLocal.RegNum == argRegister) || (argLocalNumber == _compiler.lvaWasmSpArg))
                            {
                                assert(abiInfo.HasExactlyOneRegisterSegment);
                                ref var regBank = ref virtualToPhysicalRegMap[(int)argType];
                                regBank.DeclaredCount = unchecked(regBank.DeclaredCount - 1);
                            }

                            var mapping = _compiler.FindParameterRegisterLocalMappingByRegister(argRegister);
                            if ((mapping is ParameterRegisterLocalMapping registerMapping) &&
                                (_compiler.lvaGetDesc(registerMapping.LclNum).RegNum == argRegister))
                            {
                                ref var regBank = ref virtualToPhysicalRegMap[(int)argType];
                                regBank.DeclaredCount = unchecked(regBank.DeclaredCount - 1);
                            }
                        }
                    }

                    break;
                }

                case FuncKind.FUNC_HANDLER:
                case FuncKind.FUNC_FILTER:
                {
                    var argType = regNumberExtensions.TypeToWasmValueType(TYP_I_IMPL);

                    if (stackPointerVirtualReg != REG_NA)
                    {
                        ref var regBank = ref virtualToPhysicalRegMap[(int)argType];
                        regBank.DeclaredCount = unchecked(regBank.DeclaredCount - 1);
                    }

                    if ((framePointerVirtualReg != REG_NA) && (framePointerVirtualReg != stackPointerVirtualReg))
                    {
                        ref var regBank = ref virtualToPhysicalRegMap[(int)argType];
                        regBank.DeclaredCount = unchecked(regBank.DeclaredCount - 1);
                    }

                    ref var ehDescriptor = ref funcInfo.GetEHDesc(_compiler);
                    indexBase = ehDescriptor.HasCatchHandler ? 3u : 2u;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            for (var type = WasmValueType.First; type < WasmValueType.Count; type = (WasmValueType)((uint)type + 1))
            {
                ref var registers = ref virtualToPhysicalRegMap[(int)type];
                registers.IndexBase = indexBase;
                registers.Index = indexBase;
                indexBase = unchecked(indexBase + registers.DeclaredCount);
            }

            regNumber AllocatePhysicalRegister(regNumber virtualRegister, int? localNumber)
            {
                var physicalRegister = REG_NA;

                if (!inFunclet)
                {
                    if (localNumber is int rootLocalNumber)
                    {
                        ref var local = ref _compiler.lvaGetDesc(rootLocalNumber);
                        if (local.lvIsRegArg && !local.lvIsStructField)
                        {
                            var abiInfo = _compiler.lvaGetParameterAbiInfo(rootLocalNumber);
                            assert(abiInfo.HasExactlyOneRegisterSegment);
                            var abiRegister = abiInfo.Segments[0].Register;
                            if (regNumberExtensions.WasmRegToType(virtualRegister) ==
                                regNumberExtensions.WasmRegToType(abiRegister))
                            {
                                physicalRegister = abiRegister;
                            }
                        }
                        else if (local.lvIsParamRegTarget)
                        {
                            var mapping = FindParameterRegisterLocalMapping(
                                rootLocalNumber,
                                0)
                                ?? throw new InvalidOperationException("A Wasm parameter register target has no register mapping.");
                            var abiRegister = mapping.RegisterSegment.Register;
                            if (regNumberExtensions.WasmRegToType(virtualRegister) ==
                                regNumberExtensions.WasmRegToType(abiRegister))
                            {
                                physicalRegister = abiRegister;
                            }
                        }
                    }
                }
                else if (virtualRegister == stackPointerVirtualReg)
                {
                    physicalRegister = regNumberExtensions.MakeWasmReg(
                        0,
                        regNumberExtensions.TypeToWasmValueType(TYP_I_IMPL));
                }
                else if (virtualRegister == framePointerVirtualReg)
                {
                    physicalRegister = regNumberExtensions.MakeWasmReg(
                        1,
                        regNumberExtensions.TypeToWasmValueType(TYP_I_IMPL));
                }

                if (physicalRegister == REG_NA)
                {
                    var type = regNumberExtensions.WasmRegToType(virtualRegister);
                    ref var registers = ref virtualToPhysicalRegMap[(int)type];
                    physicalRegister = regNumberExtensions.MakeWasmReg(registers.Index++, type);
                }

                assert(genIsValidReg(physicalRegister));
                if (localNumber is int assignedLocalNumber)
                {
                    ref var local = ref _compiler.lvaGetDesc(assignedLocalNumber);
                    if (local.lvIsRegCandidate)
                    {
                        data.PhysicalRegAssignments[GetTrackedIndex(assignedLocalNumber)] = physicalRegister;
                    }
                }

                return physicalRegister;
            }

            if (stackPointerVirtualReg != REG_NA)
            {
                data.StackPointerReg = AllocatePhysicalRegister(
                    stackPointerVirtualReg,
                    _compiler.lvaWasmSpArg);
            }

            if (framePointerVirtualReg != REG_NA)
            {
                data.FramePointerReg = (stackPointerVirtualReg == framePointerVirtualReg)
                    ? data.StackPointerReg
                    : AllocatePhysicalRegister(framePointerVirtualReg, null);
            }

            for (var varIndex = 0; varIndex < _compiler.lvaTrackedCount; varIndex++)
            {
                var localNumber = _compiler.lvaTrackedIndexToLclNum(varIndex);
                ref var local = ref _compiler.lvaGetDesc(localNumber);

                if (localNumber == _compiler.lvaWasmSpArg)
                {
                    continue;
                }

                if (local.lvIsRegCandidate)
                {
                    _ = AllocatePhysicalRegister(local.RegNum, localNumber);
                }
            }

            for (var type = WasmValueType.First; type < WasmValueType.Count; type = (WasmValueType)((uint)type + 1))
            {
                ref var registers = ref temporaryRegMap[(int)type];
                if (registers.Count == 0)
                {
                    continue;
                }

                var registerValues = registers.Registers
                    ?? throw new InvalidOperationException("A Wasm temporary register bank is not initialized.");
                for (var index = 0; index < registers.Count; index++)
                {
                    registerValues[index] = AllocatePhysicalRegister(registerValues[index], null);
                }
            }

            var referencesCount = data.LastVirtualRegRefsCount;
            for (var references = data.VirtualRegRefs; references is not null; references = references.Previous)
            {
                for (var index = 0; index < referencesCount; index++)
                {
                    var node = references.Nodes[index]
                        ?? throw new InvalidOperationException("A collected Wasm register reference is missing.");

                    if (node.Oper is GT_PHYSREG)
                    {
                        assert(node.AsPhysReg().SrcReg == stackPointerVirtualReg);
                        node.AsPhysReg().SetSrcReg(data.StackPointerReg);
                        assert(!genIsValidReg(node.RegNum));
                        continue;
                    }

                    var physicalRegister = REG_NA;
                    if (node.Oper is GT_STORE_LCL_VAR)
                    {
                        var localNumber = node.AsLclVarCommon().LclNum;
                        physicalRegister = data.PhysicalRegAssignments[GetTrackedIndex(localNumber)];
                    }
                    else if (genIsValidReg(node.RegNum))
                    {
                        assert(!node.Oper.IsLocal || !_compiler.lvaGetDesc(node.AsLclVarCommon().LclNum).lvIsRegCandidate);
                        var virtualRegisterIndex = regNumberExtensions.UnpackWasmReg(node.RegNum, out var type);
                        var temporaryRegisters = temporaryRegMap[(int)type].Registers
                            ?? throw new InvalidOperationException("A Wasm temporary register bank is not initialized.");
                        physicalRegister = temporaryRegisters[checked((int)virtualRegisterIndex)];
                    }

                    if (physicalRegister != REG_NA)
                    {
                        node.RegNum = physicalRegister;
                    }

                    if (_nodesWithInternalRegisters.Contains(node))
                    {
                        ref var internalRegisters = ref _codeGen.InternalRegisters.GetAll(node);
                        var count = internalRegisters.Count;
                        for (var internalRegisterIndex = 0; internalRegisterIndex < count; internalRegisterIndex++)
                        {
                            var virtualRegisterIndex = regNumberExtensions.UnpackWasmReg(
                                internalRegisters.GetAt(internalRegisterIndex),
                                out var type);
                            var temporaryRegisters = temporaryRegMap[(int)type].Registers
                                ?? throw new InvalidOperationException("A Wasm temporary register bank is not initialized.");
                            var internalPhysicalRegister = temporaryRegisters[checked((int)virtualRegisterIndex)];
                            internalRegisters.SetAt(internalRegisterIndex, internalPhysicalRegister);
                        }
                    }
                }

                referencesCount = ReferenceChunkSize;
            }

            assert(funcInfo.funWasmLocalDecls is null);
            var localDeclarations = new List<FuncInfoDsc.WasmLocalsDecl>();
            funcInfo.funWasmLocalDecls = localDeclarations;

            for (var type = WasmValueType.First; type < WasmValueType.Count; type = (WasmValueType)((uint)type + 1))
            {
                var declaredCount = virtualToPhysicalRegMap[(int)type].DeclaredCount;
                if (declaredCount != 0)
                {
                    localDeclarations.Add(new FuncInfoDsc.WasmLocalsDecl
                    {
                        Type = type,
                        Count = declaredCount,
                    });
                }
            }

            if (_compiler.lvaWasmResumeIP != BAD_VAR_NUM)
            {
                assert(funcInfo.funWasmExnRefLocalIndex == uint.MaxValue);
                funcInfo.funWasmExnRefLocalIndex = indexBase;
                localDeclarations.Add(new FuncInfoDsc.WasmLocalsDecl
                {
                    Type = WasmValueType.ExnRef,
                    Count = 1,
                });
            }
        }

        var mainFunctionAssignments = GetFuncletData(ROOT_FUNC_IDX).PhysicalRegAssignments;
        for (var varIndex = 0; varIndex < _compiler.lvaTrackedCount; varIndex++)
        {
            var localNumber = _compiler.lvaTrackedIndexToLclNum(varIndex);
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            var assignedRegister = mainFunctionAssignments[varIndex];

            if (!genIsValidReg(assignedRegister))
            {
                continue;
            }

            local.RegNum = assignedRegister;
            local.lvRegister = _compiler.compFuncCount() == 1;
            local.lvOnFrame = false;

            if (local.lvIsParam || local.lvIsParamRegTarget)
            {
                local.ArgInitReg = assignedRegister;
            }
        }
    }

    private ParameterRegisterLocalMapping? FindParameterRegisterLocalMapping(int localNumber, uint offset)
    {
        var mappings = _compiler._paramRegLocalMappings;
        if (mappings is not null)
        {
            for (var mappingIndex = 0; mappingIndex < mappings.Count; mappingIndex++)
            {
                var mapping = mappings[mappingIndex];
                if ((mapping.LclNum == localNumber) && (mapping.Offset == offset))
                {
                    return mapping;
                }
            }
        }

        return null;
    }

    private void PublishAllocationResults()
    {
        var usesFramePointer = false;

        for (var index = 0; index < _perFuncletData.Length; index++)
        {
            var data = GetFuncletData(index);

#if DEBUG
            if (index == ROOT_FUNC_IDX)
            {
                if (data.StackPointerReg != REG_NA)
                {
                    JITDUMP($"Allocated function SP into: {data.StackPointerReg.Name}\n");
                }

                if (data.FramePointerReg != REG_NA)
                {
                    JITDUMP($"Allocated function FP into: {data.FramePointerReg.Name}\n");
                }
            }
            else
            {
                JITDUMP($"Allocated funclet {index} SP into {data.StackPointerReg.Name}\n");
                JITDUMP($"Allocated funclet {index} FP into {data.FramePointerReg.Name}\n");
            }
#endif

            if (data.StackPointerReg != REG_NA)
            {
                _codeGen.SetStackPointerReg(index, data.StackPointerReg);
            }

            if (data.FramePointerReg != REG_NA)
            {
                _codeGen.SetFramePointerReg(index, data.FramePointerReg);
                usesFramePointer = true;
            }
        }

        _codeGen.IsFramePointerUsed = usesFramePointer;
        _compiler.raMarkStkVars();
        _compiler.compRegAllocDone = true;
    }
}
#endif
