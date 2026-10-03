// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM
using System.Numerics;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genHomeRegisterParams(regNumber initReg, ref bool initRegStillZeroed)
    {
#if TARGET_WASM
        genHomeRegisterParamsWasm();
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

#if TARGET_ARM
                var segmentMask = new regMaskTP((regMask)((ulong)segment.RegisterMask << segment.RegisterMaskBase));
                spillToBaseLocal &= (_regSet.rsMaskPreSpillRegs(false) & segmentMask).IsEmpty;
#endif
                if (spillToBaseLocal)
                {
                    genSpillOrAddRegisterParam(localNumber, unchecked((uint)segment.Offset),
                        localNumber, in segment, graph);
                }
            }
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
#if TARGET_ARM
            // Adjacent float edges from one double can be copied together when neither half participates in a cycle.
            if (genIsValidFloatReg(node.Reg) && (node.Incoming.NextIncoming is null)
                && (node.Outgoing is null) && (node.Incoming.From.CopiedReg == REG_NA))
            {
                var isLowReg = genIsValidDoubleReg(node.Reg);
                var otherNode = graph.Get((regNumber)((int)node.Reg + (isLowReg ? 1 : -1)));
                if ((otherNode is RegNode other) && (other.Incoming is RegNodeEdge otherIncoming)
                    && (otherIncoming.NextIncoming is null) && (otherIncoming.From.CopiedReg == REG_NA)
                    && (other.Outgoing is null))
                {
                    var lowNode = isLowReg ? node : other;
                    var highNode = isLowReg ? other : node;
                    var lowIncoming = isLowReg ? node.Incoming : otherIncoming;
                    var highIncoming = isLowReg ? otherIncoming : node.Incoming;
                    if (genIsValidDoubleReg(lowIncoming.From.Reg)
                        && (highIncoming.From.Reg == (regNumber)((int)lowIncoming.From.Reg + 1)))
                    {
                        var ins = ins_Copy(lowIncoming.From.Reg, TYP_DOUBLE);
                        Emitter.emitIns_Mov(ins, EA_8BYTE, lowNode.Reg, lowIncoming.From.Reg, canSkip: false);
                        graph.RemoveIncomingEdges(lowNode, ref busyRegs);
                        graph.RemoveIncomingEdges(highNode, ref busyRegs);
                        busyRegs |= RegisterMask(lowNode.Reg) | RegisterMask(highNode.Reg);
                        assert((lowNode.Reg != initReg) && (highNode.Reg != initReg));
                        continue;
                    }
                }
            }
#endif
            if ((node.Outgoing is not null) && (node.CopiedReg == REG_NA))
            {
                var copyType = node.Outgoing.Type;
                var tempCandidates = genGetParameterHomingTempRegisterCandidates() & ~busyRegs;
                var typeMask = new regMaskTP(varTypeUsesFloatReg(copyType) ? SRBM_ALLFLOAT : SRBM_ALLINT);
                var available = tempCandidates & typeMask;
                noway_assert(available.IsNonEmpty);

                node.CopiedReg = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)available.Lower));
                busyRegs |= RegisterMask(node.CopiedReg);
                var ins = ins_Copy(node.Reg, copyType);
#if TARGET_XARCH
                _ =
#endif
                Emitter.emitIns_Mov(ins, copyType.EmitActualSize,
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
#if TARGET_XARCH
                _ =
#endif
                Emitter.emitIns_Mov(ins, edge.Type.EmitActualSize, node.Reg, sourceReg, canSkip: true);
                break;
            }

            for (var edge = node.Incoming; edge is not null; edge = edge.NextIncoming)
            {
                if (edge.DestOffset != 0)
                {
#if TARGET_ARM64 || UNIX_AMD64_ABI
                    var sourceReg = edge.From.CopiedReg != REG_NA ? edge.From.CopiedReg : edge.From.Reg;
#endif
#if TARGET_ARM64
                    Emitter.emitIns_R_R_I_I(INS_mov, edge.Type.EmitSize, node.Reg, sourceReg,
                        (nint)(edge.DestOffset / (uint)edge.Type.Size), 0);
#elif UNIX_AMD64_ABI
                    noway_assert(edge.DestOffset == 8);
                    assert(genIsValidFloatReg(node.Reg));
                    Emitter.emitIns_R_R_I(INS_shufpd, EA_16BYTE, node.Reg, sourceReg, 0);
#else
                    noway_assert(false, "Insertion into register is not supported");
#endif
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
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genEnregisterIncomingStackArgs()\n");
        }
#endif
        assert(!_compiler.opts.IsOSR);
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(_compiler.fgFirstBB is not null);

#if TARGET_LOONGARCH64
        var tempOffset = 0;
        var tempReg = REG_NA;
#endif
        for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
        {
            ref var local = ref _compiler.lvaGetDesc(varNum);
            if (!local.lvIsParam)
            {
                continue;
            }

            var isPrespilledForProfiling = false;
#if TARGET_ARM && PROFILING_SUPPORTED
            isPrespilledForProfiling = _compiler.compIsProfilerHookNeeded
                && _compiler.lvaIsPreSpilled(varNum, _regSet.rsMaskPreSpillRegs(false));
#endif
            if (local.lvIsRegArg && !isPrespilledForProfiling)
            {
                continue;
            }

            if (!local.lvIsInReg)
            {
                continue;
            }
            if (!VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, local._varIndex))
            {
                continue;
            }

            var reg = local.ArgInitReg;
            assert(reg != REG_STK);
#if TARGET_LOONGARCH64
            var baseOffset = _compiler.lvaFrameAddress(varNum, out var fpBased);
            var regType = local.GetStackSlotHomeType();
            if (RyuJitSharp.Emitter.isValidSimm12(baseOffset))
            {
                Emitter.emitIns_R_S(ins_Load(regType), regType.EmitSize, reg, varNum, 0);
            }
            else if (tempReg == REG_NA)
            {
                var frameReg = fpBased ? REG_FPBASE : REG_SPBASE;
                tempOffset = baseOffset;
                tempReg = REG_R21;
                Emitter.emitIns_I_la(EA_PTRSIZE, REG_R21, baseOffset);
                Emitter.emitIns_R_R_R(INS_add_d, EA_PTRSIZE, REG_R21, REG_R21, frameReg);
                Emitter.emitIns_R_S(ins_Load(regType), regType.EmitSize, reg, varNum, -8);
            }
            else
            {
                var relativeOffset = unchecked(-(baseOffset - tempOffset) - 8);
                Emitter.emitIns_R_S(ins_Load(regType), regType.EmitSize, reg, varNum, relativeOffset);
            }
#else
            genLoadLocalIntoReg(reg, varNum);
#endif
            _regSet.verifyRegUsed(reg);
        }
    }

#if !TARGET_WASM
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
            if ((local.Type != TYP_STRUCT) && (local.Type.ActualType.Size < storeType.Size))
            {
                storeType = local.Type.ActualType;
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
#if TARGET_ARM
            if (edgeType == TYP_DOUBLE)
            {
                assert(offset == 0);
                graph.AddEdge(source, destination, TYP_FLOAT, 0);
                source = graph.GetOrAdd((regNumber)((int)source.Reg + 1));
                destination = graph.GetOrAdd((regNumber)((int)destination.Reg + 1));
                graph.AddEdge(source, destination, TYP_FLOAT, 0);

                return;
            }
#endif
            graph.AddEdge(source, destination, edgeType, offset);
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
