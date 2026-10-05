// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRiscVNodeBuildingTests
{
    [Test]
    public static void BlockCopyReservesAnIntegerTemporaryAndConsumesBothAddresses()
    {
        WithAllocator((compiler, allocator) =>
        {
            var destinationAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var sourceAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            ReferenceBuildLocation(allocator) = 2;
            var destinationDefinition = BuildDef(allocator, destinationAddress, SRBM_NONE, 0);
            var sourceDefinition = BuildDef(allocator, sourceAddress, SRBM_NONE, 0);

            var source = compiler.gtNewIndir(TYP_STRUCT, sourceAddress);
            source.IsContained = true;
            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, source, new ClassLayout(16))
            {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(2));
            Assert.That(destinationDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(sourceDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));

            var definitions = InternalDefinitions(allocator, block);
            Assert.That(definitions, Has.Count.EqualTo(1));
            Assert.That(definitions[0].getInterval().registerType, Is.EqualTo(TYP_INT));
            Assert.That(definitions[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    [Test]
    public static void InitializationLoopConsumesTheFillAndReservesItsIntegerTemporary()
    {
        WithAllocator((compiler, allocator) =>
        {
            var destinationAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var fill = compiler.gtNewIconNode(TYP_INT, 0x5A);
            ReferenceBuildLocation(allocator) = 2;
            var destinationDefinition = BuildDef(allocator, destinationAddress, SRBM_NONE, 0);
            var fillDefinition = BuildDef(allocator, fill, SRBM_NONE, 0);

            var initValue = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, fill) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, initValue, new ClassLayout(16))
            {
                _kind = GenTreeBlk.BlkOpKindLoop,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(2));
            Assert.That(destinationDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(fillDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, block), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void AlignedLargeLocalInitializationReservesAnIntegerTemporary()
    {
        WithAllocator((compiler, allocator) =>
        {
            var destinationAddress = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0);
            var fill = compiler.gtNewIconNode(TYP_INT, 0);
            fill.IsContained = true;
            ReferenceBuildLocation(allocator) = 2;
            var destinationDefinition = BuildDef(allocator, destinationAddress, SRBM_NONE, 0);

            var initValue = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, fill) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, initValue, new ClassLayout(16))
            {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(1));
            Assert.That(destinationDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, block), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void CheckedLongToIntCastReservesItsOverflowCheckTemporary()
    {
        WithAllocator((compiler, allocator) =>
        {
            var source = compiler.gtNewIconNode(TYP_LONG, 17);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDefinition = BuildDef(allocator, source, SRBM_NONE, 0);
            var cast = new GenTreeCast(TYP_INT, source, false, TYP_INT);
            cast.Flags |= GTF_OVERFLOW;
            ReferenceBuildLocation(allocator) = 4;

            var castDescriptor = new CodeGen.GenIntCastDesc(cast);
            Assert.That(castDescriptor.Check, Is.Not.EqualTo(CHECK_NONE));
            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            Assert.That(sourceDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));

            var definitions = InternalDefinitions(allocator, cast);
            Assert.That(definitions, Has.Count.EqualTo(1));
            Assert.That(definitions[0].getInterval().registerType, Is.EqualTo(TYP_INT));
            Assert.That(definitions[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    [Test]
    public static void FloatingToIntegerCastReservesItsConversionTemporaryAfterTheResult()
    {
        WithAllocator((compiler, allocator) =>
        {
            var source = compiler.gtNewDconNode(TYP_DOUBLE, 4.5);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDefinition = BuildDef(allocator, source, SRBM_NONE, 0);
            var cast = new GenTreeCast(TYP_INT, source, false, TYP_INT);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            Assert.That(sourceDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));

            var castDefinition = allocator.refPositions.FindLast(
                reference => reference.treeNode == cast &&
                    reference.refType is RefType.RefTypeDef &&
                    !reference.getInterval().isInternal)
                ?? throw new AssertionException("The cast has no register definition.");
            var internalDefinitions = InternalDefinitions(allocator, cast);
            Assert.That(internalDefinitions, Has.Count.EqualTo(1));
            Assert.That(allocator.refPositions.IndexOf(internalDefinitions[0]),
                Is.GreaterThan(allocator.refPositions.IndexOf(castDefinition)));
            Assert.That(internalDefinitions[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    private static System.Collections.Generic.List<RefPosition> InternalDefinitions(
        LinearScan allocator, GenTree tree)
        => allocator.refPositions.FindAll(reference =>
            reference.treeNode == tree &&
            reference.refType is RefType.RefTypeDef &&
            reference.getInterval().isInternal);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildBlockStore")]
    private static extern int BuildBlockStore(LinearScan allocator, GenTreeBlk block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCast")]
    private static extern int BuildCast(LinearScan allocator, GenTreeCast cast);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

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
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        try
        {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
