// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_WASM
    private struct WasmVirtualIPBlockData
    {
        // UNSET is the meet identity; MANY represents conflicting or unknown incoming values.
        public const uint VIP_UNSET = uint.MaxValue;
        public const uint VIP_MANY = uint.MaxValue - 1;

        public uint requiredVip;
        public uint funcIndex;
        public uint availableIn;
        public uint availableOut;
        public bool isRequired;
        public bool isFuncEntry;
    }

    private sealed class WasmVirtualIPMerge : DataFlow.ICallback
    {
        private readonly WasmVirtualIPBlockData[] _data;
        private uint _merge;

        public WasmVirtualIPMerge(WasmVirtualIPBlockData[] data)
        {
            _data = data;
            _merge = WasmVirtualIPBlockData.VIP_UNSET;
        }

        private void MeetInto(uint value)
        {
            if (value == WasmVirtualIPBlockData.VIP_UNSET)
            {
                return;
            }

            if (_merge == WasmVirtualIPBlockData.VIP_UNSET)
            {
                _merge = value;
            }
            else if ((_merge == WasmVirtualIPBlockData.VIP_MANY) ||
                (value == WasmVirtualIPBlockData.VIP_MANY) || (_merge != value))
            {
                _merge = WasmVirtualIPBlockData.VIP_MANY;
            }
        }

        public void StartMerge(BasicBlock block)
        {
            _merge = _data[block.bbNum].isFuncEntry
                ? WasmVirtualIPBlockData.VIP_MANY
                : WasmVirtualIPBlockData.VIP_UNSET;
        }

        public void Merge(BasicBlock block, BasicBlock predecessor, int duplicateCount)
        {
            var blockData = _data[block.bbNum];
            var predecessorData = _data[predecessor.bbNum];
            var crossFunc = predecessorData.funcIndex != blockData.funcIndex;
            MeetInto(crossFunc ? WasmVirtualIPBlockData.VIP_MANY : predecessorData.availableOut);
        }

        public void MergeHandler(BasicBlock block, BasicBlock firstTryBlock, BasicBlock lastTryBlock)
        {
            MeetInto(WasmVirtualIPBlockData.VIP_MANY);
        }

        public bool EndMerge(BasicBlock block)
        {
            ref var blockData = ref _data[block.bbNum];
            blockData.availableIn = _merge;

            var availableOut = blockData.isRequired ? blockData.requiredVip : _merge;
            if (availableOut == blockData.availableOut)
            {
                return false;
            }

            blockData.availableOut = availableOut;
            return true;
        }
    }

    public PhaseStatus fgWasmVirtualIP()
    {
        var virtualIP = 0u;
        var updatesAdded = 0u;
        EHClauseInfo[] clauses = [];
        ushort[] ehToVmOrder = [];

        if (compHndBBtabCount > 0)
        {
            clauses = new EHClauseInfo[compHndBBtabCount];
            ehToVmOrder = compEHTabOrderToVMClauseOrder
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "EH-to-VM clause order has not been initialized.");

            for (ushort ehIndex = 0; ehIndex < compHndBBtabCount; ehIndex++)
            {
                ref var descriptor = ref ehGetDsc(ehIndex);
                var clause = new CORINFO_EH_CLAUSE
                {
                    ClassToken = descriptor.HasFilter ? 0 : unchecked((int)descriptor.ebdTyp),
                    Flags = CodeGen.ToCORINFO_EH_CLAUSE_FLAGS(descriptor.ebdHandlerType),
                    TryOffset = 0,
                    TryLength = 0,
                    HandlerOffset = 0,
                    HandlerLength = 0,
                };

                clauses[ehToVmOrder[ehIndex]] = new EHClauseInfo { clause = clause, EHIndex = ehIndex };
            }
        }

        fgWasmEHInfo = compHndBBtabCount > 0 ? clauses : null;

        int getVirtualIPLclNum()
        {
            if (lvaWasmVirtualIP != BAD_VAR_NUM)
            {
                return lvaWasmVirtualIP;
            }

            lvaWasmVirtualIP = lvaGrabTemp(shortLifetime: true, "Wasm Virtual IP");
            ref var virtualIPVar = ref lvaGetDesc(lvaWasmVirtualIP);
            virtualIPVar.Type = TYP_INT;
            virtualIPVar.lvImplicitlyReferenced = true;
            lvaSetVarAddrExposed(lvaWasmVirtualIP, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);

            lvaWasmFunctionIndex = lvaGrabTemp(shortLifetime: true, "Wasm Function Index");
            ref var functionIndexVar = ref lvaGetDesc(lvaWasmFunctionIndex);
            functionIndexVar.Type = TYP_INT;
            functionIndexVar.lvImplicitlyReferenced = true;
            lvaSetVarAddrExposed(lvaWasmFunctionIndex, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);

            return lvaWasmVirtualIP;
        }

        // Funclets store through their SP local; the root method keeps its virtual IP in a local.
        void updateVirtualIPOnFrame(ref FuncInfoDsc func, BasicBlock block, uint vipValue, GenTree? beforeNode = null)
        {
            var virtualIPLocal = getVirtualIPLclNum();
            var virtualIPValue = gtNewIconNode(TYP_INT, unchecked((nint)vipValue));
            GenTree setVirtualIP;

            if (func.IsFunclet())
            {
                func.ensureUnwindableFrame(this);

                var spLocal = gtNewLclVarNode(TYP_I_IMPL, lvaWasmSpArg);
                var virtualIPSlotAddress = gtNewBinaryNode(
                    GT_ADD,
                    TYP_I_IMPL,
                    spLocal,
                    gtNewIconNode(TYP_INT, (nint)TARGET_POINTER_SIZE));
                var indirectFlags = GTF_IND_NONFAULTING | GTF_IND_TGT_NOT_HEAP;
                setVirtualIP = gtNewStoreIndNode(TYP_INT, virtualIPSlotAddress, virtualIPValue, indirectFlags);
            }
            else
            {
                setVirtualIP = gtNewStoreLclVarNode(virtualIPLocal, virtualIPValue);
            }

            var range = LIR.SeqTree(this, setVirtualIP);
            var firstNode = range.FirstNode;
            var lastNode = range.LastNode;

            if (beforeNode is not null)
            {
                block.InsertBefore(beforeNode, range);
            }
            else
            {
                block.InsertAtBeginning(range);
            }

            var lowering = _pLowering
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Lowering is unavailable during Wasm virtual-IP setup.");
            lowering.LowerRange(block, new LIR.ReadOnlyRange(firstNode, lastNode));
            updatesAdded = unchecked(updatesAdded + 1);
        }

        var blockData = new WasmVirtualIPBlockData[fgBBNumMax + 1];
        for (var blockNum = 0; blockNum < blockData.Length; blockNum++)
        {
            blockData[blockNum].availableIn = WasmVirtualIPBlockData.VIP_UNSET;
            blockData[blockNum].availableOut = WasmVirtualIPBlockData.VIP_UNSET;
        }

        var virtualIPFuncIndex = 0u;
        for (uint funcIndex = 0; funcIndex < compFuncInfoCount; funcIndex++)
        {
            ref var func = ref funGetFunc(funcIndex);
            func.startVirtualIP = virtualIP;
            var firstBlockInFunc = true;

            if (func.IsMethod())
            {
                // Reserve a value for the prolog before any EH or GC point.
                virtualIP = unchecked(virtualIP + 1);
            }

            foreach (var block in func.Blocks(this))
            {
                if (block.hasTryIndex)
                {
                    ref var tryDescriptor = ref ehGetDsc(block.TryIndex);
                    if (block == tryDescriptor.ebdTryBeg)
                    {
                        virtualIP = unchecked(virtualIP + 1);
                        var tryIndex = block.TryIndex;
                        var clauseIndex = ehToVmOrder[tryIndex];
                        clauses[clauseIndex].clause.TryOffset = unchecked((int)virtualIP);

                        foreach (ref var enclosingDescriptor in new EHClauses(this, tryIndex))
                        {
                            if (enclosingDescriptor.ebdTryBeg == block)
                            {
                                assert(EHblkDsc.ebdIsSameTry(tryDescriptor, enclosingDescriptor));
                                var enclosingTryIndex = ehGetIndex(enclosingDescriptor);
                                var enclosingClauseIndex = ehToVmOrder[enclosingTryIndex];
                                clauses[enclosingClauseIndex].clause.TryOffset = unchecked((int)virtualIP);
                            }
                        }
                    }
                }

                if (block.hasHndIndex)
                {
                    ref var handlerDescriptor = ref ehGetDsc(block.HndIndex);
                    if (block == handlerDescriptor.ebdHndBeg)
                    {
                        virtualIP = unchecked(virtualIP + 1);
                        var clauseIndex = ehToVmOrder[block.HndIndex];
                        clauses[clauseIndex].clause.HandlerOffset = unchecked((int)virtualIP);
                    }

                    if (handlerDescriptor.HasFilter && (block == handlerDescriptor.ebdFilter))
                    {
                        virtualIP = unchecked(virtualIP + 1);
                        var clauseIndex = ehToVmOrder[block.HndIndex];
                        clauses[clauseIndex].clause.ClassToken = unchecked((int)(
                            firstBlockInFunc ? func.startVirtualIP : virtualIP));
                    }
                }

                ref var data = ref blockData[block.bbNum];
                data.requiredVip = virtualIP;
                data.funcIndex = virtualIPFuncIndex;
                data.isRequired = !block.isEmpty() || (block.Kind is BBJ_CALLFINALLY);
                if (firstBlockInFunc)
                {
                    data.isFuncEntry = true;
                    firstBlockInFunc = false;
                }

                if (block.hasTryIndex)
                {
                    ref var tryDescriptor = ref ehGetDsc(block.TryIndex);
                    if (block == tryDescriptor.ebdTryLast)
                    {
                        virtualIP = unchecked(virtualIP + 1);
                        var tryIndex = block.TryIndex;
                        var clauseIndex = ehToVmOrder[tryIndex];
                        assert(virtualIP > unchecked((uint)clauses[clauseIndex].clause.TryOffset));
                        clauses[clauseIndex].clause.TryLength = unchecked((int)virtualIP);

                        foreach (ref var enclosingDescriptor in new EHClauses(this, tryIndex))
                        {
                            if (enclosingDescriptor.ebdTryLast == block)
                            {
                                var enclosingTryIndex = ehGetIndex(enclosingDescriptor);
                                var enclosingClauseIndex = ehToVmOrder[enclosingTryIndex];
                                assert(virtualIP > unchecked((uint)clauses[enclosingClauseIndex].clause.TryOffset));
                                clauses[enclosingClauseIndex].clause.TryLength = unchecked((int)virtualIP);
                            }
                        }
                    }
                }

                if (block.hasHndIndex)
                {
                    ref var handlerDescriptor = ref ehGetDsc(block.HndIndex);
                    if (block == handlerDescriptor.ebdHndLast)
                    {
                        virtualIP = unchecked(virtualIP + 1);
                        var clauseIndex = ehToVmOrder[block.HndIndex];
                        assert(virtualIP > unchecked((uint)clauses[clauseIndex].clause.HandlerOffset));
                        clauses[clauseIndex].clause.HandlerLength = unchecked((int)virtualIP);
                    }

                    if (handlerDescriptor.HasFilter && (block.Next == handlerDescriptor.ebdHndBeg))
                    {
                        virtualIP = unchecked(virtualIP + 1);
                    }
                }
            }

            if (func.IsMethod())
            {
                virtualIP = unchecked(virtualIP + 1);
            }

            func.endVirtualIP = virtualIP;
            virtualIPFuncIndex = unchecked(virtualIPFuncIndex + 1);
        }

        // A redundant store is unnecessary when the same IP reaches the block on every path.
        var vipMerge = new WasmVirtualIPMerge(blockData);
        new DataFlow(this).ForwardAnalysis(ref vipMerge);
        fgInvalidateDfsTree();

        for (uint funcIndex = 0; funcIndex < compFuncInfoCount; funcIndex++)
        {
            ref var func = ref funGetFunc(funcIndex);
            foreach (var block in func.Blocks(this))
            {
                ref var data = ref blockData[block.bbNum];
                assert(!data.isRequired ||
                    (data.availableOut == data.requiredVip) ||
                    (data.availableOut == WasmVirtualIPBlockData.VIP_UNSET));

                if (data.isRequired && (data.availableIn != data.requiredVip))
                {
                    updateVirtualIPOnFrame(ref func, block, data.requiredVip);
                }
            }
        }

#if DEBUG
        if (compHndBBtabCount > 0)
        {
            JITDUMP("EH virtual IP ranges\n");
            for (ushort ehIndex = 0; ehIndex < compHndBBtabCount; ehIndex++)
            {
                ref var descriptor = ref ehGetDsc(ehIndex);
                var clauseIndex = ehToVmOrder[ehIndex];
                ref var clause = ref clauses[clauseIndex].clause;

                JITDUMP($"EH#{ehIndex:D2}: Try [{clause.TryOffset:D4}..{clause.TryLength:D4})");
                if (descriptor.HasFilter)
                {
                    JITDUMP($" Filter [{clause.ClassToken:D4}..{clause.HandlerOffset:D4})\n");
                }

                JITDUMP($" Handler [{clause.HandlerOffset:D4}..{clause.HandlerLength:D4})\n");
            }

            for (uint func1Index = 0; func1Index < compFuncInfoCount; func1Index++)
            {
                ref var func1 = ref funGetFunc(func1Index);
                for (uint func2Index = 0; func2Index < compFuncInfoCount; func2Index++)
                {
                    if (func1Index == func2Index)
                    {
                        break;
                    }

                    ref var func2 = ref funGetFunc(func2Index);
                    assert((func1.endVirtualIP <= func2.startVirtualIP) ||
                        (func2.endVirtualIP <= func1.startVirtualIP));
                }
            }
        }
#endif

        return updatesAdded == 0 ? PhaseStatus.MODIFIED_NOTHING : PhaseStatus.MODIFIED_EVERYTHING;
    }
#endif
}
