// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using System.Collections.Generic;
using System.IO;
using static RyuJitSharp.FuncKind;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class WasmRegAlloc : IRegAlloc
{
    private const int ReferenceChunkSize = 16;

    private readonly Compiler _compiler;
    private readonly ICodeGen _codeGen;
    private readonly VirtualRegStack[] _virtualRegs = new VirtualRegStack[(int)WasmValueType.Count];
    private readonly TemporaryRegStack[] _temporaryRegs = new TemporaryRegStack[(int)WasmValueType.Count];
    private readonly PerFuncletData?[] _perFuncletData;

    private BasicBlock? _currentBlock;
    private int _currentFunclet = ROOT_FUNC_IDX;

    private sealed class PerFuncletData
    {
        public PerFuncletData(int trackedCount)
        {
            PhysicalRegAssignments = new regNumber[trackedCount];
            Array.Fill(PhysicalRegAssignments, REG_STK);
        }

        public regNumber StackPointerReg = REG_NA;
        public regNumber FramePointerReg = REG_NA;
        public int LastVirtualRegRefsCount;
        public VirtualRegReferences? VirtualRegRefs;
        public regNumber[] PhysicalRegAssignments { get; }
    }

    private struct VirtualRegStack
    {
        private WasmValueType _type;
        private uint _nextIndex;

        public readonly bool IsInitialized => _type != WasmValueType.Invalid;

        public readonly uint Count => _nextIndex;

        public regNumber Push()
        {
            assert(IsInitialized);
            return regNumberExtensions.MakeWasmReg(_nextIndex++, _type);
        }

        public void Pop()
        {
            assert(IsInitialized);
            assert(_nextIndex != 0);
            _nextIndex--;
        }

        public VirtualRegStack(WasmValueType type)
        {
            _type = type;
            _nextIndex = 0;
        }
    }

    private struct TemporaryRegStack
    {
        public uint Count;
        public uint MaxCount;

        public uint Push()
        {
            var index = Count++;
            MaxCount = uint.Max(MaxCount, Count);
            return index;
        }

        public uint Pop()
        {
            assert(Count > 0);
            return --Count;
        }
    }

    private sealed class VirtualRegReferences
    {
        public readonly GenTree?[] Nodes = new GenTree?[ReferenceChunkSize];
        public VirtualRegReferences? Previous;
    }

    private struct TemporaryRegBank
    {
        public regNumber[]? Registers;
        public uint Count;
    }

    private struct PhysicalRegBank
    {
        public uint DeclaredCount;
        public uint IndexBase;
        public uint Index;
    }

    public WasmRegAlloc(Compiler compiler)
    {
        _compiler = compiler;
        _codeGen = compiler.codeGen ?? throw new InvalidOperationException("Wasm register allocation requires code generation.");
        _perFuncletData = new PerFuncletData?[compiler.compFuncCount()];
    }

    public static IRegAlloc GetRegisterAllocator(Compiler compiler)
    {
        return new WasmRegAlloc(compiler);
    }

    public PhaseStatus DoRegisterAllocation()
    {
        for (var index = 0; index < _perFuncletData.Length; index++)
        {
            _perFuncletData[index] = new PerFuncletData(_compiler.lvaTrackedCount);
        }

        IdentifyCandidates();
        CollectReferences();
        ResolveReferences();
        PublishAllocationResults();

        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    public void recordVarLocationsAtStartOfBB(BasicBlock block)
    {
        var isFuncEntry = _compiler.fgFirstBB == block;
        var isFuncletEntry = _compiler.bbIsFuncletBeg(block);

        if (!isFuncletEntry && !isFuncEntry)
        {
            return;
        }

        var funcletIndex = isFuncEntry ? ROOT_FUNC_IDX : (int)_compiler.funGetFuncIdx(block);

        void UpdateOrVerifyAssignments(bool verify = false)
        {
            var data = GetFuncletData(funcletIndex);
            var assignments = data.PhysicalRegAssignments;
            var hasAssignment = false;

            if (isFuncletEntry)
            {
                JITDUMP($"{(verify ? "Reporting" : "Updating")} Var Locations to start of funclet {funcletIndex} entry {FMT_BB(block.bbNum)}\n");
            }
            else
            {
                JITDUMP($"{(verify ? "Reporting" : "Updating")} Var Locations to start of method entry {FMT_BB(block.bbNum)}\n");
            }

            for (var varIndex = 0; varIndex < assignments.Length; varIndex++)
            {
                var localNumber = _compiler.lvaTrackedIndexToLclNum(varIndex);
                ref var local = ref _compiler.lvaGetDesc(localNumber);
                var reg = assignments[varIndex];

                if (verify)
                {
                    assert(local.RegNum == reg);
                }
                else
                {
                    local.RegNum = reg;
                }

                if (reg != REG_STK)
                {
                    JITDUMP($"  V{localNumber:D2}({reg.Name})");
                    hasAssignment = true;
                }
            }

            JITDUMP($"{(hasAssignment ? "" : "  <none>")}\n");
        }

        if (_currentFunclet == funcletIndex)
        {
#if DEBUG
            UpdateOrVerifyAssignments(verify: true);
#endif
            return;
        }

        UpdateOrVerifyAssignments();
        _currentFunclet = funcletIndex;
    }

    public bool WillEnregisterLocalVars()
    {
        return _compiler.compEnregLocals;
    }

#if TRACK_LSRA_STATS
    public void dumpLsraStatsCsv(StreamWriter streamWriter)
    {
    }

    public void dumpLsraStatsSummary(StreamWriter streamWriter)
    {
    }
#endif

    public bool IsRegCandidate(in LclVarDsc varDsc)
    {
        if (!WillEnregisterLocalVars())
        {
            return false;
        }

        assert(_compiler.compEnregLocals);

        if (!varDsc.lvTracked)
        {
            return false;
        }

#if LOWER_DECOMPOSE_LONGS
        if (varDsc.Type is TYP_LONG)
        {
            return false;
        }
#endif

        if (_compiler.compJmpOpUsed && varDsc.lvIsRegArg)
        {
            return false;
        }

        if (_compiler.lvaIsFieldOfDependentlyPromotedStruct(in varDsc))
        {
            return false;
        }

        if (varDsc.lvRefCnt() is 0)
        {
            ref var local = ref _compiler.lvaGetDesc(_compiler.lvaGetLclNum(in varDsc));
            local.setLvRefCntWtd(0);
            return false;
        }

        if (varDsc.lvDoNotEnregister)
        {
            return false;
        }

        switch (varDsc.Type.ActualType)
        {
            case TYP_FLOAT:
            case TYP_DOUBLE:
            {
                return !_compiler.opts.compDbgCode;
            }

            case TYP_INT:
            case TYP_LONG:
            case TYP_REF:
            case TYP_BYREF:
            {
                return true;
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
            {
                return !varDsc.lvPromoted;
            }
#endif

            case TYP_STRUCT:
            {
                return _compiler.compEnregStructLocals && !varDsc.HasGCPtr;
            }

            default:
            {
                return false;
            }
        }
    }

    public bool IsContainableMemoryOp(GenTree node)
    {
        if (node.IsMemoryOp)
        {
            return true;
        }

        if (!node.Oper.IsLocal)
        {
            return false;
        }

        if (!WillEnregisterLocalVars())
        {
            return true;
        }

        return _compiler.lvaGetDesc(node.AsLclVarCommon().LclNum).lvDoNotEnregister;
    }

    private void CheckForDNER(int localNumber, in LclVarDsc local)
    {
        if (local.lvDoNotEnregister)
        {
            return;
        }

        if (!_compiler.compEnregLocals)
        {
            _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.NoRegVars);
            return;
        }

        if (varTypeIsStruct(local.Type) && !local.lvPromoted)
        {
            if (!local.IsEnregisterableType)
            {
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.NotRegSizeStruct);
                return;
            }

            if (local.Type is TYP_STRUCT)
            {
                if (!local.lvRegStruct && !_compiler.compEnregStructLocals)
                {
                    _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.DontEnregStructs);
                    return;
                }

                if (local.lvIsMultiRegArgOrRet)
                {
                    _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.IsStructArg);
                    return;
                }

#if TARGET_ARM
                if (local.lvIsParam)
                {
                    _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.IsStructArg);
                    return;
                }
#endif
            }
        }

        if (local.lvPinned)
        {
            _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.PinningRef);
            return;
        }

        if (local.lvTracked && local.IsLiveInOutOfHandler)
        {
            if (!_compiler.IsEHVarARegCandidate(in local))
            {
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LiveInOutOfHandler);
                return;
            }

#if JIT32_GCENCODER
            if (_compiler.lvaKeepAliveAndReportThis() && (localNumber == _compiler.info.compThisArg))
            {
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LiveInOutOfHandler);
                return;
            }
#endif
        }
    }

    private PerFuncletData GetFuncletData(int index)
    {
        assert((uint)index < (uint)_perFuncletData.Length);
        return _perFuncletData[index] ?? throw new InvalidOperationException("Wasm funclet allocation state is not initialized.");
    }
}
#endif
