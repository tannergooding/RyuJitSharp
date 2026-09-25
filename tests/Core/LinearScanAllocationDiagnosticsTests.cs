// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanAllocationDiagnosticsTests
{
    [Test]
    public static void CompactAllocationTableUsesNativeHeaderAndInitialRecordIndent()
    {
        WithCompiler((compiler, codeGen) => {
            compiler.compFloatingPointUsed = false;
            var allocator = CreateAllocator(compiler);
            _ = NewInterval(allocator, TYP_INT);
            MaxNodeLocation(allocator) = 10;
            compiler.verbose = true;
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";

            var output = Capture(() => {
                InitializeAllocationDumpFormat(allocator);
                DumpAllocationRegisterRecords(allocator);
            });

            Assert.That(output, Does.Contain(
                "TreeID   LocRP#Name Type  Action    Reg  " +
                $"{separator}rax {separator}rcx {separator}rdx {separator}rbx {separator}rbp " +
                $"{separator}rsi {separator}rdi {separator}r8  {separator}r9  {separator}"));
            Assert.That(output, Does.Contain(
                new string(' ', 41) + string.Concat(
                    System.Linq.Enumerable.Repeat(separator + "    ", 9)) + separator));
        });
    }

    [Test]
    public static void FloatingPointAllocationTitleDoesNotIncludeUnmodifiedArgumentRegisters()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = CreateAllocator(compiler);
            _ = NewInterval(allocator, TYP_DOUBLE);
            compiler.verbose = true;
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";

            var output = Capture(() => {
                InitializeAllocationDumpFormat(allocator);
                AllocationDumpRegisters(allocator) |= new regMaskTP(SRBM_XMM3);
                DumpAllocationRegisterTitleIfNeeded(allocator);
            });

            Assert.That(output, Does.Contain(
                $"{separator}mm0 {separator}mm1 {separator}mm2 {separator}mm6 {separator}mm7 {separator}"));
            Assert.That(output, Does.Contain(
                $"{separator}mm0 {separator}mm1 {separator}mm2 {separator}mm3 {separator}mm6 {separator}mm7 {separator}"));
            Assert.That(output.Split($"{separator}mm3 ", StringSplitOptions.None), Has.Length.EqualTo(2));
        });
    }

    [Test]
    public static void RepeatedReferenceRowsLeaveTheLegendBlank()
    {
        WithCompiler((compiler, _) => {
            compiler.compFloatingPointUsed = false;
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var reference = allocator.newRefPosition(interval, 1, RefType.RefTypeDef, tree, SRBM_RAX);
            compiler.verbose = true;
            var separator = compiler.ShouldDumpAsciiTrees ? "|" : "│";

            var output = Capture(() => {
                InitializeAllocationDumpFormat(allocator);
                DumpAllocationRegisterRecords(allocator);
                DumpRefPositionShort(allocator, reference);
                DumpAllocationRegisterRecords(allocator);
                DumpRefPositionShort(allocator, reference);
                DumpAllocationRegisterRecords(allocator);
            });

            Assert.That(output, Does.Contain($"         1.#1 I0   Def    {separator}"));
            Assert.That(output, Does.Contain(
                new string(' ', 26) + separator + "    " + separator));
        });
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [Test]
    public static void UpperVectorRegisterRecordsUseTwoDigitSmallLocalNames()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = CreateAllocator(compiler);
            var local = NewInterval(allocator, TYP_SIMD16);
            local.varNum = 3;
            var upperVector = NewInterval(allocator, TYP_SIMD16);
            upperVector.isUpperVector = true;
            upperVector.relatedInterval = local;
            _ = Capture(() => InitializeAllocationDumpFormat(allocator));

            Assert.That(GetAllocationIntervalName(allocator, upperVector), Is.EqualTo("U03"));
        });
    }
#endif

    [Test]
    public static void MinimalRegisterOrderSelectionIncrementsItsBlockStatistic()
    {
        WithCompiler((compiler, _) => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(selected, Is.Not.EqualTo(SRBM_NONE));
            Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_ORDER, 0), Is.EqualTo(1u));
            Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_NUM, 0), Is.Zero);
        });
    }

    [Test]
    public static void MinimalRegisterNumberFallbackIncrementsItsBlockStatistic()
    {
        WithCompiler((compiler, codeGen) => {
            codeGen.RegSet.rsMaskResvd = new regMaskTP(SRBM_RBX | SRBM_RCX);
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX | SRBM_RCX);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(selected, Is.EqualTo(SRBM_RCX));
            Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_ORDER, 0), Is.Zero);
            Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_NUM, 0), Is.EqualTo(1u));
        });
    }

    [Test]
    public static void CsvOutputReportsNativeSelectorStatisticNamesAndCounts()
    {
        WithCompiler((compiler, codeGen) => {
            compiler.info = new Compiler.Info { compFullName = "LSRAStatsTest" };
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            _ = allocator.selectMinimal(interval, definition);

            using var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
            {
                allocator.dumpLsraStatsCsv(writer);
            }

            var csv = Encoding.UTF8.GetString(stream.ToArray());
            var expectedHeader =
                "\"Method Name\",\"SpillCount\",\"CopyReg\",\"ResolutionMovs\",\"SplitEdges\",\"FREE\"," +
                "\"CONST_AVAILABLE\",\"THIS_ASSIGNED\",\"COVERS\",\"OWN_PREFERENCE\",\"COVERS_RELATED\"," +
                "\"RELATED_PREFERENCE\",\"CALLER_CALLEE\",\"UNASSIGNED\",\"COVERS_FULL\",\"BEST_FIT\"," +
                "\"IS_PREV_REG\",\"REG_ORDER\",\"SPILL_COST\",\"FAR_NEXT_REF\",\"PREV_REG_OPT\",\"REG_NUM\"," +
                "\"PerfScore\"";
            Assert.That(csv, Does.StartWith(expectedHeader + Environment.NewLine));
            Assert.That(csv, Does.Contain("\"LSRAStatsTest\",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0.00"));
        });
    }

    private static LinearScan CreateAllocator(Compiler compiler)
    {
        var allocator = new LinearScan(compiler);
        BlockInfo(allocator) = [new LsraBlockInfo()];
        for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
        {
            var register = allocator.physRegs[index];
            register.init((regNumber)index);
            register.regOrder = (byte)index;
        }

        return allocator;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "initializeAllocationDumpFormat")]
    private static extern void InitializeAllocationDumpFormat(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpAllocationRegisterRecords")]
    private static extern void DumpAllocationRegisterRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpAllocationRegisterTitleIfNeeded")]
    private static extern void DumpAllocationRegisterTitleIfNeeded(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpRefPositionShort")]
    private static extern void DumpRefPositionShort(
        LinearScan allocator, RefPosition? reference, BasicBlock? block = null);

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getAllocationIntervalName")]
    private static extern string GetAllocationIntervalName(LinearScan allocator, Interval interval);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationDumpRegisters")]
    private static extern ref regMaskTP AllocationDumpRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_maxNodeLocation")]
    private static extern ref uint MaxNodeLocation(LinearScan allocator);

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

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        CompilerLastIntRegister(compiler) = regNumber.REG_R15;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        try
        {
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

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
