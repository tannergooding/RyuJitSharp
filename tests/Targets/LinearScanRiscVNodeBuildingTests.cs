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
    [TestCase(0, false, 0)]
    [TestCase(-2048, false, 0)]
    [TestCase(2047, false, 0)]
    [TestCase(2048, false, 1)]
    [TestCase(8, true, 0)]
    [TestCase(2048, true, 1)]
    [TestCase(0, true, 0)]
    public static void ContainedIndirectionAddressReservesTemporaryOnlyForOutOfRangeOffsets(
        int offset, bool hasIndex, int expectedInternalDefinitions)
    {
        WithAllocator((compiler, allocator) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, baseAddress, SRBM_NONE, 0);

            GenTree? index = null;
            if (hasIndex)
            {
                index = compiler.gtNewIconNode(TYP_I_IMPL, 3);
                ReferenceBuildLocation(allocator) = 4;
                _ = BuildDef(allocator, index, SRBM_NONE, 0);
            }

            var address = new GenTreeAddrMode(
                TYP_BYREF,
                baseAddress,
                index,
                hasIndex ? (byte)1 : (byte)0,
                offset)
            {
                IsContained = true,
            };
            var indirection = compiler.gtNewIndir(TYP_INT, address);
            ReferenceBuildLocation(allocator) = hasIndex ? 6u : 4u;

            Assert.That(BuildNode(allocator, indirection), Is.EqualTo(hasIndex ? 2 : 1));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(indirection));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == indirection) &&
                (reference.refType is RefType.RefTypeDef) &&
                reference.getInterval().isInternal), Has.Count.EqualTo(expectedInternalDefinitions));
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void Simd12IndirectionReservesAnInternalRegister()
    {
        WithAllocator((compiler, allocator) =>
        {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, address, SRBM_NONE, 0);
            var indirection = compiler.gtNewIndir(TYP_SIMD12, address);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildNode(allocator, indirection), Is.EqualTo(1));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == indirection) &&
                (reference.refType is RefType.RefTypeDef) &&
                reference.getInterval().isInternal), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void SimdNodeBuildingSkipsForUnportedRiscVBackend()
    {
        WithAllocator((_, allocator) =>
        {
            var simdTree = (GenTreeSIMD)RuntimeHelpers.GetUninitializedObject(typeof(GenTreeSIMD));
            var failure = Assert.Throws<FatalJitException>(() => BuildSIMD(allocator, simdTree));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }
#endif

#if FEATURE_HW_INTRINSICS
    [Test]
    public static void HardwareIntrinsicNodeBuildingSkipsForUnportedRiscVBackend()
    {
        WithAllocator((_, allocator) =>
        {
            var intrinsicTree = (GenTreeHWIntrinsic)RuntimeHelpers.GetUninitializedObject(typeof(GenTreeHWIntrinsic));
            var failure = Assert.Throws<FatalJitException>(() =>
                BuildHWIntrinsic(allocator, intrinsicTree, out _));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }
#endif

    [Test]
    public static void CallConsumesItsIndirectTargetAndDefinesItsReturnValue()
    {
        WithAllocator((compiler, allocator) =>
        {
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var targetDefinition = BuildDef(allocator, target, SRBM_NONE, 0);
            var call = new GenTreeCall(TYP_INT)
            {
                _callType = gtCallTypes.CT_INDIRECT,
                ControlExpr = target,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildNode(allocator, call), Is.EqualTo(1));
            Assert.That(targetDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == call) &&
                (reference.refType is RefType.RefTypeDef) &&
                !reference.getInterval().isInternal), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void CallMaterializesAContainedIndirectTarget()
    {
        WithAllocator((compiler, allocator) =>
        {
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            target.IsContained = true;
            var call = new GenTreeCall(TYP_INT)
            {
                _callType = gtCallTypes.CT_INDIRECT,
                ControlExpr = target,
            };
            ReferenceBuildLocation(allocator) = 2;

            Assert.That(BuildNode(allocator, call), Is.Zero);
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == call) &&
                (reference.refType is RefType.RefTypeDef) &&
                reference.getInterval().isInternal), Has.Count.EqualTo(1));
        });
    }

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

            Assert.That(BuildNode(allocator, block), Is.EqualTo(2));
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

            Assert.That(BuildNode(allocator, block), Is.EqualTo(2));
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

            Assert.That(BuildNode(allocator, block), Is.EqualTo(1));
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
            Assert.That(BuildNode(allocator, cast), Is.EqualTo(1));
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

            Assert.That(BuildNode(allocator, cast), Is.EqualTo(1));
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

    [Test]
    public static void NodeBuilderConsumesBinaryOperandsAndDefinesTheResult()
    {
        WithAllocator((compiler, allocator) =>
        {
            var left = compiler.gtNewIconNode(TYP_INT, 12);
            var right = compiler.gtNewIconNode(TYP_INT, 30);
            ReferenceBuildLocation(allocator) = 2;
            Assert.That(BuildNode(allocator, left), Is.Zero);
            var leftDefinition = allocator.refPositions.FindLast(reference =>
                (reference.treeNode == left) && (reference.refType is RefType.RefTypeDef))
                ?? throw new AssertionException("The left constant has no register definition.");
            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildNode(allocator, right), Is.Zero);
            var rightDefinition = allocator.refPositions.FindLast(reference =>
                (reference.treeNode == right) && (reference.refType is RefType.RefTypeDef))
                ?? throw new AssertionException("The right constant has no register definition.");

            Assert.That(leftDefinition.getInterval().isConstant, Is.True);
            Assert.That(rightDefinition.getInterval().isConstant, Is.True);

            var addition = new GenTreeOp(GT_ADD, TYP_INT, left, right);
            ReferenceBuildLocation(allocator) = 6;
            Assert.That(BuildNode(allocator, addition), Is.EqualTo(2));

            Assert.That(leftDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == addition) &&
                (reference.refType is RefType.RefTypeDef) &&
                !reference.getInterval().isInternal), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void StructStackArgumentReservesLoadAndStoreTemporaries()
    {
        WithAllocator((compiler, allocator) =>
        {
            var sourceAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDefinition = BuildDef(allocator, sourceAddress, SRBM_NONE, 0);

            var source = new GenTreeBlk(TYP_STRUCT, sourceAddress, new ClassLayout(16))
            {
                IsContained = true,
            };
            var argument = new GenTreePutArgStk(TYP_VOID, source, null, 0, 16, false);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildNode(allocator, argument), Is.EqualTo(1));
            Assert.That(sourceDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, argument), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public static void ScalarStackArgumentConsumesItsSourceWithoutTemporaries()
    {
        WithAllocator((compiler, allocator) =>
        {
            var source = compiler.gtNewIconNode(TYP_LONG, 17);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDefinition = BuildDef(allocator, source, SRBM_NONE, 0);
            var argument = new GenTreePutArgStk(TYP_VOID, source, null, 0, TARGET_POINTER_SIZE, false);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildNode(allocator, argument), Is.EqualTo(1));
            Assert.That(sourceDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, argument), Is.Empty);
        });
    }

    private static System.Collections.Generic.List<RefPosition> InternalDefinitions(
        LinearScan allocator, GenTree tree)
        => allocator.refPositions.FindAll(reference =>
            reference.treeNode == tree &&
            reference.refType is RefType.RefTypeDef &&
            reference.getInterval().isInternal);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildNode")]
    private static extern int BuildNode(LinearScan allocator, GenTree tree);

#if FEATURE_SIMD
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildSIMD")]
    private static extern int BuildSIMD(LinearScan allocator, GenTreeSIMD simdTree);
#endif

#if FEATURE_HW_INTRINSICS
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildHWIntrinsic")]
    private static extern int BuildHWIntrinsic(
        LinearScan allocator, GenTreeHWIntrinsic intrinsicTree, out int destinationCount);
#endif

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
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        codeGen.RegSet.rsClearRegsModified();
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
