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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

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
}
#endif
