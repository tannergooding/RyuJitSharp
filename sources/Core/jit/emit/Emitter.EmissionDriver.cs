// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.CorJitAllocMemFlag;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    // These native-pointer tables share the emitter's lifetime. Retain their pinned
    // owners across EE callbacks, instruction issue, and later emitter queries.
    private AllocMemChunk[]? _emissionDataChunks;
    private int[]? _emissionDataOffsets;
    private int[]? _emissionFrameOffsets;
    private byte[]? _emissionArgumentTracking;

    private unsafe nuint emitIssue1Instr(insGroup ig, instrDesc id, byte** dp)
    {
        var compiler = _compiler ?? throw new FatalJitException("Instruction issue requires an active compiler.");
        var curInsAdr = *dp;
        var size = emitOutputInstr(ig, id, dp);

#if DEBUG || LATE_DISASM
        var insExeCost = insEvaluateExecutionCost(id);
        var insPerfScore = (ig.igWeight / (double)BB_UNITY_WEIGHT) * insExeCost;
        compiler.Metrics.PerfScore += insPerfScore;
        ig.igPerfScore += insPerfScore;
#endif
#if EMIT_TRACK_STACK_DEPTH
        assert(!emitFullGCinfo || (emitCurStackLvl != 0) || (u2.emitGcArgTrackCnt == 0));
#endif
        var actualSize = unchecked((uint)(*dp - curInsAdr));
        var estimatedSize = id.idCodeSize();
        if (actualSize != estimatedSize)
        {
            noway_assert(estimatedSize >= actualSize);
#if FEATURE_LOOP_ALIGN
            assert(emitCurIG is not null);
            assert((id.idIns() != INS_align) && ((emitLastAlignedIg is null) || emitCurIG.IsAfter(emitLastAlignedIg)));
#endif
#if DEBUG || DEBUG_EMIT
            if (compiler.verbose)
            {
                jitprintf($"Instruction predicted size = {estimatedSize}, actual = {actualSize}\n");
            }
#endif
            // Adjust per instruction, not per group, so debug descriptor sizes cannot
            // affect the forward jump distances observed later in the same group.
            var offsShrinkage = unchecked((int)(estimatedSize - actualSize));
            JITDUMP($"Increasing size adj {emitOffsAdj} by {offsShrinkage} => {unchecked(emitOffsAdj + offsShrinkage)}\n");
            emitOffsAdj = unchecked(emitOffsAdj + offsShrinkage);
            ig.igFlags |= InsGroupFlags.UpdatedInstructionSize;
#if TARGET_XARCH
            id.idCodeSize(actualSize);
#elif TARGET_ARM
            // emitSetShortJump updates the ARM instruction size.
#else
            IMPL_LIMITATION("Over-estimated instruction size");
#endif
        }

#if DEBUG
        if (size != (nuint)emitSizeOfInsDsc(id))
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            jitprintf($"{emitIfName(id.idInsFmt())} at {debugInfo.idNum}: Expected size = {size} , actual size = {emitSizeOfInsDsc(id)}\n");
            assert(size == (nuint)emitSizeOfInsDsc(id));
        }
#endif
        return size;
    }

    public unsafe uint emitEndCodeGen(
        Compiler comp,
        bool contTrkPtrLcls,
        bool fullyInt,
        bool fullPtrMap,
        uint xcptnsCount,
        uint* prologSize,
        uint* epilogSize,
        void** codeAddr,
        void** codeAddrRW,
        void** coldCodeAddr,
        void** coldCodeAddrRW
#if DEBUG
        , uint* instrCount
#endif
        )
    {
        var compiler = _compiler ?? throw new FatalJitException("Final instruction emission requires an active compiler.");
#if DEBUG
        if (compiler.verbose)
        {
            jitprintf("*************** In emitEndCodeGen()\n");
        }
#endif
        assert(emitCurIG is null);

        // The inline table is inside a managed emitter. Keep it pinned for the
        // entire issuing interval, including allocation and diagnostic callbacks.
        fixed (byte* localArgTracking = &u2.emitArgTrackLcl[0])
        {
            emitCodeBlock = null;
            emitDataChunks = null;
            emitDataChunkOffsets = null;
            emitNumDataChunks = 0;
            emitOffsAdj = 0;

            emitFullyInt = fullyInt;
            emitFullGCinfo = fullPtrMap;
#if TARGET_X86
            emitFullArgInfo = fullPtrMap;
#else
            emitFullArgInfo = !emitHasFramePtr;
#endif
#if EMITTER_STATS
            GCrefsTable.record(unchecked((uint)emitGCrFrameOffsCnt));
            emitSizeTable.record(unchecked((uint)emitSizeMethod));
            stkDepthTable.record(unchecked((uint)emitMaxStackDepth));
#endif

            emitSimpleStkUsed = true;
            u1.emitSimpleStkMask = 0;
            u1.emitSimpleByrefStkMask = 0;

#if EMIT_TRACK_STACK_DEPTH
            var maxStackDepthIn4ByteElements = unchecked((uint)emitMaxStackDepth) / sizeof(int);
            JITDUMP($"Converting emitMaxStackDepth from bytes ({emitMaxStackDepth}) to elements ({maxStackDepthIn4ByteElements})\n");
            emitMaxStackDepth = unchecked((int)maxStackDepthIn4ByteElements);
            if ((emitMaxStackDepth > MAX_SIMPLE_STK_DEPTH) || emitFullGCinfo)
            {
                emitSimpleStkUsed = false;
                if (emitMaxStackDepth <= sizeof(InlineArray16<byte>))
                {
                    u2.emitArgTrackTab = localArgTracking;
                }
                else
                {
                    _emissionArgumentTracking = GC.AllocateArray<byte>(emitMaxStackDepth, pinned: true);
                    u2.emitArgTrackTab = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_emissionArgumentTracking));
                }
                u2.emitArgTrackTop = u2.emitArgTrackTab;
                u2.emitGcArgTrackCnt = 0;
            }
#endif
            if (emitEpilogCnt == 0)
            {
                emitEpilogSize = 0;
#if TARGET_XARCH
                emitExitSeqSize = 0;
#endif
            }
            *epilogSize = unchecked((uint)emitEpilogSize);
#if TARGET_XARCH
            *epilogSize = unchecked(*epilogSize + (uint)emitExitSeqSize);
#endif

#if DEBUG
            emitCheckIGList();
#endif
            var codeChunk = new AllocMemChunk
            {
                alignment = 1,
                size = emitTotalHotCodeSize,
                flags = CORJIT_ALLOCMEM_HOT_CODE,
            };
#if TARGET_X86
            // Profiled methods align when called more than 256 times per scenario;
            // without profile data only methods at most 16 bytes receive this alignment.
            if (compiler.fgHaveProfileData)
            {
                const weight_t scenarioHotWeight = 256.0;
                if (compiler.fgCalledCount > (scenarioHotWeight * compiler.fgProfileRunsCount()))
                {
                    codeChunk.alignment = 16;
                }
            }
            else if (unchecked((uint)emitTotalHotCodeSize) <= 16)
            {
                codeChunk.alignment = 16;
            }
#endif
#if TARGET_XARCH || TARGET_ARM64
            if (codeGen.ShouldAlignLoops && (unchecked((uint)emitTotalHotCodeSize) > 16) && compiler.fgHasLoops)
            {
                codeChunk.alignment = 32;
            }
#endif

            AllocMemChunk coldCodeChunk = default;
            if (emitTotalColdCodeSize != 0)
            {
                coldCodeChunk.alignment = 1;
                coldCodeChunk.size = emitTotalColdCodeSize;
                coldCodeChunk.flags = CORJIT_ALLOCMEM_COLD_CODE;
            }

            var numDataChunks = 0;
            for (var sec = emitConsDsc.dsdList; sec is not null; sec = sec.dsNext)
            {
                numDataChunks++;
            }
            if (numDataChunks != 0)
            {
                _emissionDataChunks = GC.AllocateArray<AllocMemChunk>(numDataChunks, pinned: true);
                _emissionDataOffsets = GC.AllocateArray<int>(numDataChunks, pinned: true);
                emitDataChunks = (AllocMemChunk*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_emissionDataChunks));
                emitDataChunkOffsets = (int*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_emissionDataOffsets));
            }
            emitNumDataChunks = numDataChunks;

            var dataChunk = emitDataChunks;
            var dataChunkOffset = emitDataChunkOffsets;
            for (var sec = emitConsDsc.dsdList; sec is not null; sec = sec.dsNext, dataChunk++, dataChunkOffset++)
            {
                comp.Metrics.ReadOnlyDataBytes += unchecked((int)sec.dsSize);
                dataChunk->alignment = (int)sec.dsAlignment;
                dataChunk->size = unchecked((int)sec.dsSize);
                dataChunk->flags = CORJIT_ALLOCMEM_READONLY_DATA;
                if (sec.dsType == dataSection.sectionType.asyncResumeInfo)
                {
                    dataChunk->flags |= CORJIT_ALLOCMEM_HAS_POINTERS_TO_CODE;
                }

                // Logical offsets include inter-section alignment padding and must
                // match the offsets embedded in constant-data references.
                *dataChunkOffset = unchecked((int)sec.dsOffset);
            }

            comp.Metrics.AllocatedHotCodeBytes = emitTotalHotCodeSize;
            comp.Metrics.AllocatedColdCodeBytes = emitTotalColdCodeSize;
            compiler.eeAllocMem(ref codeChunk, coldCodeChunk.size != 0 ? &coldCodeChunk : null,
                emitDataChunks, (uint)numDataChunks, xcptnsCount);

            *codeAddr = emitCodeBlock = codeChunk.block;
            *codeAddrRW = codeChunk.blockRW;
            *coldCodeAddr = emitColdCodeBlock = coldCodeChunk.size != 0 ? coldCodeChunk.block : null;
            *coldCodeAddrRW = coldCodeChunk.size != 0 ? coldCodeChunk.blockRW : null;

#if EMIT_TRACK_STACK_DEPTH
            emitCurStackLvl = 0;
#endif
            VarSetOps.ClearD(compiler, emitThisGCrefVars);
#if DEBUG && JIT32_ENCODER
            VarSetOps.ClearD(compiler, debugThisGCrefVars);
            VarSetOps.ClearD(compiler, debugPrevGCrefVars);
            debugPrevRegPtrDsc = null;
#endif
            emitThisGCrefRegs = (regMask)RBM_NONE;
            emitThisByrefRegs = (regMask)RBM_NONE;
            emitThisGCrefVset = true;

#if DEBUG
            emitIssuing = true;
            VarSetOps.AssignNoCopy(compiler, ref emitPrevGCrefVars, VarSetOps.UninitVal());
            emitPrevGCrefRegs = unchecked((regMask)0xBAADFEEDu);
            emitPrevByrefRegs = unchecked((regMask)0xBAADFEEDu);
            VarSetOps.AssignNoCopy(compiler, ref emitInitGCrefVars, VarSetOps.UninitVal());
            emitInitGCrefRegs = unchecked((regMask)0xBAADFEEDu);
            emitInitByrefRegs = unchecked((regMask)0xBAADFEEDu);
#endif
            codeGen.GCInfo.gcVarPtrSetInit();
            emitSyncThisObjOffs = -1;
            emitSyncThisObjReg = REG_NA;
#if JIT32_GCENCODER
            if (compiler.lvaKeepAliveAndReportThis())
            {
                assert(compiler.lvaIsOriginalThisArg(0));
                ref var thisDsc = ref compiler.lvaGetDesc(0);
                if (thisDsc.lvRegister)
                {
                    emitSyncThisObjReg = thisDsc.RegNum;
                    if ((emitSyncThisObjReg == REG_ARG_0)
                        && !(codeGen.CalleeRegArgMaskLiveIn & genRegMask(REG_ARG_0)).IsEmpty)
                    {
                        if (emitFullGCinfo)
                        {
                            emitGCregLiveSet(GCT_GCREF, genRegMask(REG_ARG_0), emitCodeBlock, true);
                        }
                    }
                }
            }
#endif
            emitContTrkPtrLcls = contTrkPtrLcls;

            if (emitGCrFrameOffsCnt != 0)
            {
                emitGCrFrameLiveTab = new GCInfo.varPtrDsc?[emitGCrFrameOffsCnt];
                emitTrkVarCnt = compiler.lvaTrackedCount;
                assert(emitTrkVarCnt != 0);
                _emissionFrameOffsets = GC.AllocateArray<int>(emitTrkVarCnt, pinned: true);
                _emissionFrameOffsets.AsSpan().Fill(-1);
                emitGCrFrameOffsTab = (int*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_emissionFrameOffsets));

                for (var num = 0; num < compiler.lvaCount; num++)
                {
                    ref var dsc = ref compiler.lvaGetDesc(num);
                    if (!dsc.lvOnFrame || (dsc.lvIsParam && !dsc.lvIsRegArg))
                    {
                        continue;
                    }
                    if (compiler.lvaIsUnknownSizeLocal(num))
                    {
                        continue;
                    }
#if FEATURE_FIXED_OUT_ARGS
                    if ((uint)num == compiler.lvaOutgoingArgSpaceVar)
                    {
                        continue;
                    }
#endif
                    var offs = dsc.StackOffset;
                    if ((offs >= emitGCrFrameOffsMin) && (offs < emitGCrFrameOffsMax))
                    {
                        if (!emitContTrkPtrLcls && !compiler.lvaIsGCTracked(in dsc))
                        {
                            continue;
                        }
                        var index = dsc._varIndex;
                        assert(!dsc.lvRegister);
                        assert(dsc.lvTracked);
                        assert(dsc.lvRefCnt(compiler.lvaRefCountState) != 0);
                        assert(dsc.Type is TYP_REF or TYP_BYREF);
                        assert(index < compiler.lvaTrackedCount);
                        if (dsc.Type == TYP_BYREF)
                        {
                            offs |= (int)byref_OFFSET_FLAG;
                        }
                        emitGCrFrameOffsTab[index] = offs;
                    }
                }
            }
#if DEBUG
            else
            {
                emitTrkVarCnt = 0;
                emitGCrFrameOffsTab = null;
            }
            if (compiler.verbose)
            {
                jitprintf("\n***************************************************************************\n");
                jitprintf("Instructions as they come out of the scheduler\n\n");
            }
#endif
            var cp = codeChunk.block;
            writeableOffset = unchecked((nint)(codeChunk.blockRW - codeChunk.block));

#if DEBUG
            *instrCount = 0;
            var nextMapping = compiler.genRichIPmappings.First;
#endif
#if DEBUG && TARGET_ARM64
            instrDesc? previousId = null;
#endif
            for (var ig = emitIGlist; ig is not null; ig = ig.igNext)
            {
#if DEBUG && TARGET_ARM64
                var groupDescriptors = ig.igData.AsSpan();
                for (var index = 0; index < ig.igInsCnt; index++)
                {
                    var currentId = groupDescriptors[index];
                    emitInsPairSanityCheck(previousId, currentId);
                    previousId = currentId;
                }
                if (ig.igNext is null)
                {
                    assert(previousId is not null);
                    assert(previousId.idIns() != INS_sve_movprfx);
                }
#endif
                assert((ig.igFlags & InsGroupFlags.Placeholder) == 0);
                if (ig == emitFirstColdIG)
                {
                    assert(emitCurCodeOffs(cp) == (uint)emitTotalHotCodeSize);
                    assert(coldCodeChunk.size != 0);
                    cp = coldCodeChunk.block;
                    writeableOffset = unchecked((nint)(coldCodeChunk.blockRW - coldCodeChunk.block));
                    emitOffsAdj = 0;
#if DEBUG
                    if (compiler.opts.disAsm || compiler.verbose)
                    {
                        jitprintf("\n************** Beginning of cold code **************\n");
                    }
#endif
                }

#if DEBUG
                if (compiler.opts.disAsm || compiler.verbose)
                {
                    if (compiler.verbose || compiler.opts.disasmWithGC)
                    {
                        jitprintf("\n");
                        emitDispIG(ig);
                    }
                    else
                    {
                        jitprintf($"\n{emitLabelString(ig)}:");
                        if (!compiler.opts.disDiffable)
                        {
                            if (compiler.opts.disAddr)
                            {
                                jitprintf("            ");
                            }
                            jitprintf($"  ;; offset=0x{emitCurCodeOffs(cp):X4}");
                        }
                        jitprintf("\n");
                    }
                }
#else
                if (compiler.opts.disAsm)
                {
                    jitprintf($"\n{emitLabelString(ig)}:");
                    if (!compiler.opts.disDiffable)
                    {
                        jitprintf($"                ;; offset=0x{emitCurCodeOffs(cp):X4}");
                    }
                    jitprintf("\n");
                }
#endif
                var bp = cp;
                var newOffsAdj = unchecked((int)(ig.igOffs - emitCurCodeOffs(cp)));
#if DEBUG
                if (compiler.verbose)
                {
                    if (newOffsAdj != 0)
                    {
                        jitprintf($"Block predicted offs = {ig.igOffs:X8}, actual = {emitCurCodeOffs(cp):X8} -> size adj = {newOffsAdj}\n");
                    }
                    if (emitOffsAdj != newOffsAdj)
                    {
                        jitprintf($"Block expected size adj {emitOffsAdj} not equal to actual size adj {newOffsAdj} (probably some instruction size was underestimated but not included in the running `emitOffsAdj` count)\n");
                    }
                }
                assert(emitOffsAdj == newOffsAdj);
#endif
                noway_assert(emitOffsAdj <= newOffsAdj);
                emitOffsAdj = newOffsAdj;
                assert(emitOffsAdj >= 0);
                ig.igOffs = emitCurCodeOffs(cp);
                assert(IsCodeAligned(ig.igOffs));

#if EMIT_TRACK_STACK_DEPTH
                if (ig.igStkLvl != (uint)emitCurStackLvl)
                {
                    assert(ig.igStkLvl > (uint)emitCurStackLvl);
                    emitStackPushN(cp, (ig.igStkLvl - (uint)emitCurStackLvl) / sizeof(int));
                }
#endif
                if ((ig.igFlags & InsGroupFlags.Extend) == 0)
                {
                    if ((ig.igFlags & InsGroupFlags.GCVars) != 0)
                    {
                        emitUpdateLiveGCvars(ig.igGCvars(), cp);
                    }
                    else if (!emitThisGCrefVset)
                    {
                        emitUpdateLiveGCvars(emitThisGCrefVars, cp);
                    }
                    if (ig.igGCregs != emitThisGCrefRegs)
                    {
                        emitUpdateLiveGCregs(GCT_GCREF, new regMaskTP(ig.igGCregs), cp);
                    }
                    if ((ig.igFlags & InsGroupFlags.ByrefRegs) != 0)
                    {
                        var byrefRegs = (regMask)ig.igByrefRegs();
                        if (byrefRegs != emitThisByrefRegs)
                        {
                            emitUpdateLiveGCregs(GCT_BYREF, new regMaskTP(byrefRegs), cp);
                        }
                    }
#if DEBUG
                    if (compiler.verbose || compiler.opts.disasmWithGC)
                    {
                        emitDispGCInfoDelta();
                    }
#endif
                }
                else
                {
                    assert((ig.igFlags & (InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs)) == 0);
                }

                emitCurIG = ig;
                var descriptors = ig.igData.AsSpan();
                assert(descriptors.Length == ig.igInsCnt);
                if (!compiler.opts.disAsm
#if DEBUG
                    && !compiler.verbose
#endif
                    )
                {
                    for (var index = 0; index < ig.igInsCnt; index++)
                    {
                        _ = emitIssue1Instr(ig, descriptors[index], &cp);
                    }
                }
                else
                {
                    for (var index = 0; index < ig.igInsCnt; index++)
                    {
                        var curInstrAddr = (nuint)cp;
                        var id = descriptors[index];
#if DEBUG
                        if ((compiler.opts.disAsm || compiler.verbose) && (JitConfig.JitDisasmWithDebugInfo != 0) &&
                            (id.idCodeSize() > 0))
                        {
                            var curCodeOffs = emitCurCodeOffs(cp);
                            while (nextMapping is not null)
                            {
                                var mappingOffs = nextMapping.Value.nativeLoc.CodeOffset(this);
                                if (mappingOffs > curCodeOffs)
                                {
                                    break;
                                }
                                if (mappingOffs == curCodeOffs)
                                {
                                    emitDispInsIndent();
                                    jitprintf("; ");
                                    nextMapping.Value.debugInfo.Dump(true);
                                    jitprintf("\n");
                                }
                                nextMapping = nextMapping.Next;
                            }
                        }
#endif
                        _ = emitIssue1Instr(ig, id, &cp);
                        if ((compiler.opts.disAsm
#if DEBUG
                            || compiler.verbose
#endif
                            ) && (
#if DEBUG
                            compiler.opts.disAddr ||
#endif
                            compiler.opts.disAlignment))
                        {
                            var afterInstrAddr = (nuint)cp;
#if TARGET_XARCH
                            var curIns = id.idIns();
#else
                            _ = id.idIns();
#endif
                            var isJccAffectedIns = false;
#if TARGET_XARCH
                            const nuint jccAlignBoundary = 32;
                            const nuint jccAlignBoundaryMask = jccAlignBoundary - 1;
                            var jccLastBoundaryAddr = afterInstrAddr & ~jccAlignBoundaryMask;

                            // Include fused op-Jcc pairs when the operation crosses the boundary.
                            // A Jcc cannot be the first instruction of a group.
                            if (curInstrAddr < jccLastBoundaryAddr)
                            {
                                isJccAffectedIns = IsJccInstruction(curIns) || IsJmpInstruction(curIns) ||
                                    (curIns is INS_call or INS_ret);
                                if (!isJccAffectedIns && (index + 1 < ig.igInsCnt))
                                {
                                    var nextIns = descriptors[index + 1].idIns();
                                    if ((curIns is INS_cmp or INS_test or INS_add or INS_sub or INS_and or INS_inc or INS_dec) &&
                                        IsJccInstruction(nextIns))
                                    {
                                        isJccAffectedIns = true;
                                    }
                                }
                                if (isJccAffectedIns)
                                {
                                    var bytesCrossedBoundary = (uint)(afterInstrAddr & jccAlignBoundaryMask);
                                    jitprintf($"; ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ ({codeGen.genInsDisplayName(id)}: {bytesCrossedBoundary} ; jcc erratum) {jccAlignBoundary}B boundary ...............................\n");
                                }
                            }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
                            isJccAffectedIns = true;
#endif
                            if (!isJccAffectedIns)
                            {
                                var alignBoundaryMask = (nuint)compiler.opts.compJitAlignLoopBoundary - 1;
                                var lastBoundaryAddr = afterInstrAddr & ~alignBoundaryMask;
                                if (curInstrAddr < lastBoundaryAddr)
                                {
                                    var bytesCrossedBoundary = (uint)(afterInstrAddr & alignBoundaryMask);
                                    if (bytesCrossedBoundary != 0)
                                    {
                                        jitprintf($"; ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ ({codeGen.genInsDisplayName(id)}: {bytesCrossedBoundary})");
                                    }
                                    else
                                    {
                                        jitprintf("; ...............................");
                                    }
                                    jitprintf($" {compiler.opts.compJitAlignLoopBoundary}B boundary ...............................\n");
                                }
                            }
                        }
                    }
                }

#if DEBUG
                if (compiler.opts.disAsm || compiler.verbose)
                {
                    jitprintf($"\t\t\t\t\t\t;; size={cp - bp} bbWeight={refCntWtd2str(ig.igWeight)} PerfScore {ig.igPerfScore:F2}");
                }
                *instrCount += ig.igInsCnt;
#else
                if (compiler.opts.disAsm)
                {
                    jitprintf(" ");
                }
#endif
                emitCurIG = null;
                assert(ig.igSize >= cp - bp);

                var lastHotIG = (emitFirstColdIG is not null) && (ig.igNext == emitFirstColdIG);
                if (lastHotIG)
                {
                    var actualHotCodeSize = emitCurCodeOffs(cp);
                    var allocatedHotCodeSize = (uint)emitTotalHotCodeSize;
                    assert(actualHotCodeSize <= allocatedHotCodeSize);
                    if (actualHotCodeSize < allocatedHotCodeSize)
                    {
                        var unusedHotSize = allocatedHotCodeSize - actualHotCodeSize;
                        var hotRW = unchecked(cp + writeableOffset);
                        for (var i = 0u; i < unusedHotSize; i++)
                        {
                            *hotRW++ = 0xCC;
                        }
                        cp = unchecked(hotRW - writeableOffset);
                        assert(allocatedHotCodeSize == emitCurCodeOffs(cp));
                    }
                }
                assert((ig.igSize >= cp - bp) || lastHotIG);
                ig.igSize = unchecked((ushort)(cp - bp));
            }

#if EMIT_TRACK_STACK_DEPTH
            assert(emitCurStackLvl == 0);
#endif
            if (emitConsDsc.dsdOffs != 0)
            {
                emitOutputDataSec(emitConsDsc, emitDataChunks);
            }
            if (emitGCrFrameOffsCnt != 0)
            {
                assert(emitGCrFrameLiveTab is not null);
                for (int vn = 0, offs = emitGCrFrameOffsMin; vn < emitGCrFrameOffsCnt; vn++, offs += TARGET_POINTER_SIZE)
                {
                    if (emitGCrFrameLiveTab[vn] is not null)
                    {
                        emitGCvarDeadSet(offs, cp, vn);
                    }
                }
            }
            if (emitThisByrefRegs != (regMask)RBM_NONE)
            {
                emitUpdateLiveGCregs(GCT_BYREF, RBM_NONE, cp);
            }
            if (emitThisGCrefRegs != (regMask)RBM_NONE)
            {
                emitUpdateLiveGCregs(GCT_GCREF, RBM_NONE, cp);
            }

            if (emitFwdJumps)
            {
                for (var jmp = emitJumpList; jmp is not null; jmp = jmp.idjNext)
                {
#if TARGET_XARCH
                    assert(jmp.idInsFmt() is IF_LABEL or IF_RWR_LABEL or IF_SWR_LABEL);
#endif
                    var target = jmp.idjTargetIG;
                    assert(target is not null);
                    if (jmp.idjAddr is null)
                    {
                        continue;
                    }
                    if (jmp.idjOffs != target.igOffs)
                    {
                        var adr = jmp.idjAddr;
                        var adj = unchecked((int)(jmp.idjOffs - target.igOffs));
#if TARGET_ARM
                        adj >>= 1;
#endif
#if DEBUG
                        var debugInfo = jmp.idDebugOnlyInfo();
                        assert(debugInfo is not null);
                        if ((debugInfo.idNum == unchecked((uint)INTERESTING_JUMP_NUM)) ||
                            (INTERESTING_JUMP_NUM == 0))
                        {
#if TARGET_ARM
                            jitprintf("[5] This output is broken for ARM, since it doesn't properly decode the jump offsets of " +
                                "the instruction at adr\n");
#endif
                            if (INTERESTING_JUMP_NUM == 0)
                            {
                                jitprintf($"[5] Jump {debugInfo.idNum}:\n");
                            }
                            if (jmp.idjShort)
                            {
                                jitprintf($"[5] Jump        is at {unchecked((nuint)(adr + 1 - emitCodeBlock)):X8}\n");
                                jitprintf($"[5] Jump distance is  {*adr:X2} - {adj:X2} = {unchecked(*adr - adj):X2}\n");
                            }
                            else
                            {
                                jitprintf($"[5] Jump        is at {unchecked((nuint)(adr + 4 - emitCodeBlock)):X8}\n");
                                jitprintf($"[5] Jump distance is  {*(int*)adr:X8} - {adj:X2} = {unchecked(*(int*)adr - adj):X8}\n");
                            }
                        }
#endif
                        if (jmp.idjShort)
                        {
#if TARGET_XARCH
                            var patch = unchecked(adr + writeableOffset);
                            *patch = unchecked((byte)(*patch - (byte)adj));
#elif TARGET_ARM
                            var patch = (short*)unchecked(adr + writeableOffset);
                            *patch = unchecked((short)(*patch - (short)adj));
#elif TARGET_ARM64
                            _ = emitOutputLJ(null, adr, jmp);
#elif TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
                            unreached();
#else
#error Unsupported or unset target architecture
#endif
                        }
                        else
                        {
#if TARGET_XARCH
                            var patch = (int*)unchecked(adr + writeableOffset);
                            *patch = unchecked(*patch - adj);
#elif TARGET_ARMARCH
                            _ = emitOutputLJ(null, adr, jmp);
#elif TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
                            unreached();
#else
#error Unsupported or unset target architecture
#endif
                        }
                    }
                }
            }

#if DEBUG
            if (compiler.opts.disAsm)
            {
                jitprintf("\n");
            }
#endif
            var actualCodeSize = emitCurCodeOffs(cp);
#if TARGET_ARM64
            assert((uint)emitTotalCodeSize == actualCodeSize);
#else
            assert((uint)emitTotalCodeSize >= actualCodeSize);
#endif
#if EMITTER_STATS
            totAllocdSize = unchecked(totAllocdSize + (uint)emitTotalCodeSize);
            totActualSize = unchecked(totActualSize + actualCodeSize);
#endif
            var unusedSize = (uint)emitTotalCodeSize - actualCodeSize;
            JITDUMP($"\n\nAllocated method code size = {emitTotalCodeSize,4} , actual size = {actualCodeSize,4}, unused size = {unusedSize,4}\n");

            // Padding after lifetime finalization must not extend GC ranges or the
            // reported code size. Hot-region padding above is part of its final IG.
            var cpRW = unchecked(cp + writeableOffset);
            for (var i = 0u; i < unusedSize; i++)
            {
                *cpRW++ = 0xCC;
            }
            cp = unchecked(cpRW - writeableOffset);
            assert((uint)emitTotalCodeSize == emitCurCodeOffs(cp));
            emitTotalCodeSize = unchecked((int)actualCodeSize);

#if DEBUG
            assert(VarSetOps.MaybeUninit(emitPrevGCrefVars));
            assert(emitPrevGCrefRegs == unchecked((regMask)0xBAADFEEDu));
            assert(emitPrevByrefRegs == unchecked((regMask)0xBAADFEEDu));
            assert(VarSetOps.MaybeUninit(emitInitGCrefVars));
            assert(emitInitGCrefRegs == unchecked((regMask)0xBAADFEEDu));
            assert(emitInitByrefRegs == unchecked((regMask)0xBAADFEEDu));
            emitCheckIGList();
#endif
            *prologSize = emitPrologEndPos.CodeOffset(this);
            comp.Metrics.ActualCodeBytes = unchecked((int)actualCodeSize);

            return actualCodeSize;
        }
    }
}
