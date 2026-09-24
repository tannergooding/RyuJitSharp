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

internal static unsafe class LinearScanAllocationEventTests
{
    [Test]
    public static void ConflictingDefUseEventsPreserveNativeOrderAndText()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = CreateAllocator(compiler);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var priorInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(priorInterval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            _ = allocator.newRefPosition(priorInterval, 9, RefType.RefTypeUse, tree, SRBM_RAX);
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = priorInterval;

            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RBX);
            _ = allocator.newRefPosition(interval, 12, RefType.RefTypeUse, tree, SRBM_RAX);
            UpdateNextFixedReference(allocator, regNumber.REG_RAX);
            compiler.verbose = true;

            var output = Capture(() => {
                var selected = allocator.selectMinimal(interval, definition);
                Assert.That(selected, Is.EqualTo(SRBM_RAX));
            });

            var conflict = output.IndexOf("DUconflict    ", StringComparison.Ordinal);
            var fixedUse = output.IndexOf("  Define in fixed use reg", StringComparison.Ordinal);
            Assert.That(conflict, Is.GreaterThanOrEqualTo(0));
            Assert.That(fixedUse, Is.GreaterThan(conflict));
        });
    }

    [Test]
    public static void AllocationEventsRemainGatedByVerboseConfiguration()
    {
        WithCompiler((compiler, codeGen) => {
            var allocator = CreateAllocator(compiler);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            compiler.verbose = false;

            var output = Capture(() => _ = allocator.selectMinimal(interval, definition));

            Assert.That(output, Is.Empty);
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "updateNextFixedRef")]
    private static extern void UpdateNextFixedRef(
        LinearScan allocator, RegRecord regRecord, RefPosition? nextRefPosition, RefPosition? nextKill);

    private static void UpdateNextFixedReference(LinearScan allocator, regNumber register)
    {
        var record = allocator.physRegs[(int)register];
        var nextReference = record.lastRefPosition
            ?? throw new InvalidOperationException("The fixed register must have a linked reference.");
        UpdateNextFixedRef(allocator, record, nextReference, null);
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
