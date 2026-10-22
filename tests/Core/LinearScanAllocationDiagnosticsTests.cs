// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanAllocationDiagnosticsTests
{
    [SetUp]
    public static void RequireDebugBuild()
    {
        Assume.That(TestConfiguration.IsDebug, "This fixture requires Debug diagnostics.");
    }

    private static int s_assertionCount;

    [Test]
    public static void CompactAllocationTableUsesNativeHeaderAndInitialRecordIndent()
    {
        WithCompiler((compiler, codeGen) => {
            compiler.compFloatingPointUsed = false;
            var allocator = CreateAllocator(compiler);
            _ = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
            SetMaxNodeLocation(allocator, 10);
            TestConfiguration.EnableVerbose(compiler);
            var separator = TestConfiguration.ShouldDumpAsciiTrees(compiler) ? "|" : "│";

            var output = Capture(() => {
                InitializeAllocationDumpFormat(allocator);
                DumpAllocationRegisterRecords(allocator);
            });

            var expectedColumns = new StringBuilder();
            var expectedRegisterCount = 0;
            for (var registerIndex = (int)REG_INT_FIRST; registerIndex <= (int)codeGen.REG_INT_LAST; registerIndex++)
            {
                var register = (regNumber)registerIndex;
                if (!AllocationDumpRegisters(allocator).IsSet(register))
                {
                    continue;
                }

                _ = expectedColumns.Append(separator).Append(register.Name.PadRight(4));
                expectedRegisterCount++;
            }

            Assert.That(output, Does.Contain(
                "TreeID   LocRP#Name Type  Action    Reg  " +
                $"{expectedColumns}{separator}"));
            Assert.That(output, Does.Contain(
                new string(' ', 41) + string.Concat(
                    System.Linq.Enumerable.Repeat(separator + "    ", expectedRegisterCount)) + separator));
        });
    }

    [Test]
    public static void FloatingPointAllocationTitleDoesNotIncludeUnmodifiedArgumentRegisters()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = CreateAllocator(compiler);
            _ = NewInterval(allocator, TYP_DOUBLE);
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
            TestConfiguration.EnableVerbose(compiler);
            var separator = TestConfiguration.ShouldDumpAsciiTrees(compiler) ? "|" : "│";

            var output = Capture(() => {
                InitializeAllocationDumpFormat(allocator);
                SetAllocationDumpRegisters(allocator,
                    AllocationDumpRegisters(allocator) | new regMaskTP(SRBM_X64_XMM3));
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
            var reference = allocator.newRefPosition(interval, 1, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            TestConfiguration.EnableVerbose(compiler);
            var separator = TestConfiguration.ShouldDumpAsciiTrees(compiler) ? "|" : "│";

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

    [Test]
    public static void ShortReferenceAssertsWhenAnUnassociatedKindIsNotAKill()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = CreateAllocator(compiler);
            _ = allocator.newRefPosition(null, 1, RefType.RefTypeKill, null, SRBM_NONE);
            var reference = new RefPosition(0, 1, null, RefType.RefTypeInvalid);
            TestConfiguration.EnableVerbose(compiler);
            _ = Capture(() => InitializeAllocationDumpFormat(allocator));

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.doAssert = &RecordAssertion;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            using var tls = new JitTls(&jitInfo);
            JitTls.Compiler = compiler;
            s_assertionCount = 0;

            _ = Capture(() => DumpRefPositionShort(allocator, reference));

            Assert.That(s_assertionCount, Is.EqualTo(1));
        });
    }

    [Test]
    public static void UpperVectorRegisterRecordsUseTwoDigitSmallLocalNames()
    {
        Assume.That(Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE,
            "This test requires partial SIMD callee-save support.");

        WithCompiler((compiler, codeGen) => {
            var allocator = CreateAllocator(compiler);
            var local = NewInterval(allocator, TYP_SIMD16);
            local.varNum = 3;
            var upperVector = NewInterval(allocator, TYP_SIMD16);
            upperVector.isUpperVector = true;
            upperVector.relatedInterval = local;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
            _ = Capture(() => InitializeAllocationDumpFormat(allocator));

            Assert.That(GetAllocationIntervalName(allocator, upperVector), Is.EqualTo("U03"));
        });
    }

    [Test]
    public static void MinimalRegisterOrderSelectionIncrementsItsBlockStatistic()
    {
        TestConfiguration.WithLsraStatistics(() => WithCompiler((compiler, _) => {
                var allocator = CreateAllocator(compiler);
                var interval = NewInterval(allocator, TYP_INT);
                var tree = compiler.gtNewIconNode(TYP_INT, 1);
                var definition = allocator.newRefPosition(
                    interval, 10, RefType.RefTypeDef, tree, SRBM_X64_RAX | SRBM_X64_RBX);

                var selected = allocator.selectMinimal(interval, definition);

                Assert.That(selected, Is.Not.EqualTo(SRBM_NONE));
                Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_ORDER, 0), Is.EqualTo(1u));
                Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_NUM, 0), Is.Zero);
            }));
    }

    [Test]
    public static void MinimalRegisterNumberFallbackIncrementsItsBlockStatistic()
    {
        TestConfiguration.WithLsraStatistics(() => WithCompiler((compiler, codeGen) => {
                codeGen.RegSet.rsMaskResvd = new regMaskTP(SRBM_X64_RBX | SRBM_X64_RCX);
                var allocator = CreateAllocator(compiler);
                var interval = NewInterval(allocator, TYP_INT);
                var tree = compiler.gtNewIconNode(TYP_INT, 1);
                var definition = allocator.newRefPosition(
                    interval, 10, RefType.RefTypeDef, tree, SRBM_X64_RAX | SRBM_X64_RBX | SRBM_X64_RCX);
                BusyUntilKill(allocator) = new regMaskTP(SRBM_X64_RAX);

                var selected = allocator.selectMinimal(interval, definition);

                Assert.That(selected, Is.EqualTo(SRBM_X64_RCX));
                Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_ORDER, 0), Is.Zero);
                Assert.That(allocator.getLsraStat(LsraStat.STAT_REG_NUM, 0), Is.EqualTo(1u));
            }));
    }

    [Test]
    public static void CsvOutputReportsNativeSelectorStatisticNamesAndCounts()
    {
        TestConfiguration.WithLsraStatistics(() => WithCompiler((compiler, codeGen) => {
                compiler.info = new Compiler.Info { compFullName = "LSRAStatsTest" };
                var allocator = CreateAllocator(compiler);
                var interval = NewInterval(allocator, TYP_INT);
                var tree = compiler.gtNewIconNode(TYP_INT, 1);
                var definition = allocator.newRefPosition(
                    interval, 10, RefType.RefTypeDef, tree, SRBM_X64_RAX | SRBM_X64_RBX);
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
            }));
    }

    private static LinearScan CreateAllocator(Compiler compiler)
    {
        var allocator = new LinearScan(compiler);
        var blockInfo = new LsraBlockInfo[1];
        BlockInfo(allocator) = blockInfo;
        var blockStats = Configuration.TRACK_LSRA_STATS ? new uint[]?[blockInfo.Length] : null;
        BlockStats(allocator) = blockStats;
        blockStats?[0] = new uint[(int)LsraStat.COUNT];
        for (var index = 0; index < (int)regNumber.ACTUAL_REG_X64_COUNT; index++)
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getAllocationIntervalName")]
    private static extern string GetAllocationIntervalName(LinearScan allocator, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get__allocationDumpRegisters")]
    private static extern regMaskTP AllocationDumpRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set__allocationDumpRegisters")]
    private static extern void SetAllocationDumpRegisters(LinearScan allocator, regMaskTP value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get__maxNodeLocation")]
    private static extern uint MaxNodeLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set__maxNodeLocation")]
    private static extern void SetMaxNodeLocation(LinearScan allocator, uint value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockStats")]
    private static extern ref uint[]?[]? BlockStats(LinearScan allocator);

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertionCount++;
        return 0;
    }

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        CompilerLastIntRegister(compiler) = regNumber.REG_X64_R15;
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
