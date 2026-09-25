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
        FREE_REGS,
        LAST_USE,
        LAST_USE_DELAYED,
        DEFUSE_CONFLICT,
        DEFUSE_DEF_IN_FIXED_USE,
        DEFUSE_DEF_IN_USE,
        DEFUSE_ANY_DEF,
        DEFUSE_COPY,
    }

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
        LsraDumpEvent dumpEvent, Interval? interval, RefPosition? activeRefPosition, regNumber register = REG_NA)
    {
        if (!VERBOSE)
        {
            return;
        }

        initializeAllocationDumpFormat();
        if ((interval is not null) && (register is not REG_NA and not REG_STK))
        {
            _allocationDumpRegisters |= regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
            dumpAllocationRegisterTitleIfNeeded();
        }

        switch (dumpEvent)
        {
            case LsraDumpEvent.DEFUSE_CONFLICT:
            {
                dumpRefPositionShort(activeRefPosition);
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
                else if ((interval.firstRefPosition is not null) && (interval.firstRefPosition.multiRegIdx != 0))
                {
                    dumpAllocationIndentedText("    (multiReg)");
                    dumpAllocationRegisterRecords();
                }

                break;
            }

            case LsraDumpEvent.FREE_REGS:
            case LsraDumpEvent.LAST_USE:
            case LsraDumpEvent.LAST_USE_DELAYED:
            {
                break;
            }

            default:
            {
                throw new FatalJitException($"Unsupported LSRA allocation dump event: {dumpEvent}.");
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
        var smallIntegerSet = SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_RSI | SRBM_RDI;
        var smallFloatSet = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM6 | SRBM_XMM7;
        _allocationDumpRegisters = new regMaskTP(smallIntegerSet | smallFloatSet | SRBM_ARG_REGS);
#else
        NYI("LSRA allocation table formatting outside AMD64");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("LSRA allocation table formatting outside AMD64.");
#endif

        var intervalNumberWidth = getDecimalWidth((uint)intervals.Count);
        _allocationDumpRegColumnWidth = Math.Max(4, intervalNumberWidth + 2);
        var maximumLocation = Math.Max(1, _maxNodeLocation);
        var referenceCount = (uint)Math.Max(1, refPositions.Count);
        _allocationDumpNodeLocationWidth = getDecimalWidth(maximumLocation);
        _allocationDumpRefPositionWidth = getDecimalWidth(referenceCount);
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
        _allocationDumpFormatInitialized = true;
        jitprintf("\n\nAllocating Registers\n--------------------\n");
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
        dumpAllocationRegisterTitleIfNeeded();
        dumpAllocationIndentedText("");
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
                jitprintf("Busy".PadRight(_allocationDumpRegColumnWidth));
            }
            else
            {
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
            jitprintf($"  {getRefTypeShortName(refPosition.refType)}{lastUse}{delay} ");
        }
        else if (refPosition.IsPhysRegRef())
        {
            jitprintf(refPosition.getReg().regNum.Name.PadRight(_allocationDumpRegColumnWidth));
            jitprintf($" {getRefTypeShortName(refPosition.refType)}   ");
        }
        else
        {
            jitprintf(new string(' ', _allocationDumpRegColumnWidth));
            jitprintf($" {getRefTypeShortName(refPosition.refType)}   ");
        }
    }

    private static string getRefTypeShortName(RefType refType) => refType switch
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
        _ => throw new FatalJitException($"Unsupported LSRA reference type: {refType}."),
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
        if (!streamWriter.BaseStream.CanSeek || (streamWriter.BaseStream.Position == 0))
        {
            streamWriter.Write("\"Method Name\"");
            foreach (var statName in s_lsraStatNames)
            {
                streamWriter.Write(",\"");
                streamWriter.Write(statName);
                streamWriter.Write('"');
            }

            streamWriter.WriteLine(",\"PerfScore\"");
        }

        var totalStats = new uint[(int)LsraStat.COUNT];
        var blockInfo = _blockInfo
            ?? throw new FatalJitException("LSRA statistics require initialized block information.");
        var blockCount = checked((int)(_bbNumMaxBeforeResolution + 1));
        if (blockCount > blockInfo.Length)
        {
            throw new FatalJitException("LSRA statistics do not cover all pre-resolution blocks.");
        }

        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var blockStats = blockInfo[blockIndex].stats;
            if (blockStats is null)
            {
                continue;
            }

            for (var statIndex = 0; statIndex < totalStats.Length; statIndex++)
            {
                totalStats[statIndex] = unchecked(totalStats[statIndex] + blockStats[statIndex]);
            }
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
