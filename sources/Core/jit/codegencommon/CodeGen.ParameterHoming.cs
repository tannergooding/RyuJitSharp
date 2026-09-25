// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using System.Numerics;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genHomeRegisterParams(regNumber initReg, ref bool initRegStillZeroed)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Incoming parameter homing requires Windows AMD64.");
#else
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genHomeRegisterParams()\n");
        }
#endif
        if (_calleeRegArgMaskLiveIn.IsEmpty)
        {
            return;
        }

        var paramRegs = _calleeRegArgMaskLiveIn;
        if (_compiler.opts.OptimizationDisabled)
        {
            for (var localNumber = 0; localNumber < _compiler.info.compArgsCount; localNumber++)
            {
                ref var local = ref _compiler.lvaGetDesc(localNumber);
                if (!local.lvOnFrame)
                {
                    continue;
                }

                ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(localNumber);
                foreach (ref readonly var segment in abiInfo.Segments)
                {
                    if (segment.IsPassedInRegister && (paramRegs & RegisterMask(segment.Register)).IsNonEmpty)
                    {
                        var storeType = genParamStackType(in local, in segment);
                        Emitter.emitIns_S_R(ins_Store(storeType), storeType.EmitActualSize,
                            segment.Register, localNumber, segment.Offset);
                    }
                }
            }

            if (_compiler.info.compPublishStubParam
                && (paramRegs & RegisterMask(REG_SECRET_STUB_PARAM)).IsNonEmpty)
            {
                ref var stub = ref _compiler.lvaGetDesc(_compiler.lvaStubArgumentVar);
                if (stub.lvOnFrame)
                {
                    Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE,
                        REG_SECRET_STUB_PARAM, _compiler.lvaStubArgumentVar, 0);
                }
            }

            return;
        }

        var graph = new RegGraph();
        for (var localNumber = 0; localNumber < _compiler.info.compArgsCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(localNumber);
            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (!segment.IsPassedInRegister)
                {
                    continue;
                }

                var mapping = _compiler.FindParameterRegisterLocalMappingByRegister(segment.Register);
                var spillToBaseLocal = true;
                if (mapping is ParameterRegisterLocalMapping mapped)
                {
                    genSpillOrAddRegisterParam(mapped.LclNum, mapped.Offset, localNumber,
                        in segment, graph);
                    if (local.lvPromoted)
                    {
                        spillToBaseLocal = false;
                    }
                }

                if (spillToBaseLocal)
                {
                    genSpillOrAddRegisterParam(localNumber, unchecked((uint)segment.Offset),
                        localNumber, in segment, graph);
                }
            }
        }

        if (_compiler.info.compPublishStubParam
            && (paramRegs & RegisterMask(REG_SECRET_STUB_PARAM)).IsNonEmpty)
        {
            genSpillOrAddNonStandardRegisterParam(_compiler.lvaStubArgumentVar, REG_SECRET_STUB_PARAM, graph);
        }

#if DEBUG
        if (_verbose)
        {
            graph.Dump();
        }
        graph.Validate();
#endif

        var busyRegs = _calleeRegArgMaskLiveIn;
        while (true)
        {
            var node = graph.FindNodeToProcess();
            if (node is null)
            {
                break;
            }

            assert(node.Incoming is not null);
            if ((node.Outgoing is not null) && (node.CopiedReg == REG_NA))
            {
                var copyType = node.Outgoing.Type;
                var tempCandidates = genGetParameterHomingTempRegisterCandidates() & ~busyRegs;
                var typeMask = new regMaskTP(varTypeUsesFloatReg(copyType) ? SRBM_ALLFLOAT : SRBM_ALLINT);
                var available = tempCandidates & typeMask;
                noway_assert(available.IsNonEmpty);

                node.CopiedReg = (regNumber)BitOperations.TrailingZeroCount((ulong)available.Lower);
                busyRegs |= RegisterMask(node.CopiedReg);
                var ins = ins_Copy(node.Reg, copyType);
                _ = Emitter.emitIns_Mov(ins, copyType.EmitActualSize,
                    node.CopiedReg, node.Reg, canSkip: false);
                if (node.CopiedReg == initReg)
                {
                    initRegStillZeroed = false;
                }
            }

            for (var edge = node.Incoming; edge is not null; edge = edge.NextIncoming)
            {
                if (edge.DestOffset != 0)
                {
                    continue;
                }

                var sourceReg = edge.From.CopiedReg != REG_NA ? edge.From.CopiedReg : edge.From.Reg;
                var ins = ins_Copy(sourceReg, edge.Type.ActualType);
                _ = Emitter.emitIns_Mov(ins, edge.Type.EmitActualSize, node.Reg, sourceReg, canSkip: true);
                break;
            }

            for (var edge = node.Incoming; edge is not null; edge = edge.NextIncoming)
            {
                if (edge.DestOffset != 0)
                {
                    // Windows AMD64 never inserts partial incoming registers into a destination.
                    noway_assert(false, "Insertion into register is not supported");
                }
            }

            graph.RemoveIncomingEdges(node, ref busyRegs);
            busyRegs |= RegisterMask(node.Reg);
            if (node.Reg == initReg)
            {
                initRegStillZeroed = false;
            }
        }
#endif
    }

    public void genEnregisterIncomingStackArgs()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Incoming stack argument enregistration requires Windows AMD64.");
#else
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genEnregisterIncomingStackArgs()\n");
        }
#endif
        assert(!_compiler.opts.IsOSR);
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(_compiler.fgFirstBB is not null);

        for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
        {
            ref var local = ref _compiler.lvaGetDesc(varNum);
            if (!local.lvIsParam || local.lvIsRegArg || !local.lvIsInReg)
            {
                continue;
            }
            if (!VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, local._varIndex))
            {
                continue;
            }

            var reg = local.ArgInitReg;
            assert(reg != REG_STK);
            genLoadLocalIntoReg(reg, varNum);
            _regSet.verifyRegUsed(reg);
        }
#endif
    }

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    private static regMaskTP RegisterMask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    private void genSpillOrAddRegisterParam(int localNumber, uint offset, int paramLocalNumber,
        in AbiPassingSegment segment, RegGraph graph)
    {
        var paramRegs = _calleeRegArgMaskLiveIn;
        if (!segment.IsPassedInRegister || (paramRegs & RegisterMask(segment.Register)).IsEmpty)
        {
            return;
        }

        ref var local = ref _compiler.lvaGetDesc(localNumber);
        if (local.lvOnFrame && (!local.lvIsInReg || local.IsLiveInOutOfHandler))
        {
            ref var param = ref _compiler.lvaGetDesc(paramLocalNumber);
            var storeType = genParamStackType(in param, in segment);
            if ((local.Type != TYP_STRUCT) && (local.GetRegisterType().ActualType.Size < storeType.Size))
            {
                storeType = local.GetRegisterType().ActualType;
            }

            Emitter.emitIns_S_R(ins_Store(storeType), storeType.EmitActualSize,
                segment.Register, localNumber, unchecked((int)offset));
        }

        if (!local.lvIsInReg)
        {
            return;
        }

        var edgeType = local.GetRegisterType().ActualType;
        if (segment.Size < edgeType.Size)
        {
            edgeType = segment.GetRegisterType();
        }

        var source = graph.GetOrAdd(segment.Register);
        var destination = graph.GetOrAdd(local.RegNum);
        if (!ReferenceEquals(source, destination) || (offset != 0))
        {
            graph.AddEdge(source, destination, edgeType, offset);
        }
    }

    private void genSpillOrAddNonStandardRegisterParam(int localNumber, regNumber sourceReg, RegGraph graph)
    {
        ref var local = ref _compiler.lvaGetDesc(localNumber);
        if (local.lvOnFrame && (!local.lvIsInReg || local.IsLiveInOutOfHandler))
        {
            Emitter.emitIns_S_R(ins_Store(local.Type), local.Type.EmitActualSize,
                sourceReg, localNumber, 0);
        }

        if (local.lvIsInReg)
        {
            var source = graph.GetOrAdd(sourceReg);
            var destination = graph.GetOrAdd(local.RegNum);
            if (!ReferenceEquals(source, destination))
            {
                graph.AddEdge(source, destination, TYP_I_IMPL, 0);
            }
        }
    }

    private void genLoadLocalIntoReg(regNumber reg, int localNumber)
    {
        ref var local = ref _compiler.lvaGetDesc(localNumber);
        var type = local.GetStackSlotHomeType();
        Emitter.emitIns_R_S(ins_Load(type), type.EmitSize, reg, localNumber, 0);
    }
#endif
}
