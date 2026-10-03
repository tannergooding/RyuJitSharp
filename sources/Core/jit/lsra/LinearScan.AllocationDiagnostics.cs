// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
#endif
#if DEBUG || TRACK_LSRA_STATS
using System.Globalization;
#endif
#if TRACK_LSRA_STATS
using System.IO;
#endif

namespace RyuJitSharp;

#if TRACK_LSRA_STATS
internal enum LsraStat
{
    STAT_SPILL,
    STAT_COPY_REG,
    STAT_RESOLUTION_MOV,
    STAT_SPLIT_EDGE,
    STAT_FREE,
    STAT_CONST_AVAILABLE,
    STAT_THIS_ASSIGNED,
    STAT_COVERS,
    STAT_OWN_PREFERENCE,
    STAT_COVERS_RELATED,
    STAT_RELATED_PREFERENCE,
    STAT_CALLER_CALLEE,
    STAT_UNASSIGNED,
    STAT_COVERS_FULL,
    STAT_BEST_FIT,
    STAT_IS_PREV_REG,
    STAT_REG_ORDER,
    STAT_SPILL_COST,
    STAT_FAR_NEXT_REF,
    STAT_PREV_REG_OPT,
    STAT_REG_NUM,
    COUNT,
}
#endif

public sealed partial class LinearScan
{
#if DEBUG
    private enum LsraDumpEvent
    {
        DEFUSE_CONFLICT,
        DEFUSE_DEF_IN_FIXED_USE,
        DEFUSE_DEF_IN_USE,
        DEFUSE_ANY_DEF,
        DEFUSE_COPY,
        SPILL,
        SPILL_EXTENDED_LIFETIME,
        RESTORE_PREVIOUS_INTERVAL,
        RESTORE_PREVIOUS_INTERVAL_AFTER_SPILL,
        DONE_KILL_GC_REFS,
        NO_GC_KILLS,
        START_BB,
        END_BB,
        FREE_REGS,
        UPPER_VECTOR_SAVE,
        UPPER_VECTOR_RESTORE,
        KILL_REGS,
        INCREMENT_RANGE_END,
        LAST_USE,
        LAST_USE_DELAYED,
        NEEDS_NEW_REG,
        FIXED_REG,
        EXP_USE,
        ZERO_REF,
        NO_ENTRY_REG_ALLOCATED,
        KEPT_ALLOCATION,
        COPY_REG,
        MOVE_REG,
        ALLOC_REG,
        NO_REG_ALLOCATED,
        RELOAD,
        SPECIAL_PUTARG,
        REUSE_REG,
    }

    private static int s_allocationDumpEmptyColumnWidth;

    private bool _allocationDumpFormatInitialized;
    private regMaskTP _allocationDumpRegisters;
    private regMaskTP _lastAllocationDumpRegisters;
    private RefPosition? _lastAllocationDumpRefPosition;
    private int _allocationDumpLastUsedRegNumIndex;
    private int _allocationDumpRegColumnWidth;
    private int _allocationDumpNodeLocationWidth;
    private int _allocationDumpRefPositionWidth;
    private int _allocationDumpShortRefPositionWidth;
    private int _allocationDumpTableIndent;
    private int _allocationDumpBbNumWidth;
    private int _allocationDumpPredBbNumWidth;
    private int _allocationDumpRowsSinceTitle;
    private string _allocationDumpColumnSeparator = "";
    private string _allocationDumpLine = "";
    private string _allocationDumpMiddleBox = "";
    private string _allocationDumpRightBox = "";

    private void dumpLsraAllocationEvent(
        LsraDumpEvent dumpEvent,
        Interval? interval,
        RefPosition? activeRefPosition,
        regNumber register = REG_NA,
        BasicBlock? currentBlock = null,
        RegisterScore registerScore = RegisterScore.NONE,
        regMaskTP registerMask = default)
    {
        if (!VERBOSE)
        {
            return;
        }

        initializeAllocationDumpFormat();
        if ((interval is not null) && (register is not REG_NA and not REG_STK))
        {
            _allocationDumpRegisters |=
                regMaskTP.CreateFromRegNum(register, getSingleTypeRegMask(register, interval.registerType));
            dumpAllocationRegisterTitleIfNeeded();
        }

        switch (dumpEvent)
        {
            case LsraDumpEvent.DEFUSE_CONFLICT:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("DUconflict    ");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.DEFUSE_DEF_IN_FIXED_USE:
            {
                dumpAllocationIndentedText("  Define in fixed use reg");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.DEFUSE_DEF_IN_USE:
            {
                dumpAllocationIndentedText("  Define in candidate use reg");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.DEFUSE_ANY_DEF:
            {
                dumpAllocationIndentedText("  Define in any reg");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.DEFUSE_COPY:
            {
                dumpAllocationIndentedText("  Need a copy");
                dumpAllocationRegisterRecords();
                if (interval is null)
                {
                    dumpAllocationIndentedText("    NULL interval");
                    dumpAllocationRegisterRecords();
                }
                else
                {
                    assert(interval.firstRefPosition is not null);
                    if (interval.firstRefPosition.multiRegIdx != 0)
                    {
                        dumpAllocationIndentedText("    (multiReg)");
                        dumpAllocationRegisterRecords();
                    }
                }

                break;
            }

            case LsraDumpEvent.SPILL:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                assert(interval is not null && interval.assignedReg is not null);
                jitprintf($"Spill    {interval.assignedReg.regNum.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.RESTORE_PREVIOUS_INTERVAL:
            case LsraDumpEvent.RESTORE_PREVIOUS_INTERVAL_AFTER_SPILL:
            {
                assert(interval is not null);
                if ((activeRefPosition is null) || (activeRefPosition.refType is RefType.RefTypeBB))
                {
                    dumpRefPositionShort(null);
                }
                else
                {
                    dumpRefPositionShort(activeRefPosition, currentBlock);
                }

                jitprintf(
                    $"{(dumpEvent is LsraDumpEvent.RESTORE_PREVIOUS_INTERVAL ? "Restr" : "SRstr")}    {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.DONE_KILL_GC_REFS:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("Done          ");
                break;
            }

            case LsraDumpEvent.NO_GC_KILLS:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("None          ");
                break;
            }

            case LsraDumpEvent.START_BB:
            {
                assert(activeRefPosition is not null);
                if (activeRefPosition.refType is not RefType.RefTypeBB)
                {
                    dumpAllocationNewBlock(currentBlock, activeRefPosition.nodeLocation, activeRefPosition);
                    dumpAllocationRegisterRecords();
                }
                else
                {
                    dumpRefPositionShort(activeRefPosition, currentBlock);
                }

                break;
            }

            case LsraDumpEvent.NEEDS_NEW_REG:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf($"Free  {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.ZERO_REF:
            {
                assert(interval is not null && interval.isLocalVar);
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("NoRef      ");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.FIXED_REG:
            case LsraDumpEvent.EXP_USE:
            case LsraDumpEvent.KEPT_ALLOCATION:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf($"Keep     {register.Name,-4} ");
                break;
            }

            case LsraDumpEvent.COPY_REG:
            {
                assert(interval is not null && interval.recentRefPosition is not null);
                dumpRefPositionShort(activeRefPosition, currentBlock);
                if (_allocationPassComplete || (registerScore is RegisterScore.NONE))
                {
                    jitprintf($"Copy     {register.Name,-4} ");
                }
                else
                {
                    jitprintf($"{getScoreName(registerScore),-5}(C) {register.Name,-4} ");
                }

                break;
            }

            case LsraDumpEvent.MOVE_REG:
            {
                assert(interval is not null && interval.recentRefPosition is not null);
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf($"Move     {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.ALLOC_REG:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                if (_allocationPassComplete || (registerScore is RegisterScore.NONE))
                {
                    jitprintf($"Alloc    {register.Name,-4} ");
                }
                else
                {
                    jitprintf($"{getScoreName(registerScore),-5}(A) {register.Name,-4} ");
                }

                break;
            }

            case LsraDumpEvent.REUSE_REG:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                if (_allocationPassComplete || (registerScore is RegisterScore.NONE))
                {
                    jitprintf($"Reuse    {register.Name,-4} ");
                }
                else
                {
                    jitprintf($"{getScoreName(registerScore),-5}(R) {register.Name,-4} ");
                }

                break;
            }

            case LsraDumpEvent.NO_ENTRY_REG_ALLOCATED:
            {
                assert(interval is not null && interval.isLocalVar);
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("LoRef         ");
                break;
            }

            case LsraDumpEvent.NO_REG_ALLOCATED:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("NoReg         ");
                break;
            }

            case LsraDumpEvent.RELOAD:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf($"ReLod    {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }

            case LsraDumpEvent.SPECIAL_PUTARG:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf($"PtArg    {register.Name,-4} ");
                break;
            }

            case LsraDumpEvent.UPPER_VECTOR_SAVE:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf($"UVSav    {register.Name,-4} ");
                break;
            }

            case LsraDumpEvent.UPPER_VECTOR_RESTORE:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf($"UVRes    {register.Name,-4} ");
                break;
            }

            case LsraDumpEvent.KILL_REGS:
            {
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("None     ");
                _compiler.dumpRegMask(registerMask);
                jitprintf("\n");
                dumpRefPositionShort(activeRefPosition, currentBlock);
                jitprintf("              ");
                break;
            }

            case LsraDumpEvent.SPILL_EXTENDED_LIFETIME:
            case LsraDumpEvent.END_BB:
            case LsraDumpEvent.FREE_REGS:
            case LsraDumpEvent.INCREMENT_RANGE_END:
            case LsraDumpEvent.LAST_USE:
            case LsraDumpEvent.LAST_USE_DELAYED:
            {
                break;
            }

            default:
            {
                jitprintf($"?????    {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }
        }
    }

    private void initializeAllocationDumpFormat()
    {
        if (_allocationDumpFormatInitialized)
        {
            return;
        }

#if TARGET_AMD64
#if UNIX_AMD64_ABI
        var smallIntegerSet = SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_R12 | SRBM_R13;
#else
        var smallIntegerSet = SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_RSI | SRBM_RDI;
#endif
        var smallFloatSet = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM6 | SRBM_XMM7;
        var argumentRegisters = SRBM_ARG_REGS;
#elif TARGET_ARM
        var smallIntegerSet = SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3 | SRBM_R4 | SRBM_R5;
        var smallFloatSet = SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F16 | SRBM_F17;
        var argumentRegisters = SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3;
#elif TARGET_ARM64
        var smallIntegerSet = SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R19 | SRBM_R20;
        var smallFloatSet = SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V8 | SRBM_V9;
        var argumentRegisters = SRBM_ARG_REGS;
#elif TARGET_X86
        var smallIntegerSet = SRBM_EAX | SRBM_ECX | SRBM_EDI;
        var smallFloatSet = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM6 | SRBM_XMM7;
        var argumentRegisters = SRBM_ECX | SRBM_EDX;
#elif TARGET_LOONGARCH64
        var smallIntegerSet = SRBM_T1 | SRBM_T3 | SRBM_A0 | SRBM_A1 | SRBM_T0;
        var smallFloatSet = SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F8 | SRBM_F9;
        var argumentRegisters = SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7;
#elif TARGET_RISCV64
        var smallIntegerSet = SRBM_T1 | SRBM_T3 | SRBM_A0 | SRBM_A1 | SRBM_T0;
        var smallFloatSet = SRBM_FT0 | SRBM_FT1 | SRBM_FT2 | SRBM_FS0 | SRBM_FS1;
        var argumentRegisters = SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7;
#endif
#if TARGET_AMD64 || TARGET_ARM || TARGET_ARM64 || TARGET_X86 || TARGET_LOONGARCH64 || TARGET_RISCV64
        _allocationDumpRegisters = new regMaskTP(smallIntegerSet | smallFloatSet | argumentRegisters);
#else
        NYI("LSRA allocation table initial register set on this target");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("LSRA allocation table initial register set on this target.");
#endif

        jitprintf("\n\nAllocating Registers\n--------------------\n");
        dumpRegRecordHeader();
        _allocationDumpFormatInitialized = true;
        dumpAllocationIndentedText("");
    }

    private void dumpRegRecordHeader()
    {
        jitprintf(
            "The following table has one or more rows for each RefPosition that is handled during allocation.\n" +
            "The columns are: (1) Loc: LSRA location, (2) RP#: RefPosition number, (3) Name, (4) Type (e.g. Def, Use,\n" +
            "Fixd, Parm, DDef (Dummy Def), ExpU (Exposed Use), Kill) followed by a '*' if it is a last use, and a 'D'\n" +
            "if it is delayRegFree, (5) Action taken during allocation. Some actions include (a) Alloc a new register,\n" +
            "(b) Keep an existing register, (c) Spill a register, (d) ReLod (Reload) a register. If an ALL-CAPS name\n" +
            "such as COVRS is displayed, it is a score name from lsra_score.h, with a trailing '(A)' indicating alloc,\n" +
            "'(C)' indicating copy, and '(R)' indicating re-use. See dumpLsraAllocationEvent() for details.\n" +
            "The subsequent columns show the Interval occupying each register, if any, followed by 'a' if it is\n" +
            "active, 'p' if it is a large vector that has been partially spilled, and 'i' if it is inactive.\n" +
            "Columns are only printed up to the last modified register, which may increase during allocation,\n" +
            "in which case additional columns will appear. Registers which are not marked modified have ---- in\n" +
            "their column.\n\n");

        var intervalNumberWidth = getDecimalWidth((uint)intervals.Count);
        _allocationDumpRegColumnWidth = Math.Max(4, intervalNumberWidth + 2);
        _maxNodeLocation = _maxNodeLocation == 0 ? 1 : _maxNodeLocation;
        assert(_maxNodeLocation >= 1);
        assert(refPositions.Count >= 1);
        _allocationDumpNodeLocationWidth = getDecimalWidth(_maxNodeLocation);
        _allocationDumpRefPositionWidth = getDecimalWidth((uint)refPositions.Count);
        _allocationDumpShortRefPositionWidth =
            _allocationDumpNodeLocationWidth + 2 + _allocationDumpRefPositionWidth + 1 +
            _allocationDumpRegColumnWidth + 1 + 7;
        _allocationDumpTableIndent = 9 + _allocationDumpShortRefPositionWidth + 14;
        _allocationDumpBbNumWidth = getDecimalWidth((uint)Math.Max(1, _compiler.fgBBNumMax));
        _allocationDumpPredBbNumWidth = _allocationDumpTableIndent -
            (_allocationDumpNodeLocationWidth + 2 + _allocationDumpRefPositionWidth + 1) -
            _allocationDumpBbNumWidth - 9 - 9;
        _allocationDumpLine = _compiler.ShouldDumpAsciiTrees ? "-" : "─";
        _allocationDumpMiddleBox = _compiler.ShouldDumpAsciiTrees ? "+" : "┼";
        _allocationDumpRightBox = _compiler.ShouldDumpAsciiTrees ? "+" : "┤";
        _allocationDumpColumnSeparator = _compiler.ShouldDumpAsciiTrees ? "|" : "│";
        _lastAllocationDumpRegisters = RBM_NONE;
        dumpAllocationRegisterTitleIfNeeded();
    }

    private static int getDecimalWidth(uint value)
    {
        var width = 1;
        while (value >= 10)
        {
            value /= 10;
            width++;
        }

        return width;
    }

    private void dumpAllocationRegisterTitleIfNeeded()
    {
        if ((_lastAllocationDumpRegisters == _allocationDumpRegisters) && (_allocationDumpRowsSinceTitle <= 50))
        {
            return;
        }

        _allocationDumpLastUsedRegNumIndex = 0;
#if HAS_MORE_THAN_64_REGISTERS
        var lastRegister = _compiler.compFloatingPointUsed ? REG_MASK_LAST : _compiler.REG_INT_LAST;
#else
        var lastRegister = _compiler.compFloatingPointUsed ? REG_FP_LAST : _compiler.REG_INT_LAST;
#endif
        for (var registerIndex = 0; registerIndex <= (int)lastRegister; registerIndex++)
        {
            if (_allocationDumpRegisters.IsSet((regNumber)registerIndex))
            {
                _allocationDumpLastUsedRegNumIndex = registerIndex;
            }
        }

        dumpAllocationRegisterTitle();
        _lastAllocationDumpRegisters = _allocationDumpRegisters;
    }

    private void dumpAllocationRegisterTitle()
    {
        dumpAllocationRegisterTitleLines();
        jitprintf(
            "TreeID ".PadRight(9) +
            padOrTruncate("Loc ", _allocationDumpNodeLocationWidth + 1) +
            padOrTruncate("RP# ", _allocationDumpRefPositionWidth + 2) +
            padOrTruncate("Name ", _allocationDumpRegColumnWidth + 1) +
            "Type  Action    Reg  ");

        for (var registerIndex = 0; registerIndex <= _allocationDumpLastUsedRegNumIndex; registerIndex++)
        {
            var register = (regNumber)registerIndex;
            if (_allocationDumpRegisters.IsSet(register))
            {
                jitprintf(_allocationDumpColumnSeparator + register.Name.PadRight(_allocationDumpRegColumnWidth));
            }
        }

        jitprintf(_allocationDumpColumnSeparator + "\n");
        _allocationDumpRowsSinceTitle = 0;
        dumpAllocationRegisterTitleLines();
    }

    private static string padOrTruncate(string text, int width) =>
        text.Length > width ? text[..width] : text.PadRight(width);

    private void dumpAllocationRegisterTitleLines()
    {
        for (var index = 0; index < _allocationDumpTableIndent; index++)
        {
            jitprintf(_allocationDumpLine);
        }

        for (var registerIndex = 0; registerIndex <= _allocationDumpLastUsedRegNumIndex; registerIndex++)
        {
            if (_allocationDumpRegisters.IsSet((regNumber)registerIndex))
            {
                jitprintf(_allocationDumpMiddleBox);
                for (var column = 0; column < _allocationDumpRegColumnWidth; column++)
                {
                    jitprintf(_allocationDumpLine);
                }
            }
        }

        jitprintf(_allocationDumpRightBox + "\n");
    }

    private void dumpAllocationRegisterRecords()
    {
        for (var registerIndex = 0; registerIndex <= _allocationDumpLastUsedRegNumIndex; registerIndex++)
        {
            var register = (regNumber)registerIndex;
            if (!_allocationDumpRegisters.IsSet(register))
            {
                continue;
            }

            jitprintf(_allocationDumpColumnSeparator);
            var interval = getRegisterRecord(register).assignedInterval;
            if (interval is not null)
            {
                jitprintf(getAllocationIntervalName(interval));
                var activeCharacter = interval.isActive ? 'a' : 'i';
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                if (interval.isPartiallySpilled)
                {
                    activeCharacter = 'p';
                }
#endif
                jitprintf(activeCharacter.ToString());
            }
            else if (_regsBusyUntilKill.IsSet(register))
            {
                if (s_allocationDumpEmptyColumnWidth != 0)
                {
                    jitprintf("Busy".PadRight(s_allocationDumpEmptyColumnWidth));
                }
            }
            else
            {
                // Native's static column format is initialized only by an empty cell, and survives later dumps.
                s_allocationDumpEmptyColumnWidth = _allocationDumpRegColumnWidth;
                jitprintf(new string(' ', _allocationDumpRegColumnWidth));
            }
        }

        jitprintf(_allocationDumpColumnSeparator + "\n");
        _allocationDumpRowsSinceTitle++;
    }

    private string getAllocationIntervalName(Interval interval)
    {
        char prefix;
        uint number;
        if (interval.isLocalVar)
        {
            prefix = 'V';
            number = interval.varNum;
        }
        else if (interval.IsUpperVector())
        {
            prefix = 'U';
            number = interval.relatedInterval
                ?.varNum ?? throw new FatalJitException("Upper-vector intervals must retain their related local.");
        }
        else if (interval.isConstant)
        {
            prefix = 'C';
            number = interval.intervalIndex;
        }
        else
        {
            prefix = 'I';
            number = interval.intervalIndex;
        }

        var prefixAndNumber = (interval.isLocalVar || interval.IsUpperVector()) && (number < 10)
            ? $"{prefix}0{number}"
            : $"{prefix}{number}";
        return prefixAndNumber.PadRight(_allocationDumpRegColumnWidth - 1);
    }

    private void dumpAllocationIndentedText(string text)
    {
        jitprintf(text.PadRight(_allocationDumpTableIndent));
    }

    private void dumpAllocationLocation(LsraLocation location, uint referenceNumber)
    {
        jitprintf(
            location.ToString(CultureInfo.InvariantCulture).PadLeft(_allocationDumpNodeLocationWidth) +
            ".#" +
            referenceNumber.ToString(CultureInfo.InvariantCulture).PadRight(_allocationDumpRefPositionWidth) +
            " ");
    }

    private void dumpAllocationNewBlock(BasicBlock? block, LsraLocation location, RefPosition reference)
    {
        if (!VERBOSE)
        {
            return;
        }

        if ((block is not null) && (block != _compiler.fgFirstBB))
        {
            dumpAllocationRegisterTitle();
        }

        if (reference.refType is RefType.RefTypeDummyDef)
        {
            dumpRefPositionShort(null);
            jitprintf("DDefs    " + new string(' ', _allocationDumpRegColumnWidth));
            return;
        }

        jitprintf(new string(' ', 9));
        dumpAllocationLocation(location, reference.rpNum);
        if (block is null)
        {
            jitprintf("END".PadRight(_allocationDumpRegColumnWidth) +
                new string(' ', 17 + _allocationDumpRegColumnWidth));
        }
        else if (_allocationDumpPredBbNumWidth < _allocationDumpBbNumWidth)
        {
            jitprintf("BB" + block.bbNum.ToString(CultureInfo.InvariantCulture)
                .PadRight(_allocationDumpShortRefPositionWidth - 2));
        }
        else
        {
            var blockInfo = _blockInfo;
            assert(blockInfo is not null);
            var predecessor = block == _compiler.fgFirstBB ? 0 : blockInfo[block.bbNum].predBBNum;
            jitprintf("BB" + block.bbNum.ToString(CultureInfo.InvariantCulture).PadRight(_allocationDumpBbNumWidth) +
                " PredBB" + predecessor.ToString(CultureInfo.InvariantCulture).PadRight(_allocationDumpPredBbNumWidth));
        }
    }

    private void dumpRefPositionShort(RefPosition? refPosition, BasicBlock? block = null)
    {
        if ((refPosition is null) || ReferenceEquals(refPosition, _lastAllocationDumpRefPosition))
        {
            jitprintf(new string(' ', 9 + _allocationDumpShortRefPositionWidth));
            return;
        }

        _lastAllocationDumpRefPosition = refPosition;
        if (refPosition.refType is RefType.RefTypeBB)
        {
            dumpAllocationNewBlock(block, refPosition.nodeLocation, refPosition);
            return;
        }

        if (refPosition.buildNode is not null)
        {
            jitprintf($"[{refPosition.buildNode.TreeId:D6}] ");
        }
        else
        {
            jitprintf(new string(' ', 9));
        }

        dumpAllocationLocation(refPosition.nodeLocation, refPosition.rpNum);

        if (refPosition.isIntervalRef())
        {
            jitprintf(getAllocationIntervalName(refPosition.getInterval()));
            var lastUse = refPosition.lastUse ? '*' : ' ';
            var delay = refPosition.lastUse && refPosition.delayRegFree ? 'D' : ' ';
            jitprintf($"  {getRefTypeShortName(refPosition.refType) ?? "(null)"}{lastUse}{delay} ");
        }
        else if (refPosition.IsPhysRegRef())
        {
            jitprintf(refPosition.getReg().regNum.Name.PadRight(_allocationDumpRegColumnWidth));
            jitprintf($" {getRefTypeShortName(refPosition.refType) ?? "(null)"}   ");
        }
        else
        {
            assert(refPosition.refType is RefType.RefTypeKill or RefType.RefTypeKillGCRefs);
            jitprintf(new string(' ', _allocationDumpRegColumnWidth));
            jitprintf($" {getRefTypeShortName(refPosition.refType) ?? "(null)"}   ");
        }
    }

    private static string? getRefTypeShortName(RefType refType) => refType switch
    {
        RefType.RefTypeInvalid => "Invl",
        RefType.RefTypeDef => "Def ",
        RefType.RefTypeUse => "Use ",
        RefType.RefTypeKill => "Kill",
        RefType.RefTypeBB => "BB  ",
        RefType.RefTypeFixedReg => "Fixd",
        RefType.RefTypeExpUse => "ExpU",
        RefType.RefTypeParamDef => "Parm",
        RefType.RefTypeDummyDef => "DDef",
        RefType.RefTypeZeroInit => "Zero",
        RefType.RefTypeUpperVectorSave => "UVSv",
        RefType.RefTypeUpperVectorRestore => "UVRs",
        RefType.RefTypeKillGCRefs => "KlGC",
        _ => null,
    };
#endif

#if TRACK_LSRA_STATS
    private static readonly string[] s_lsraStatNames =
    [
        "SpillCount",
        "CopyReg",
        "ResolutionMovs",
        "SplitEdges",
        "FREE",
        "CONST_AVAILABLE",
        "THIS_ASSIGNED",
        "COVERS",
        "OWN_PREFERENCE",
        "COVERS_RELATED",
        "RELATED_PREFERENCE",
        "CALLER_CALLEE",
        "UNASSIGNED",
        "COVERS_FULL",
        "BEST_FIT",
        "IS_PREV_REG",
        "REG_ORDER",
        "SPILL_COST",
        "FAR_NEXT_REF",
        "PREV_REG_OPT",
        "REG_NUM",
    ];

    private static string getStatName(uint stat)
    {
        assert(stat != (uint)LsraStat.COUNT);
        assert(stat < (uint)s_lsraStatNames.Length);

        return s_lsraStatNames[(int)stat];
    }

    private void updateLsraStat(LsraStat stat, uint blockNumber)
    {
        if (blockNumber > _bbNumMaxBeforeResolution)
        {
            return;
        }

        var blockInfo = _blockInfo
            ?? throw new FatalJitException("LSRA statistics require initialized block information.");
        var blockIndex = checked((int)blockNumber);
        if ((uint)blockIndex >= (uint)blockInfo.Length)
        {
            throw new FatalJitException("LSRA statistic block number is outside the block table.");
        }

        ref var block = ref blockInfo[blockIndex];
        var blockStats = block.stats ??= new uint[(int)LsraStat.COUNT];
        var statIndex = (int)stat;
        blockStats[statIndex] = unchecked(blockStats[statIndex] + 1);
    }

    internal uint getLsraStat(LsraStat stat, uint blockNumber)
    {
        if (blockNumber > _bbNumMaxBeforeResolution)
        {
            return 0;
        }

        var blockInfo = _blockInfo;
        if (blockInfo is null)
        {
            return 0;
        }

        var blockIndex = checked((int)blockNumber);
        if ((uint)blockIndex >= (uint)blockInfo.Length)
        {
            throw new FatalJitException("LSRA statistic block number is outside the block table.");
        }

        var blockStats = blockInfo[blockIndex].stats;
        return blockStats is null ? 0 : blockStats[(int)stat];
    }

    private void dumpLsraStatsCsvCore(StreamWriter streamWriter)
    {
        streamWriter.Flush();
        if (streamWriter.BaseStream.CanSeek && (streamWriter.BaseStream.Position == 0))
        {
            streamWriter.Write("\"Method Name\"");
            for (var statIndex = 0; statIndex < (int)LsraStat.COUNT; statIndex++)
            {
                streamWriter.Write(",\"");
                streamWriter.Write(getStatName((uint)statIndex));
                streamWriter.Write('"');
            }

            streamWriter.WriteLine(",\"PerfScore\"");
        }

        var totalStats = new uint[(int)LsraStat.COUNT];
        var blockInfo = _blockInfo
            ?? throw new FatalJitException("LSRA statistics require initialized block information.");

        void AddBlockStats(int blockNumber)
        {
            var blockStats = blockInfo[blockNumber].stats;
            if (blockStats is null)
            {
                return;
            }

            for (var statIndex = 0; statIndex < totalStats.Length; statIndex++)
            {
                totalStats[statIndex] = unchecked(totalStats[statIndex] + blockStats[statIndex]);
            }
        }

        AddBlockStats(0);
        foreach (var block in _compiler.Blocks)
        {
            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                continue;
            }

            AddBlockStats(block.bbNum);
        }

        streamWriter.Write('"');
#if DEBUG || LATE_DISASM || DUMP_FLOWGRAPHS || DUMP_GC_TABLES
        streamWriter.Write(_compiler.info.compFullName);
#else
        streamWriter.Write(_compiler.eeGetMethodFullName(_compiler.info.compCompHnd));
#endif
        streamWriter.Write('"');
        foreach (var totalStat in totalStats)
        {
            streamWriter.Write(',');
            streamWriter.Write(totalStat.ToString(CultureInfo.InvariantCulture));
        }

        streamWriter.Write(',');
        streamWriter.Write(_compiler.Metrics.PerfScore.ToString("F2", CultureInfo.InvariantCulture));
        streamWriter.WriteLine();
    }
#endif
}
