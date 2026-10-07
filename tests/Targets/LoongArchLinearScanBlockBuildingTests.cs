// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LoongArchLinearScanBlockBuildingTests
{
    [TestCase(0, false, 0)]
    [TestCase(-2048, false, 0)]
    [TestCase(2047, false, 0)]
    [TestCase(2048, false, 1)]
    [TestCase(8, true, 1)]
    [TestCase(0, true, 0)]
    public static void ContainedIndirectionAddressReservesTemporaryOnlyForUnsupportedOffsets(
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
            Assert.That(InternalDefinitions(allocator, indirection),
                Has.Count.EqualTo(expectedInternalDefinitions));
        });
    }

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
            Assert.That(InternalDefinitions(allocator, call), Is.Empty);
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == call) &&
                (reference.refType is RefType.RefTypeDef) &&
                !reference.getInterval().isInternal), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void BlockCopyReservesItsTemporaryAndConsumesBothAddresses()
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
            Assert.That(InternalDefinitions(allocator, block), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void InitializationLoopConsumesItsFillAndReservesAnIntegerTemporary()
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

#if FEATURE_HW_INTRINSICS
    [Test]
    public static void HardwareIntrinsicNodeBuildingSkipsForUnportedLoongArchBackend()
    {
        WithAllocator((_, allocator) =>
        {
            var intrinsic = (GenTreeHWIntrinsic)RuntimeHelpers.GetUninitializedObject(typeof(GenTreeHWIntrinsic));
            var failure = Assert.Throws<FatalJitException>(() =>
                BuildHWIntrinsic(allocator, intrinsic, out _));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }
#endif

    [Test]
    public static void ArithmeticNodeConsumesOperandsAndDefinesItsResult()
    {
        WithAllocator((compiler, allocator) =>
        {
            var left = compiler.gtNewIconNode(TYP_INT, 3);
            var right = compiler.gtNewIconNode(TYP_INT, 5);
            ReferenceBuildLocation(allocator) = 2;
            var leftDefinition = BuildDef(allocator, left, SRBM_NONE, 0);
            var rightDefinition = BuildDef(allocator, right, SRBM_NONE, 0);
            var add = new GenTreeOp(GT_ADD, TYP_INT, left, right);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildNode(allocator, add), Is.EqualTo(2));
            Assert.That(leftDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == add) &&
                (reference.refType is RefType.RefTypeDef) &&
                !reference.getInterval().isInternal), Has.Count.EqualTo(1));
        });
    }

    private static System.Collections.Generic.List<RefPosition> InternalDefinitions(
        LinearScan allocator, GenTree tree)
        => allocator.refPositions.FindAll(reference =>
            reference.treeNode == tree &&
            reference.refType is RefType.RefTypeDef &&
            reference.getInterval().isInternal);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

#if FEATURE_HW_INTRINSICS
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildHWIntrinsic")]
    private static extern int BuildHWIntrinsic(
        LinearScan allocator, GenTreeHWIntrinsic intrinsic, out int destinationCount);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildNode")]
    private static extern int BuildNode(LinearScan allocator, GenTree tree);

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
