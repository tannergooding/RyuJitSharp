// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanSpillAccountingTests
{
    [TestCase(TYP_BYTE, TYP_INT)]
    [TestCase(TYP_INT, TYP_INT)]
    [TestCase(TYP_LONG, TYP_LONG)]
    [TestCase(TYP_FLOAT, TYP_FLOAT)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE)]
    [TestCase(TYP_REF, TYP_REF)]
    [TestCase(TYP_BYREF, TYP_BYREF)]
    [TestCase(TYP_SIMD12, TYP_SIMD16)]
    [TestCase(TYP_SIMD32, TYP_SIMD32)]
    [TestCase(TYP_SIMD64, TYP_SIMD64)]
    public static void ConcurrentSpillsUseNormalizedTypesAndRetainTheirPeak(var_types type, var_types normalized)
    {
        WithAllocator((compiler, allocator) => {
            var first = CreateDefinition(type);
            var second = CreateDefinition(type);
            InitMaxSpill(allocator);
            UpdateMaxSpill(allocator, first);
            UpdateMaxSpill(allocator, second);
            Assert.That(CurrentSpill(allocator)[(int)normalized], Is.EqualTo(2));
            Assert.That(MaxSpill(allocator)[(int)normalized], Is.EqualTo(2));

            var reload = CreateUse(first, reload: true);
            UpdateMaxSpill(allocator, reload);
            var optionalUse = CreateUse(second, reload: false);
            optionalUse.regOptional = true;
            UpdateMaxSpill(allocator, optionalUse);

            Assert.That(CurrentSpill(allocator)[(int)normalized], Is.Zero);
            Assert.That(MaxSpill(allocator)[(int)normalized], Is.EqualTo(2));
            RecordMaxSpill(allocator);
            ref var registers = ref compiler.codeGen!.RegSet;
            Assert.That(registers.tmpTotalSize, Is.EqualTo(normalized.Size * 2));
            Assert.That(registers.tmpGetTemp(type).tdTempType, Is.EqualTo(normalized));
            Assert.That(registers.tmpGetTemp(type).tdTempType, Is.EqualTo(normalized));

            InitMaxSpill(allocator);
            Assert.That(CurrentSpill(allocator), Is.All.Zero);
            Assert.That(MaxSpill(allocator), Is.All.Zero);
        });
    }

    [Test]
    public static void LocalHomesAndUnspilledReferencesDoNotAllocateTemporarySlots()
    {
        WithAllocator((compiler, allocator) => {
            var local = CreateDefinition(TYP_INT);
            local.getInterval().isLocalVar = true;
            var unspilled = CreateDefinition(TYP_LONG);
            unspilled.spillAfter = false;
            UpdateMaxSpill(allocator, local);
            UpdateMaxSpill(allocator, unspilled);
            RecordMaxSpill(allocator);

            Assert.That(MaxSpill(allocator), Is.All.Zero);
            Assert.That(compiler.codeGen!.RegSet.HasComputedTmpSize, Is.True);
            Assert.That(compiler.codeGen.RegSet.tmpTotalSize, Is.Zero);
        });
    }

    [Test]
    public static void ReloadTakesPrecedenceOverSpillAfterAndOptionalUsesWithRegisters()
    {
        WithAllocator((compiler, allocator) => {
            var definition = CreateDefinition(TYP_INT);
            UpdateMaxSpill(allocator, definition);
            var assignedOptionalUse = CreateUse(definition, reload: false);
            assignedOptionalUse.regOptional = true;
            assignedOptionalUse.registerAssignment = SRBM_EAX;
            UpdateMaxSpill(allocator, assignedOptionalUse);
            Assert.That(CurrentSpill(allocator)[(int)TYP_INT], Is.EqualTo(1));

            var reload = CreateUse(definition, reload: true);
            reload.spillAfter = true;
            UpdateMaxSpill(allocator, reload);
            Assert.That(CurrentSpill(allocator)[(int)TYP_INT], Is.Zero);
            Assert.That(MaxSpill(allocator)[(int)TYP_INT], Is.EqualTo(1));
        });
    }

    [Test]
    public static void MultiRegisterHardwareSpillsUseTheSelectedResultType()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_LONG, 3);
            var right = compiler.gtNewIconNode(TYP_LONG, 5);
            var tree = new GenTreeHWIntrinsic(TYP_STRUCT, NI_X86Base_X64_BigMul, TYP_LONG, 0, left, right);
            for (uint index = 0; index < 2; index++)
            {
                var interval = new Interval(TYP_LONG, SRBM_NONE);
                var definition = new RefPosition(1, 1, tree, RefType.RefTypeDef) { spillAfter = true };
                definition.setInterval(interval);
                definition.setMultiRegIdx(index);
                interval.firstRefPosition = definition;
                UpdateMaxSpill(allocator, definition);
                UpdateMaxSpill(allocator, CreateUse(definition, reload: true));
            }

            Assert.That(CurrentSpill(allocator)[(int)TYP_LONG], Is.Zero);
            Assert.That(MaxSpill(allocator)[(int)TYP_LONG], Is.EqualTo(1));
            RecordMaxSpill(allocator);
            Assert.That(compiler.codeGen!.RegSet.tmpTotalSize, Is.EqualTo(8));
        });
    }

    [Test]
    public static void SharedSizeSlotsKeepGcAndNonGcPeaksSeparate()
    {
        WithAllocator((compiler, allocator) => {
            UpdateMaxSpill(allocator, CreateDefinition(TYP_REF));
            UpdateMaxSpill(allocator, CreateDefinition(TYP_LONG));
            UpdateMaxSpill(allocator, CreateDefinition(TYP_BYREF));
            RecordMaxSpill(allocator);

            ref var registers = ref compiler.codeGen!.RegSet;
            Assert.That(registers.tmpTotalSize, Is.EqualTo(24));
            Assert.That(registers.tmpGetTemp(TYP_REF).tdTempType, Is.EqualTo(TYP_REF));
            Assert.That(registers.tmpGetTemp(TYP_LONG).tdTempType, Is.EqualTo(TYP_LONG));
            Assert.That(registers.tmpGetTemp(TYP_BYREF).tdTempType, Is.EqualTo(TYP_BYREF));
        });
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [TestCase(false)]
    [TestCase(true)]
    public static void UpperVectorReferencesDoNotDoubleCountSpilledTempsOrLocalHomes(bool local)
    {
        WithAllocator((compiler, allocator) => {
            var definition = CreateDefinition(TYP_SIMD32);
            var interval = definition.getInterval();
            if (local)
            {
                interval.isUpperVector = true;
                interval.relatedInterval = new Interval(TYP_SIMD32, SRBM_NONE) { isLocalVar = true, isSpilled = true };
            }
            else
            {
                UpdateMaxSpill(allocator, definition);
            }

            var save = new RefPosition(1, 4, definition.treeNode, RefType.RefTypeUpperVectorSave) {
                spillAfter = true,
            };
            save.setInterval(interval);
            UpdateMaxSpill(allocator, save);
            if (local)
            {
                save.refType = RefType.RefTypeUpperVectorRestore;
                save.reload = true;
                UpdateMaxSpill(allocator, save);
            }

            Assert.That(CurrentSpill(allocator)[(int)TYP_SIMD32], Is.EqualTo(local ? 0 : 1));
            Assert.That(MaxSpill(allocator)[(int)TYP_SIMD32], Is.EqualTo(local ? 0 : 1));
        });
    }
#endif

    private static RefPosition CreateDefinition(var_types type)
    {
        var tree = new GenTreeLclVar(type, 0);
        var interval = new Interval(type, SRBM_NONE);
        var definition = new RefPosition(1, 1, tree, RefType.RefTypeDef) { spillAfter = true };
        definition.setInterval(interval);
        interval.firstRefPosition = definition;
        return definition;
    }

    private static RefPosition CreateUse(RefPosition definition, bool reload)
    {
        var use = new RefPosition(1, 2, null, RefType.RefTypeUse) { reload = reload };
        use.setInterval(definition.getInterval());
        use.setMultiRegIdx(definition.getMultiRegIdx());
        return use;
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        try
        {
            action(compiler, new LinearScan(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "initMaxSpill")]
    private static extern void InitMaxSpill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "updateMaxSpill")]
    private static extern void UpdateMaxSpill(LinearScan allocator, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "recordMaxSpill")]
    private static extern void RecordMaxSpill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_maxSpill")]
    private static extern ref uint[] MaxSpill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentSpill")]
    private static extern ref uint[] CurrentSpill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
}
