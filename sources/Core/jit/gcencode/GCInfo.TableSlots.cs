// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.rpdArgType_t;
using static RyuJitSharp.GcSlotFlags;
using static RyuJitSharp.GcSlotState;
using static RyuJitSharp.GcStackSlotBase;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial struct GCInfo
{
    public enum MakeRegPtrMode
    {
        MAKE_REG_PTR_MODE_ASSIGN_SLOTS,
        MAKE_REG_PTR_MODE_DO_WORK,
    }

    private const uint OffsetMask = 3;
    private const uint ByrefOffsetFlag = 1;
    private const uint PinnedOffsetFlag = 2;

    public void gcMakeRegPtrTable(GcInfoEncoder gcInfoEncoder, uint codeSize, uint prologSize,
        MakeRegPtrMode mode, ref uint callCount)
    {
        if (!Configuration.TARGET_AMD64 || !Configuration.WINDOWS_AMD64_ABI ||
            Configuration.JIT32_GCENCODER)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.ModernGCTableEncodingRequiresWindowsAMD64);
            return;
        }

        var compiler = Compiler;
        var encoder = new GcInfoEncoderWithLogging(gcInfoEncoder, compiler);
        var noTrackedGCSlots = compiler.MinOpts;

        if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
        {
            _regSlotMap = [];
            _stackSlotMap = [];
        }

        for (var varNum = 0; varNum < compiler.lvaCount; varNum++)
        {
            ref var variable = ref compiler.lvaTable[varNum];
            if (compiler.lvaIsFieldOfDependentlyPromotedStruct(in variable))
            {
                continue;
            }
            if (varTypeIsGC(variable.Type))
            {
                if (!variable.lvIsParam)
                {
                    assert(!variable.lvPinned || !variable.lvTracked);
                    if ((variable.lvTracked && !noTrackedGCSlots) || !variable.lvOnFrame)
                    {
                        continue;
                    }
                }
                else if (!variable.lvOnFrame)
                {
                    if (!compiler.compJmpOpUsed)
                    {
                        continue;
                    }
                }
                else if (variable.lvIsRegArg && variable.lvTracked && !noTrackedGCSlots)
                {
                    continue;
                }

                var flags = GC_SLOT_UNTRACKED;
                if (variable.Type == TYP_BYREF)
                {
                    flags |= GC_SLOT_INTERIOR;
                }
                if (variable.lvPinned)
                {
                    flags |= GC_SLOT_PINNED;
                }
                var stackBase = variable.lvFramePointerBased ? GC_FRAMEREG_REL : GC_SP_REL;
                if (noTrackedGCSlots)
                {
                    if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
                    {
                        _ = encoder.GetStackSlotId(variable.StackOffset, flags, stackBase);
                    }
                }
                else if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
                {
                    gcAssignStackSlot(encoder, variable.StackOffset, flags, stackBase);
                }
            }

            if ((variable.Type == TYP_STRUCT) && variable.HasGCPtr && variable.lvOnFrame &&
                (variable.lvExactSize >= Configuration.TARGET_POINTER_SIZE))
            {
                var layout = variable.Layout;
                if (layout is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.GCStructLocalHasNoLayout);
                    return;
                }
                for (var slot = 0; slot < layout.SlotCount; slot++)
                {
                    if (!layout.IsGCPtr(slot))
                    {
                        continue;
                    }
                    var fieldOffset = slot * Configuration.TARGET_POINTER_SIZE;
                    var offset = unchecked(variable.StackOffset + fieldOffset);
                    if (Configuration.DEBUG && variable.lvPromoted)
                    {
                        assert(compiler.lvaGetPromotionType(in variable) == Compiler.PROMOTION_TYPE_DEPENDENT);
                        var fieldLocal = compiler.lvaGetFieldLocal(in variable, (uint)fieldOffset);
                        assert(fieldLocal != BAD_VAR_NUM);
                    }
                    var flags = GC_SLOT_UNTRACKED;
                    if (layout.GetGCPtrType(slot) == TYP_BYREF)
                    {
                        flags |= GC_SLOT_INTERIOR;
                    }
                    var stackBase = variable.lvFramePointerBased ? GC_FRAMEREG_REL : GC_SP_REL;
                    if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
                    {
                        gcAssignStackSlot(encoder, offset, flags, stackBase);
                    }
                }
            }
        }

        if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
        {
            assert(_codeGen.RegSet.tmpGetAllFree());
            for (var temp = _codeGen.RegSet.tmpListBeg(); temp is not null;
                temp = _codeGen.RegSet.tmpListNxt(temp))
            {
                if (!varTypeIsGC(temp.tdTempType))
                {
                    continue;
                }
                var flags = GC_SLOT_UNTRACKED;
                if (temp.tdTempType == TYP_BYREF)
                {
                    flags |= GC_SLOT_INTERIOR;
                }
                gcAssignStackSlot(encoder, temp.tdTempOffs, flags,
                    _codeGen.IsFramePointerUsed ? GC_FRAMEREG_REL : GC_SP_REL);
            }

            if (compiler.lvaKeepAliveAndReportThis())
            {
                assert(compiler.info.compThisArg != BAD_VAR_NUM);
                assert(!compiler.lvaReportParamTypeArg());
                var flags = GC_SLOT_UNTRACKED;
                if (compiler.lvaTable[compiler.info.compThisArg].Type == TYP_BYREF)
                {
                    flags |= GC_SLOT_INTERIOR;
                }
                _ = encoder.GetStackSlotId(compiler.lvaCachedGenericContextArgOffset(), flags,
                    _codeGen.IsFramePointerUsed ? GC_FRAMEREG_REL : GC_SP_REL);
            }
        }

        gcMakeVarPtrTable(encoder, mode);
        if (_codeGen.Interruptible)
        {
            assert(_codeGen.IsFullPtrRegMapRequired);
            var liveRegs = 0UL;
            regPtrDsc? firstStackArgument = null;
            for (var item = gcRegPtrList; item is not null; item = item.rpdNext)
            {
                if (item.rpdArg)
                {
                    if (item.rpdArgTypeGet() == rpdARG_KILL)
                    {
                        if ((mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK) &&
                            (firstStackArgument is not null))
                        {
                            gcInfoRecordGCStackArgsDead(encoder, item.rpdOffs, firstStackArgument, item);
                        }
                        firstStackArgument = null;
                    }
                    else if (item.rpdGCtypeGet() != GCT_NONE)
                    {
                        if ((item.rpdArgTypeGet() == rpdARG_PUSH) || (item.rpdCallData.rpdPtrArg != 0))
                        {
                            assert(item.rpdArgTypeGet() != rpdARG_POP);
                            gcInfoRecordGCStackArgLive(encoder, mode, item);
                            firstStackArgument ??= item;
                        }
                        else
                        {
                            assert(item.rpdArgTypeGet() == rpdARG_POP);
                            assert(item.rpdIsCallInstr());
                            if ((mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK) &&
                                (firstStackArgument is not null))
                            {
                                gcInfoRecordGCStackArgsDead(encoder, item.rpdOffs, firstStackArgument, item);
                            }
                            firstStackArgument = null;
                        }
                    }
                }
                else
                {
                    var mask = (ulong)item.rpdCompiler.rpdDel & liveRegs;
                    var byrefs = item.rpdGCtypeGet() == GCT_BYREF ? mask : 0;
                    gcInfoRecordGCRegStateChange(encoder, mode, item.rpdOffs, mask,
                        GC_SLOT_DEAD, byrefs, ref liveRegs);

                    mask = (ulong)item.rpdCompiler.rpdAdd & ~liveRegs;
                    byrefs = item.rpdGCtypeGet() == GCT_BYREF ? mask : 0;
                    gcInfoRecordGCRegStateChange(encoder, mode, item.rpdOffs, mask,
                        GC_SLOT_LIVE, byrefs, ref liveRegs);
                }
            }

            if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK)
            {
                assert(prologSize <= codeSize);
                var visitor = new UninterruptibleRangeVisitor(encoder, prologSize);
                _ = _codeGen.Emitter.emitGenNoGCLst(ref visitor);
                var uninterruptibleEnd = visitor.UninterruptibleEnd;

                if (uninterruptibleEnd < codeSize)
                {
                    encoder.DefineInterruptibleRange(uninterruptibleEnd, codeSize - uninterruptibleEnd);
                }
            }
        }
        else if (_codeGen.IsFramePointerUsed)
        {
            gcMakeFramePointerCallSites(encoder, mode, noTrackedGCSlots, ref callCount);
        }
        else
        {
            gcMakeFramelessCallSites(encoder, mode);
        }
    }

    private struct UninterruptibleRangeVisitor : INoGcRangeVisitor
    {
        private readonly GcInfoEncoderWithLogging _encoder;

        public uint UninterruptibleEnd;

        public UninterruptibleRangeVisitor(GcInfoEncoderWithLogging encoder, uint prologSize)
        {
            _encoder = encoder;
            UninterruptibleEnd = prologSize;
        }

        public bool Visit(uint funcIndex, uint offset, uint size, uint firstSize, bool isFuncletProlog)
        {
            if (offset < UninterruptibleEnd)
            {
                assert(funcIndex == 0);
                assert(unchecked(offset + size) <= UninterruptibleEnd);
                return true;
            }

            if (offset > UninterruptibleEnd)
            {
                var end = isFuncletProlog ? offset : unchecked(offset + firstSize);
                _encoder.DefineInterruptibleRange(UninterruptibleEnd, unchecked(end - UninterruptibleEnd));
            }

            UninterruptibleEnd = unchecked(offset + size);
            return true;
        }
    }

    private readonly void gcAssignStackSlot(GcInfoEncoderWithLogging encoder, int offset,
        GcSlotFlags flags, GcStackSlotBase stackBase)
    {
        var map = _stackSlotMap;
        if (map is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackSlotMapWasNotInitialized);
            return;
        }
        var key = new StackSlotIdKey(offset, stackBase == GC_FRAMEREG_REL, (uint)flags);
        if (!map.ContainsKey(key))
        {
            map.Add(key, encoder.GetStackSlotId(offset, flags, stackBase));
        }
    }

    private readonly uint gcGetStackSlot(int offset, GcSlotFlags flags, GcStackSlotBase stackBase)
    {
        var key = new StackSlotIdKey(offset, stackBase == GC_FRAMEREG_REL, (uint)flags);
        var map = _stackSlotMap;
        if (map is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackSlotMapWasNotInitialized);
            return default!;
        }
        if (!map.TryGetValue(key, out var slot))
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.LiveStackSlotWasNotAssigned);
            return default!;
        }
        return slot;
    }

    private void gcMakeVarPtrTable(GcInfoEncoderWithLogging encoder, MakeRegPtrMode mode)
    {
        if ((mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS) && (Compiler.compHndBBtabCount > 0))
        {
            gcMarkFilterVarsPinned();
        }
        for (var variable = gcVarPtrList; variable is not null; variable = variable.vpdNext)
        {
            var lowBits = variable.vpdVarNum & OffsetMask;
            var offset = unchecked((int)(variable.vpdVarNum & ~OffsetMask));
            if (variable.vpdEndOfs == variable.vpdBegOfs)
            {
                continue;
            }
            var flags = GC_SLOT_BASE;
            if ((lowBits & ByrefOffsetFlag) != 0)
            {
                flags |= GC_SLOT_INTERIOR;
            }
            if ((lowBits & PinnedOffsetFlag) != 0)
            {
                flags |= GC_SLOT_PINNED;
            }
            var stackBase = _codeGen.IsFramePointerUsed ? GC_FRAMEREG_REL : GC_SP_REL;
            if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
            {
                gcAssignStackSlot(encoder, offset, flags, stackBase);
            }
            else
            {
                var slot = gcGetStackSlot(offset, flags, stackBase);
                encoder.SetSlotState(variable.vpdBegOfs, slot, GC_SLOT_LIVE);
                encoder.SetSlotState(variable.vpdEndOfs, slot, GC_SLOT_DEAD);
            }
        }
    }

    private readonly void gcInfoRecordGCRegStateChange(GcInfoEncoderWithLogging encoder, MakeRegPtrMode mode,
        uint offset, ulong mask, GcSlotState state, ulong byrefs, ref ulong liveRegs)
    {
        assert((byrefs & ~mask) == 0);
        while (mask != 0)
        {
            var reg = (uint)BitOperations.TrailingZeroCount(mask);
            var bit = 1UL << (int)reg;
            liveRegs = state == GC_SLOT_DEAD ? liveRegs & ~bit : liveRegs | bit;
            assert(reg != (uint)TargetRuntimeMetadata.StackPointerBaseRegister);
            var flags = (byrefs & bit) != 0 ? GC_SLOT_INTERIOR : GC_SLOT_BASE;
            var key = new RegSlotIdKey((ushort)reg, (uint)flags);
            var map = _regSlotMap;
            if (map is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.RegisterSlotMapWasNotInitialized);
                return;
            }
            if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
            {
                if (!map.ContainsKey(key))
                {
                    map.Add(key, encoder.GetRegisterSlotId(reg, flags));
                }
            }
            else
            {
                if (!map.TryGetValue(key, out var slot))
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.LiveRegisterSlotWasNotAssigned);
                    return;
                }
                encoder.SetSlotState(offset, slot, state);
            }
            mask ^= bit;
        }
    }

    private readonly void gcInfoRecordGCStackArgLive(GcInfoEncoderWithLogging encoder, MakeRegPtrMode mode,
        regPtrDsc descriptor)
    {
        assert(_codeGen.Interruptible && descriptor.rpdArg && descriptor.rpdArgTypeGet() == rpdARG_PUSH);
        var flags = descriptor.rpdGCtypeGet() == GCT_BYREF ? GC_SLOT_INTERIOR : GC_SLOT_BASE;
        var offset = descriptor.rpdCallData.rpdPtrArg;
        if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS)
        {
            gcAssignStackSlot(encoder, offset, flags, GC_SP_REL);
        }
        else
        {
            encoder.SetSlotState(descriptor.rpdOffs, gcGetStackSlot(offset, flags, GC_SP_REL), GC_SLOT_LIVE);
        }
    }

    private readonly void gcInfoRecordGCStackArgsDead(GcInfoEncoderWithLogging encoder, uint offset,
        regPtrDsc first, regPtrDsc last)
    {
        assert(_codeGen.Interruptible);
        var descriptor = first;
        while (descriptor != last)
        {
            if (!descriptor.rpdArg)
            {
                var next = descriptor.rpdNext;
                if (next is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.OutgoingArgumentTerminatorIsMissing);
                    return;
                }

                descriptor = next;
                continue;
            }
            assert(descriptor.rpdGCtypeGet() != GCT_NONE && descriptor.rpdArgTypeGet() == rpdARG_PUSH);
            var flags = descriptor.rpdGCtypeGet() == GCT_BYREF ? GC_SLOT_INTERIOR : GC_SLOT_BASE;
            var slot = gcGetStackSlot(descriptor.rpdCallData.rpdPtrArg, flags, GC_SP_REL);
            encoder.SetSlotState(offset, slot, GC_SLOT_DEAD);
            var nextDescriptor = descriptor.rpdNext;
            if (nextDescriptor is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.OutgoingArgumentTerminatorIsMissing);
                return;
            }

            descriptor = nextDescriptor;
        }
    }

    private readonly unsafe void gcDefineCallSites(GcInfoEncoderWithLogging encoder, uint[]? offsets, byte[]? sizes, uint count)
    {
        if (count == 0)
        {
            encoder.DefineCallSites(null, null, 0);
            return;
        }
        assert(offsets is not null && sizes is not null);
        // DefineCallSites copies both buffers before the pins are released.
        fixed (uint* offsetPtr = offsets)
        fixed (byte* sizePtr = sizes)
        {
            encoder.DefineCallSites(offsetPtr, sizePtr, count);
        }
    }

    private readonly void gcMakeFramePointerCallSites(GcInfoEncoderWithLogging encoder, MakeRegPtrMode mode,
        bool noTrackedGCSlots, ref uint callCount)
    {
        assert(!_codeGen.IsFullPtrRegMapRequired);
        var total = 0u;
        uint[]? offsets = null;
        byte[]? sizes = null;
        if ((mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK) && (gcCallDescList is not null))
        {
            if (noTrackedGCSlots)
            {
                total = callCount;
                if (total == 0)
                {
                    gcDefineCallSites(encoder, null, null, 0);
                    return;
                }
            }
            else
            {
                for (var call = gcCallDescList; call is not null; call = call.cdNext)
                {
                    total++;
                }
            }
            offsets = new uint[checked((int)total)];
            sizes = new byte[checked((int)total)];
        }

        var callSite = 0u;
        var liveRegs = 0UL;
        for (var call = gcCallDescList; call is not null; call = call.cdNext)
        {
            assert(call.cdArgMask == 0 && call.cdArgCnt == 0);
            var gcrefs = (ulong)call.cdGCrefRegs;
            var byrefs = (ulong)call.cdByrefRegs;
            assert((gcrefs & byrefs) == 0);
            var mask = gcrefs | byrefs;
            assert(call.cdOffs >= call.cdCallInstrSize);
            var callOffset = call.cdOffs - call.cdCallInstrSize;
            if (noTrackedGCSlots && mask == 0)
            {
                continue;
            }
            if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK)
            {
                assert(offsets is not null && sizes is not null);
                offsets[callSite] = callOffset;
                assert(call.cdCallInstrSize <= byte.MaxValue);
                sizes[callSite] = checked((byte)call.cdCallInstrSize);
            }
            callSite++;
            gcInfoRecordGCRegStateChange(encoder, mode, callOffset, mask, GC_SLOT_LIVE, byrefs, ref liveRegs);
            gcInfoRecordGCRegStateChange(encoder, mode, call.cdOffs, mask, GC_SLOT_DEAD, byrefs, ref liveRegs);
        }
        assert((mode != MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK) || (total == callSite));
        callCount = callSite;
        if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK)
        {
            gcDefineCallSites(encoder, offsets, sizes, total);
        }
    }

    private readonly void gcMakeFramelessCallSites(GcInfoEncoderWithLogging encoder, MakeRegPtrMode mode)
    {
        assert(_codeGen.IsFullPtrRegMapRequired);
        var total = 0u;
        if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK)
        {
            for (var item = gcRegPtrList; item is not null; item = item.rpdNext)
            {
                if (item.rpdArg && item.rpdIsCallInstr())
                {
                    total++;
                }
            }
        }
        var offsets = total > 0 ? new uint[checked((int)total)] : null;
        var sizes = total > 0 ? new byte[checked((int)total)] : null;
        var callSite = 0u;
        var liveRegs = 0UL;
        for (var item = gcRegPtrList; item is not null; item = item.rpdNext)
        {
            if (!item.rpdArg)
            {
                continue;
            }
            if (item.rpdIsCallInstr())
            {
                var gcrefs = (ulong)item.rpdCallData.rpdCallGCrefRegs << (int)TargetRuntimeMetadata.IntegerRegisterFirst;
                var byrefs = (ulong)item.rpdCallData.rpdCallByrefRegs << (int)TargetRuntimeMetadata.IntegerRegisterFirst;
                assert((gcrefs & byrefs) == 0);
                var mask = gcrefs | byrefs;
                assert(item.rpdOffs >= item.rpdCallInstrSize);
                var callOffset = item.rpdOffs - item.rpdCallInstrSize;
                gcInfoRecordGCRegStateChange(encoder, mode, callOffset, mask, GC_SLOT_LIVE, byrefs, ref liveRegs);
                gcInfoRecordGCRegStateChange(encoder, mode, item.rpdOffs, mask, GC_SLOT_DEAD, byrefs, ref liveRegs);
                if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK)
                {
                    assert(offsets is not null && sizes is not null);
                    offsets[callSite] = callOffset;
                    sizes[callSite++] = item.rpdCallInstrSize;
                }
            }
            else
            {
                assert(item.rpdGCtypeGet() != GCT_NONE && item.rpdArgTypeGet() == rpdARG_PUSH);
            }
        }
        if (mode == MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK)
        {
            gcDefineCallSites(encoder, offsets, sizes, total);
        }
    }
}
