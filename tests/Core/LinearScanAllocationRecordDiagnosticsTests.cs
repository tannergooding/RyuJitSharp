// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG && TARGET_AMD64
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanAllocationRecordDiagnosticsTests
{
    [TestCase("DEFUSE_CONFLICT", "DUconflict    ", true)]
    [TestCase("NEEDS_NEW_REG", "Free  rax  ", true)]
    [TestCase("ZERO_REF", "NoRef      ", true)]
    [TestCase("FIXED_REG", "Keep     rax  ", false)]
    [TestCase("EXP_USE", "Keep     rax  ", false)]
    [TestCase("KEPT_ALLOCATION", "Keep     rax  ", false)]
    [TestCase("COPY_REG", "Copy     rax  ", false)]
    [TestCase("MOVE_REG", "Move     rax  ", true)]
    [TestCase("ALLOC_REG", "Alloc    rax  ", false)]
    [TestCase("REUSE_REG", "Reuse    rax  ", false)]
    [TestCase("NO_ENTRY_REG_ALLOCATED", "LoRef         ", false)]
    [TestCase("NO_REG_ALLOCATED", "NoReg         ", false)]
    [TestCase("RELOAD", "ReLod    rax  ", true)]
    [TestCase("SPECIAL_PUTARG", "PtArg    rax  ", false)]
    [TestCase("UPPER_VECTOR_SAVE", "UVSav    rax  ", false)]
    [TestCase("UPPER_VECTOR_RESTORE", "UVRes    rax  ", false)]
    [TestCase("DONE_KILL_GC_REFS", "Done          ", false)]
    [TestCase("NO_GC_KILLS", "None          ", false)]
    public static void EventsPreserveExactActionsAndRowCompletion(string name, string action, bool completesRow)
    {
        WithAllocator((_, allocator, interval, reference) => {
            interval.isLocalVar = name is "ZERO_REF" or "NO_ENTRY_REG_ALLOCATED";
            interval.varNum = 0;
            interval.recentRefPosition = reference;
            var prefix = Capture(() => DumpReference(allocator, reference));
            LastReference(allocator) = null;

            var output = Capture(() => DumpEvent(allocator, name, interval, reference));

            Assert.That(output, Is.EqualTo(prefix + action + (completesRow ? EmptyRow(allocator) : "")));
            Assert.That(RowsSinceTitle(allocator), Is.EqualTo(completesRow ? 1 : 0));
        });
    }

    [TestCase("DEFUSE_DEF_IN_FIXED_USE", "  Define in fixed use reg")]
    [TestCase("DEFUSE_DEF_IN_USE", "  Define in candidate use reg")]
    [TestCase("DEFUSE_ANY_DEF", "  Define in any reg")]
    public static void ConflictDetailsUseTheWholeTableIndent(string name, string text)
    {
        WithAllocator((_, allocator, interval, reference) => {
            var output = Capture(() => DumpEvent(allocator, name, interval, reference));

            Assert.That(output, Is.EqualTo(text.PadRight(TableIndent(allocator)) + EmptyRow(allocator)));
        });
    }

    [TestCase(false, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 0)]
    public static void CopyDetailsPreserveNullAndMultiRegisterContinuation(bool nullInterval, int multiRegisterIndex)
    {
        WithAllocator((_, allocator, interval, reference) => {
            reference.multiRegIdx = (byte)multiRegisterIndex;
            var row = EmptyRow(allocator);
            var expected = "  Need a copy".PadRight(TableIndent(allocator)) + row;
            if (nullInterval || (multiRegisterIndex != 0))
            {
                expected += (nullInterval ? "    NULL interval" : "    (multiReg)")
                    .PadRight(TableIndent(allocator)) + row;
            }

            var output = Capture(() => DumpEvent(
                allocator, "DEFUSE_COPY", nullInterval ? null : interval, reference));

            Assert.That(output, Is.EqualTo(expected));
            Assert.That(RowsSinceTitle(allocator), Is.EqualTo(nullInterval || (multiRegisterIndex != 0) ? 2 : 1));
        });
    }

    [Test]
    public static void SpillUsesTheAssignedRegisterRatherThanTheRequestedRegister()
    {
        WithAllocator((_, allocator, interval, reference) => {
            interval.assignedReg = allocator.physRegs[(int)regNumber.REG_RCX];
            var prefix = Capture(() => DumpReference(allocator, reference));
            LastReference(allocator) = null;

            var output = Capture(() => DumpEvent(allocator, "SPILL", interval, reference));

            Assert.That(output, Is.EqualTo(prefix + "Spill    rcx  " + EmptyRow(allocator)));
        });
    }

    [TestCase(false, "none")]
    [TestCase(true, "none")]
    [TestCase(false, "block")]
    [TestCase(true, "block")]
    [TestCase(false, "definition")]
    [TestCase(true, "definition")]
    public static void RestoreLeavesNullAndBlockReferencesBlank(bool spilled, string referenceKind)
    {
        WithAllocator((_, allocator, interval, reference) => {
            if (referenceKind == "block")
            {
                reference.refType = RefType.RefTypeBB;
            }

            var prefix = referenceKind == "definition"
                ? Capture(() => DumpReference(allocator, reference))
                : new string(' ', TableIndent(allocator) - 14);
            LastReference(allocator) = null;
            var output = Capture(() => DumpEvent(
                allocator,
                spilled ? "RESTORE_PREVIOUS_INTERVAL_AFTER_SPILL" : "RESTORE_PREVIOUS_INTERVAL",
                interval,
                referenceKind == "none" ? null : reference));

            Assert.That(output, Is.EqualTo(prefix + (spilled ? "SRstr" : "Restr") +
                "    rax  " + EmptyRow(allocator)));
            Assert.That(LastReference(allocator), referenceKind == "definition"
                ? Is.SameAs(reference)
                : Is.Null);
        });
    }

    [TestCase("COPY_REG", "C", false)]
    [TestCase("ALLOC_REG", "A", false)]
    [TestCase("REUSE_REG", "R", false)]
    [TestCase("COPY_REG", "C", true)]
    [TestCase("ALLOC_REG", "A", true)]
    [TestCase("REUSE_REG", "R", true)]
    public static void ScoresAreUsedOnlyBeforeAllocationCompletes(string name, string suffix, bool completed)
    {
        WithAllocator((_, allocator, interval, reference) => {
            AllocationPassComplete(allocator) = completed;
            interval.recentRefPosition = reference;
            var prefix = Capture(() => DumpReference(allocator, reference));
            LastReference(allocator) = null;
            var expected = completed
                ? (name == "COPY_REG" ? "Copy " : name == "ALLOC_REG" ? "Alloc" : "Reuse") + "    rax  "
                : $"COVRS({suffix}) rax  ";

            var output = Capture(() => DumpEvent(allocator, name, interval, reference, score: "COVERS"));

            Assert.That(output, Is.EqualTo(prefix + expected));
            Assert.That(RowsSinceTitle(allocator), Is.Zero);
        });
    }

    [TestCase("SPILL_EXTENDED_LIFETIME")]
    [TestCase("END_BB")]
    [TestCase("FREE_REGS")]
    [TestCase("INCREMENT_RANGE_END")]
    [TestCase("LAST_USE")]
    [TestCase("LAST_USE_DELAYED")]
    public static void SilentEventsDoNotPrintOrAdvanceRecordRows(string name)
    {
        WithAllocator((_, allocator, interval, reference) => {
            var output = Capture(() => DumpEvent(allocator, name, interval, reference));

            Assert.That(output, Is.Empty);
            Assert.That(RowsSinceTitle(allocator), Is.Zero);
            Assert.That(LastReference(allocator), Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BlockStartCompletesOnlyTheDummyDefinitionRow(bool dummyDefinition)
    {
        WithAllocator((_, allocator, interval, reference) => {
            reference.refType = dummyDefinition ? RefType.RefTypeDummyDef : RefType.RefTypeBB;
            var prefix = dummyDefinition
                ? new string(' ', TableIndent(allocator) - 14) + "DDefs        "
                : Capture(() => DumpReference(allocator, reference));
            LastReference(allocator) = null;

            var output = Capture(() => DumpEvent(allocator, "START_BB", interval, reference));

            Assert.That(output, Is.EqualTo(prefix + (dummyDefinition ? EmptyRow(allocator) : "")));
            Assert.That(RowsSinceTitle(allocator), Is.EqualTo(dummyDefinition ? 1 : 0));
        });
    }

    [Test]
    public static void KillMaskPrecedesTheRepeatedBlankReferenceAndAction()
    {
        WithAllocator((compiler, allocator, interval, reference) => {
            var mask = new regMaskTP(SRBM_RAX | SRBM_RCX);
            var prefix = Capture(() => DumpReference(allocator, reference));
            var maskText = Capture(() => compiler.dumpRegMask(mask));
            LastReference(allocator) = null;

            var output = Capture(() => DumpEvent(allocator, "KILL_REGS", interval, reference, mask: mask));

            Assert.That(output, Is.EqualTo(prefix + "None     " + maskText + Environment.NewLine +
                new string(' ', TableIndent(allocator) - 14) + "              "));
            Assert.That(RowsSinceTitle(allocator), Is.Zero);
        });
    }

    [Test]
    public static void UnknownEventPrintsTheNativeFallbackRecordWithoutAReferencePrefix()
    {
        WithAllocator((_, allocator, _, _) => {
            var output = Capture(() => DumpEvent(allocator, "-1", null, null));

            Assert.That(output, Is.EqualTo("?????    rax  " + EmptyRow(allocator)));
            Assert.That(RowsSinceTitle(allocator), Is.EqualTo(1));
        });
    }

    [Test]
    public static void EveryEventReturnsBeforeInitializationOrDereferencesWhenNotVerbose()
    {
        WithAllocator((compiler, allocator, _, _) => {
            compiler.verbose = false;
            FormatInitialized(allocator) = false;
            MaxNodeLocation(allocator) = 0;
            var method = EventMethod();
            var eventType = method.GetParameters()[0].ParameterType;
            var output = Capture(() => {
                foreach (var name in Enum.GetNames(eventType))
                {
                    DumpEvent(allocator, name, null, null);
                }
                DumpEvent(allocator, "-1", null, null);
            });

            Assert.That(output, Is.Empty);
            Assert.That(FormatInitialized(allocator), Is.False);
            Assert.That(MaxNodeLocation(allocator), Is.Zero);
            Assert.That(RowsSinceTitle(allocator), Is.Zero);
        });
    }

    [Test]
    public static void EventValuesRetainTheNativeDeclarationOrder()
    {
        string[] names = [
            "DEFUSE_CONFLICT", "DEFUSE_DEF_IN_FIXED_USE", "DEFUSE_DEF_IN_USE", "DEFUSE_ANY_DEF", "DEFUSE_COPY",
            "SPILL", "SPILL_EXTENDED_LIFETIME", "RESTORE_PREVIOUS_INTERVAL",
            "RESTORE_PREVIOUS_INTERVAL_AFTER_SPILL", "DONE_KILL_GC_REFS", "NO_GC_KILLS", "START_BB", "END_BB",
            "FREE_REGS", "UPPER_VECTOR_SAVE", "UPPER_VECTOR_RESTORE", "KILL_REGS", "INCREMENT_RANGE_END",
            "LAST_USE", "LAST_USE_DELAYED", "NEEDS_NEW_REG", "FIXED_REG", "EXP_USE", "ZERO_REF",
            "NO_ENTRY_REG_ALLOCATED", "KEPT_ALLOCATION", "COPY_REG", "MOVE_REG", "ALLOC_REG",
            "NO_REG_ALLOCATED", "RELOAD", "SPECIAL_PUTARG", "REUSE_REG",
        ];
        var eventType = EventMethod().GetParameters()[0].ParameterType;
        Assert.That(Enum.GetNames(eventType), Is.EqualTo(names));
        for (var index = 0; index < names.Length; index++)
        {
            Assert.That(Convert.ToInt32(Enum.Parse(eventType, names[index]), CultureInfo.InvariantCulture),
                Is.EqualTo(index));
        }
    }

    [Test]
    public static void BusyFormatIsInitiallyEmptyAndRetainsTheLastEmptyCellWidthAcrossAllocators()
    {
        WithAllocator((compiler, allocator, _, _) => {
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";
            Assert.That(Capture(() => DumpRecords(allocator)),
                Is.EqualTo(separator + separator + Environment.NewLine));

            BusyUntilKill(allocator) = RBM_NONE;
            Assert.That(Capture(() => DumpRecords(allocator)), Is.EqualTo(EmptyRow(allocator)));
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);
            Assert.That(Capture(() => DumpRecords(allocator)),
                Is.EqualTo(separator + "Busy" + separator + Environment.NewLine));

            var widerAllocator = CreateAllocator(compiler, 1000, 1, 1);
            _ = Capture(() => DumpHeader(widerAllocator));
            FormatInitialized(widerAllocator) = true;
            BusyUntilKill(widerAllocator) = new regMaskTP(SRBM_RAX);
            Assert.That(ColumnWidth(widerAllocator), Is.EqualTo(6));
            Assert.That(Capture(() => DumpRecords(widerAllocator)),
                Is.EqualTo(separator + "Busy" + separator + Environment.NewLine));

            BusyUntilKill(widerAllocator) = RBM_NONE;
            Assert.That(Capture(() => DumpRecords(widerAllocator)), Is.EqualTo(EmptyRow(widerAllocator)));
            BusyUntilKill(widerAllocator) = new regMaskTP(SRBM_RAX);
            Assert.That(Capture(() => DumpRecords(widerAllocator)),
                Is.EqualTo(separator + "Busy  " + separator + Environment.NewLine));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AssignedIntervalsPreserveInactiveAndActiveStateCharacters(bool active)
    {
        WithAllocator((compiler, allocator, interval, _) => {
            interval.isActive = active;
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = interval;
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";

            Assert.That(Capture(() => DumpRecords(allocator)),
                Is.EqualTo($"{separator}I0 {(active ? 'a' : 'i')}{separator}{Environment.NewLine}"));
        });
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [Test]
    public static void PartialSpillOverridesTheActiveRecordCharacter()
    {
        WithAllocator((compiler, allocator, interval, _) => {
            interval.isActive = true;
            interval.isPartiallySpilled = true;
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = interval;
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";

            Assert.That(Capture(() => DumpRecords(allocator)),
                Is.EqualTo($"{separator}I0 p{separator}{Environment.NewLine}"));
        });
    }
#endif

    [TestCase(99, 9, 9u, 4, 1, 1)]
    [TestCase(100, 10, 10u, 5, 2, 2)]
    [TestCase(999, 99, 99u, 5, 2, 2)]
    [TestCase(1000, 100, 100u, 6, 3, 3)]
    public static void HeaderWidthsCrossTheActualDecimalBoundaries(
        int intervals, int references, uint location, int columnWidth, int locationWidth, int referenceWidth)
    {
        WithAllocator((compiler, _, _, _) => {
            var allocator = CreateAllocator(compiler, intervals, references, location);
            var output = Capture(() => DumpHeader(allocator));
            var indent = 34 + locationWidth + referenceWidth + columnWidth;
            var line = compiler.ShouldDumpAsciiTrees ? "-" : "─";
            var middle = compiler.ShouldDumpAsciiTrees ? "+" : "┼";
            var right = compiler.ShouldDumpAsciiTrees ? "+" : "┤";

            Assert.That(ColumnWidth(allocator), Is.EqualTo(columnWidth));
            Assert.That(NodeLocationWidth(allocator), Is.EqualTo(locationWidth));
            Assert.That(ReferenceWidth(allocator), Is.EqualTo(referenceWidth));
            Assert.That(TableIndent(allocator), Is.EqualTo(indent));
            Assert.That(output, Does.EndWith(
                string.Concat(System.Linq.Enumerable.Repeat(line, indent)) + middle +
                string.Concat(System.Linq.Enumerable.Repeat(line, columnWidth)) + right + Environment.NewLine));
        });
    }

    [Test]
    public static void HeaderNormalizesTheStoredZeroNodeLocation()
    {
        WithAllocator((_, allocator, _, _) => {
            MaxNodeLocation(allocator) = 0;

            _ = Capture(() => DumpHeader(allocator));

            Assert.That(MaxNodeLocation(allocator), Is.EqualTo(1u));
            Assert.That(NodeLocationWidth(allocator), Is.EqualTo(1));
        });
    }

    [TestCase(50, false)]
    [TestCase(51, true)]
    public static void TitleRepetitionUsesAStrictGreaterThanFiftyRowBoundary(int rows, bool repeats)
    {
        WithAllocator((_, allocator, _, _) => {
            RowsSinceTitle(allocator) = rows;

            var output = Capture(() => DumpTitleIfNeeded(allocator));

            Assert.That(output.Contains("TreeID ", StringComparison.Ordinal), Is.EqualTo(repeats));
            Assert.That(RowsSinceTitle(allocator), Is.EqualTo(repeats ? 0 : rows));
        });
    }

    [Test]
    public static void ANewRegisterRepeatsTheTitleAndUpdatesTheLastDumpedSet()
    {
        WithAllocator((compiler, allocator, _, _) => {
            DumpRegisters(allocator) |= new regMaskTP(SRBM_RCX);
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";

            var output = Capture(() => DumpTitleIfNeeded(allocator));

            Assert.That(output, Does.Contain(separator + "rax " + separator + "rcx " + separator));
            Assert.That(LastDumpedRegisters(allocator), Is.EqualTo(DumpRegisters(allocator)));
            Assert.That(LastRegisterIndex(allocator), Is.EqualTo((int)regNumber.REG_RCX));
            Assert.That(RowsSinceTitle(allocator), Is.Zero);
        });
    }

#if HAS_MORE_THAN_64_REGISTERS && FEATURE_MASKED_HW_INTRINSICS
    [TestCase(false)]
    [TestCase(true)]
    public static void TitleIncludesFloatingAndMaskBanksOnlyWhenFloatingPointWasUsed(bool floatingPointUsed)
    {
        WithAllocator((compiler, allocator, _, _) => {
            compiler.compFloatingPointUsed = floatingPointUsed;
            DumpRegisters(allocator) |= new regMaskTP(SRBM_XMM0) |
                regMaskTP.CreateFromRegNum(regNumber.REG_K1, genSingleTypeRegMask(regNumber.REG_K1));
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";

            var output = Capture(() => DumpTitleIfNeeded(allocator));

            Assert.That(output.Contains(separator + "mm0 " + separator, StringComparison.Ordinal),
                Is.EqualTo(floatingPointUsed));
            Assert.That(output.Contains(separator + "k1  " + separator, StringComparison.Ordinal),
                Is.EqualTo(floatingPointUsed));
            Assert.That(LastRegisterIndex(allocator), Is.EqualTo(
                floatingPointUsed ? (int)regNumber.REG_K1 : (int)regNumber.REG_RAX));
        });
    }
#endif

    [Test]
    public static void InitialRegisterSetUsesTheNativeTargetSpecificCalleeSavedRegisters()
    {
        WithAllocator((compiler, allocator, _, _) => {
            FormatInitialized(allocator) = false;
            compiler.compFloatingPointUsed = false;

            _ = Capture(() => InitializeFormat(allocator));

#if UNIX_AMD64_ABI
            var expected = SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_R12 | SRBM_R13;
            Assert.That(LastRegisterIndex(allocator), Is.EqualTo((int)regNumber.REG_R13));
#else
            var expected = SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_RSI | SRBM_RDI;
#endif
            expected |= SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM6 | SRBM_XMM7 | SRBM_ARG_REGS;
            Assert.That(DumpRegisters(allocator), Is.EqualTo(new regMaskTP(expected)));
        });
    }

    private static MethodInfo EventMethod() =>
        typeof(LinearScan).GetMethod("dumpLsraAllocationEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing allocation event dispatcher.");

    private static void DumpEvent(
        LinearScan allocator, string name, Interval? interval, RefPosition? reference,
        string score = "NONE", regMaskTP mask = default)
    {
        var method = EventMethod();
        var parameters = method.GetParameters();
        var dumpEvent = Enum.Parse(parameters[0].ParameterType, name);
        var registerScore = Enum.Parse(parameters[5].ParameterType, score);
        _ = method.Invoke(allocator, [dumpEvent, interval, reference, regNumber.REG_RAX, null, registerScore, mask]);
    }

    private static string EmptyRow(LinearScan allocator)
    {
        var separator = ColumnSeparator(allocator);
        return separator + new string(' ', ColumnWidth(allocator)) + separator + Environment.NewLine;
    }

    private static LinearScan CreateAllocator(Compiler compiler, int intervalCount, int referenceCount, uint location)
    {
        var previousVerbose = compiler.verbose;
        compiler.verbose = false;
        try
        {
            var allocator = new LinearScan(compiler);
            for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
            {
                allocator.physRegs[index].init((regNumber)index);
            }

            Interval? interval = null;
            for (var index = 0; index < intervalCount; index++)
            {
                interval = NewInterval(allocator, TYP_INT);
            }

            var referencedInterval = interval
                ?? throw new AssertionException("The fixture requires at least one interval.");
            for (var index = 0; index < referenceCount; index++)
            {
                _ = allocator.newRefPosition(
                    referencedInterval, location, RefType.RefTypeDef, null, SRBM_RAX | SRBM_RCX);
            }

            MaxNodeLocation(allocator) = location;
            DumpRegisters(allocator) = new regMaskTP(SRBM_RAX);
            return allocator;
        }
        finally
        {
            compiler.verbose = previousVerbose;
        }
    }

    private static void WithAllocator(Action<Compiler, LinearScan, Interval, RefPosition> action)
    {
        using var tls = new JitTls(null);
        var previousCompiler = JitTls.Compiler;
        var previousColumnWidth = EmptyColumnWidth(null);
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        compiler.fgBBNumMax = 1;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        CompilerLastIntRegister(compiler) = regNumber.REG_R15;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.CopyRegisterInfo();
            EmptyColumnWidth(null) = 0;
            var allocator = CreateAllocator(compiler, 1, 1, 1);
            _ = Capture(() => DumpHeader(allocator));
            FormatInitialized(allocator) = true;
            compiler.verbose = true;
            var interval = allocator.intervals[0];
            var reference = interval.firstRefPosition
                ?? throw new AssertionException("Missing fixture reference.");
            action(compiler, allocator, interval, reference);
        }
        finally
        {
            EmptyColumnWidth(null) = previousColumnWidth;
            JitTls.Compiler = previousCompiler;
        }
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpRegRecordHeader")]
    private static extern void DumpHeader(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "initializeAllocationDumpFormat")]
    private static extern void InitializeFormat(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpAllocationRegisterRecords")]
    private static extern void DumpRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpAllocationRegisterTitleIfNeeded")]
    private static extern void DumpTitleIfNeeded(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpRefPositionShort")]
    private static extern void DumpReference(LinearScan allocator, RefPosition? reference, BasicBlock? block = null);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_allocationDumpEmptyColumnWidth")]
    private static extern ref int EmptyColumnWidth(LinearScan? allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpFormatInitialized")]
    private static extern ref bool FormatInitialized(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpRegisters")]
    private static extern ref regMaskTP DumpRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lastAllocationDumpRegisters")]
    private static extern ref regMaskTP LastDumpedRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lastAllocationDumpRefPosition")]
    private static extern ref RefPosition? LastReference(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpRowsSinceTitle")]
    private static extern ref int RowsSinceTitle(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpRegColumnWidth")]
    private static extern ref int ColumnWidth(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpNodeLocationWidth")]
    private static extern ref int NodeLocationWidth(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpRefPositionWidth")]
    private static extern ref int ReferenceWidth(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpTableIndent")]
    private static extern ref int TableIndent(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpColumnSeparator")]
    private static extern ref string ColumnSeparator(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpLastUsedRegNumIndex")]
    private static extern ref int LastRegisterIndex(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationPassComplete")]
    private static extern ref bool AllocationPassComplete(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_maxNodeLocation")]
    private static extern ref uint MaxNodeLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber CompilerLastIntRegister(Compiler compiler);
}
#endif
