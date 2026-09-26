// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial class Compiler
{
    private const MemoryKindSet AllLoopMemoryKinds = (1 << (int)MemoryKindCount) - 1;

    public bool optVNIsLoopInvariant(ValueNum vn, FlowGraphNaturalLoop loop, VNSet loopVnInvariantCache)
    {
        assert(vnStore is not null);

        if (vn == ValueNumStore.NoVN)
        {
            return false;
        }

        if (vnStore.IsVNConstant(vn) || (vn == ValueNumStore.VNForVoid()))
        {
            return true;
        }

        if (loopVnInvariantCache.TryGetValue(vn, out var previousResult))
        {
            return previousResult;
        }

        var result = true;
        VNFuncApp funcApp = default;
        VNPhiDef phiDef = default;
        VNMemoryPhiDef memoryPhiDef = default;

        if (vnStore.GetVNFunc(vn, ref funcApp))
        {
            if (funcApp.FuncIs(VNF_MemOpaque))
            {
                var loopIndex = funcApp.GetArg(0);
                if (loopIndex == ValueNumStore.UnknownLoop)
                {
                    result = false;
                }
                else if (loopIndex != ValueNumStore.NoLoop)
                {
                    assert(_loops is not null);
                    var otherLoop = _loops.GetLoopByIndex(loopIndex);
                    result = !loop.ContainsLoop(otherLoop);
                }
            }
            else
            {
                for (var index = 0; index < funcApp.Arity; index++)
                {
                    if (funcApp.FuncIs(VNF_MapStore))
                    {
                        assert(funcApp.Arity == 4);
                        if (index == 3)
                        {
                            var loopIndex = funcApp.GetArg(3);
                            assert((loopIndex == ValueNumStore.NoLoop) ||
                                ((_loops is not null) && (loopIndex >= 0) && (loopIndex < _loops.NumLoops)));
                            if (loopIndex != ValueNumStore.NoLoop)
                            {
                                assert(_loops is not null);
                                result = !loop.ContainsLoop(_loops.GetLoopByIndex(loopIndex));
                            }
                            break;
                        }
                    }

                    if (!optVNIsLoopInvariant(funcApp.GetArg(index), loop, loopVnInvariantCache))
                    {
                        result = false;
                        break;
                    }
                }
            }
        }
        else if (vnStore.GetPhiDef(vn, ref phiDef))
        {
            var definitionBlock = lvaTable[phiDef.LclNum].GetPerSsaData(phiDef.SsaDef).Block;
            if (definitionBlock is not BasicBlock block)
            {
                throw new InvalidOperationException("The SSA definition block must be assigned before loop-invariance analysis.");
            }

            result = !loop.ContainsBlock(block);
        }
        else if (vnStore.GetMemoryPhiDef(vn, ref memoryPhiDef))
        {
            result = !loop.ContainsBlock(memoryPhiDef.Block);
        }

        loopVnInvariantCache[vn] = result;
        return result;
    }

    public void optComputeLoopSideEffects()
    {
        assert(_loops is not null);

        _loopSideEffects = _loops.NumLoops == 0 ? null : new LoopSideEffects[_loops.NumLoops];

        foreach (var loop in _loops.InReversePostOrder())
        {
            var effects = new LoopSideEffects
            {
                VarInOut = VarSetOps.MakeEmpty(this),
                VarUseDef = VarSetOps.MakeEmpty(this),
            };
            _loopSideEffects![loop.Index] = effects;
        }

        if (_loops.NumLoops == 0)
        {
            return;
        }

        if (_blockToLoop is not BlockToNaturalLoopMap blockToLoop)
        {
            throw new InvalidOperationException("Loop side-effect analysis requires a block-to-loop map.");
        }

        foreach (var loop in _loops.InReversePostOrder())
        {
            if (loop.Parent is not null)
            {
                continue;
            }

            _ = loop.VisitLoopBlocksReversePostOrder(block => {
                var mostNestedLoop = blockToLoop.GetLoop(block);
                assert(mostNestedLoop is not null);
                optComputeLoopSideEffectsOfBlock(block, mostNestedLoop);
                return BasicBlockVisit.Continue;
            });
        }
    }

    private unsafe void optComputeLoopSideEffectsOfBlock(BasicBlock block, FlowGraphNaturalLoop mostNestedLoop)
    {
        assert(_loopSideEffects is not null);
        assert(vnStore is not null);
        JITDUMP($"optComputeLoopSideEffectsOfBlock {FMT_BB(block.bbNum)}, mostNestedLoop L{mostNestedLoop.Index:D2}\n");
        AddVariableLivenessAllContainingLoops(mostNestedLoop, block);

        var memoryHavoc = 0;

        for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
        {
            foreach (var tree in stmt.TreeList)
            {
                var oper = tree.Oper;

                if (memoryHavoc == AllLoopMemoryKinds)
                {
                    if (oper is GT_CALL)
                    {
                        AddContainsCallAllContainingLoops(mostNestedLoop);
                    }

                    if (_loopSideEffects[mostNestedLoop.Index].ContainsCall)
                    {
                        break;
                    }

                    continue;
                }

                switch (oper)
                {
                    case GT_STORE_LCL_VAR:
                    case GT_STORE_LCL_FLD:
                    {
                        var local = tree.AsLclVarCommon();
                        var dataVN = local.Data._vnPair.Liberal;

                        if ((oper is GT_STORE_LCL_VAR) && (dataVN != ValueNumStore.NoVN))
                        {
                            dataVN = vnStore.VNNormalValue(dataVN);
                            if (local.HasSsaName)
                            {
                                ref var ssa = ref lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum);
                                ssa._vnPair.Liberal = dataVN;
                            }
                        }

                        if (lvaGetDesc(local.LclNum).IsAddressExposed)
                        {
                            memoryHavoc |= 1 << (int)ByrefExposed;
                        }

                        break;
                    }

                    case GT_IND:
                    case GT_BLK:
                    {
                        if (tree.AsIndir().IsVolatile)
                        {
                            memoryHavoc |= AllLoopMemoryKinds;
                        }

                        break;
                    }

                    case GT_STOREIND:
                    case GT_STORE_BLK:
                    {
                        if (tree.AsIndir().IsVolatile)
                        {
                            memoryHavoc |= AllLoopMemoryKinds;
                            continue;
                        }

                        var addr = tree.AsIndir().Addr.EffectiveVal;
                        if ((addr.Type is TYP_BYREF) && (addr.Oper is GT_LCL_VAR))
                        {
                            var local = addr.AsLclVar();
                            if (local.HasSsaName)
                            {
                                var argVN = lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum)._vnPair.Liberal;
                                VNFuncApp funcApp = default;
                                if ((argVN != ValueNumStore.NoVN) && vnStore.GetVNFunc(argVN, ref funcApp) &&
                                    funcApp.FuncIs(VNF_PtrToArrElem))
                                {
                                    assert(vnStore.IsVNHandle(funcApp.GetArg(0)));
                                    var elemType = (CORINFO_CLASS_HANDLE)
                                        (CORINFO_CLASS_STRUCT_*)vnStore.ConstantValue<nint>(funcApp.GetArg(0));
                                    AddModifiedElemTypeAllContainingLoops(mostNestedLoop, elemType);
                                    memoryHavoc |= 1 << (int)ByrefExposed;
                                    continue;
                                }
                            }

                            memoryHavoc |= AllLoopMemoryKinds;
                        }
                        else if (TryGetLoopArrayAddr(addr, out var arrAddr))
                        {
                            var elemType = EncodeElemType(arrAddr.ElemType, arrAddr.ElemClassHandle);
                            AddModifiedElemTypeAllContainingLoops(mostNestedLoop, elemType);
                            memoryHavoc |= 1 << (int)ByrefExposed;
                        }
                        else if (addr.IsFieldAddr(this, out var baseAddr, out var fieldSeq, out _))
                        {
                            if (fieldSeq is null)
                            {
                                throw new InvalidOperationException("A field address must carry a field sequence.");
                            }

                            var fieldKind = baseAddr is not null
                                ? FieldKindForVN.WithBaseAddr : FieldKindForVN.SimpleStatic;
                            AddModifiedFieldAllContainingLoops(mostNestedLoop, fieldSeq.FieldHandle, fieldKind);
                            memoryHavoc |= 1 << (int)ByrefExposed;
                        }
                        else
                        {
                            memoryHavoc |= AllLoopMemoryKinds;
                        }

                        break;
                    }

                    case GT_COMMA:
                    {
                        tree._vnPair = tree.AsOp().Op2._vnPair;
                        break;
                    }

                    case GT_ARR_ADDR:
                    {
                        var arrAddr = tree.AsArrAddr();
                        var elemType = EncodeElemType(arrAddr.ElemType, arrAddr.ElemClassHandle);
                        var elemTypeVN = vnStore.VNForHandle((nint)elemType, GTF_ICON_CLASS_HDL);
                        var ptrToElemVN = vnStore.VNForFunc(TYP_BYREF, VNF_PtrToArrElem,
                            elemTypeVN, ValueNumStore.VNForNull(), ValueNumStore.VNForNull(),
                            ValueNumStore.VNForNull());
                        tree._vnPair.SetBoth(ptrToElemVN);
                        break;
                    }

#if FEATURE_HW_INTRINSICS
                    case GT_HWINTRINSIC:
                    {
                        if (tree.AsHWIntrinsic().IsMemoryStoreOrBarrier)
                        {
                            memoryHavoc |= AllLoopMemoryKinds;
                        }

                        break;
                    }
#endif

                    case GT_LOCKADD:
                    case GT_XORR:
                    case GT_XAND:
                    case GT_XADD:
                    case GT_XCHG:
                    case GT_CMPXCHG:
                    case GT_MEMORYBARRIER:
                    {
                        memoryHavoc |= AllLoopMemoryKinds;
                        break;
                    }

                    case GT_CALL:
                    {
                        var call = tree.AsCall();
                        AddContainsCallAllContainingLoops(mostNestedLoop);

                        if (call.IsHelperCall())
                        {
                            var helper = call.HelperNum;
                            if (helper.MutatesHeap ||
                                (helper.MayRunCctor && (tree.Flags & GTF_CALL_HOISTABLE) == 0))
                            {
                                memoryHavoc |= AllLoopMemoryKinds;
                            }
                        }
                        else
                        {
                            memoryHavoc |= AllLoopMemoryKinds;
                        }

                        break;
                    }

                    default:
                    {
                        assert(!tree.RequiresAsgFlag);
                        break;
                    }
                }
            }

            stmt.RootNode._vnPair.SetBoth(ValueNumStore.NoVN);
        }

        if (memoryHavoc != 0)
        {
            optRecordLoopNestsMemoryHavoc(mostNestedLoop, memoryHavoc);
        }
    }

    private static bool TryGetLoopArrayAddr(GenTree address, [NotNullWhen(true)] out GenTreeArrAddr? arrAddr)
    {
        if ((address.Oper is GT_ADD) && address.AsOp().Op2.Oper.IsCnsIntOrI)
        {
            address = address.AsOp().Op1;
        }

        arrAddr = address.Oper is GT_ARR_ADDR ? address.AsArrAddr() : null;
        return arrAddr is not null;
    }

    public void optRecordLoopNestsMemoryHavoc(FlowGraphNaturalLoop loop, MemoryKindSet memoryHavoc)
    {
        assert(_loopSideEffects is not null);

        for (var current = (FlowGraphNaturalLoop?)loop; current is not null; current = current.Parent)
        {
            for (var kind = ByrefExposed; kind < MemoryKindCount; kind++)
            {
                if ((memoryHavoc & (1 << (int)kind)) != 0)
                {
                    _loopSideEffects[current.Index].HasMemoryHavoc[(int)kind] = true;
                }
            }
        }
    }

    public void AddContainsCallAllContainingLoops(FlowGraphNaturalLoop loop)
    {
        assert(_loopSideEffects is not null);

        for (var current = (FlowGraphNaturalLoop?)loop; current is not null; current = current.Parent)
        {
            _loopSideEffects[current.Index].ContainsCall = true;
        }
    }

    public void AddVariableLivenessAllContainingLoops(FlowGraphNaturalLoop loop, BasicBlock block)
    {
        assert(_loopSideEffects is not null);

        for (var current = (FlowGraphNaturalLoop?)loop; current is not null; current = current.Parent)
        {
            _loopSideEffects[current.Index].AddVariableLiveness(this, block);
        }
    }

    public unsafe void AddModifiedFieldAllContainingLoops(FlowGraphNaturalLoop loop,
        CORINFO_FIELD_HANDLE fieldHandle, FieldKindForVN fieldKind)
    {
        assert(_loopSideEffects is not null);

        for (var current = (FlowGraphNaturalLoop?)loop; current is not null; current = current.Parent)
        {
            _loopSideEffects[current.Index].AddModifiedField(this, fieldHandle, fieldKind);
        }
    }

    public unsafe void AddModifiedElemTypeAllContainingLoops(FlowGraphNaturalLoop loop,
        CORINFO_CLASS_HANDLE classHandle)
    {
        assert(_loopSideEffects is not null);

        for (var current = (FlowGraphNaturalLoop?)loop; current is not null; current = current.Parent)
        {
            _loopSideEffects[current.Index].AddModifiedElemType(this, classHandle);
        }
    }
}
