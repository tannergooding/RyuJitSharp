// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanOperandAndCallUsesTests
{
    [Test]
    public static void ContainedAddressModeBuildsUsesForItsUncontainedBaseAndIndex()
    {
        WithAllocator((compiler, allocator) => {
            var baseNode = compiler.gtNewIconNode(TYP_INT, 8);
            var indexNode = compiler.gtNewIconNode(TYP_INT, 3);
            ReferenceBuildLocation(allocator) = 1;
            var baseDefinition = BuildDef(allocator, baseNode, SRBM_RAX | SRBM_RBX, 0);
            var indexDefinition = BuildDef(allocator, indexNode, SRBM_RCX | SRBM_RDX, 0);

            var address = new GenTreeAddrMode(TYP_BYREF, baseNode, indexNode, 1, 4) {
                IsContained = true,
            };
            var indir = compiler.gtNewIndir(TYP_INT, address);
            indir.IsContained = true;
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildOperandUses(allocator, indir, SRBM_NONE), Is.EqualTo(2));
            Assert.That(baseDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(indexDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    [Test]
    public static void ContainedByteSwapBuildsUseForItsSource()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_INT, 0x12345678);
            ReferenceBuildLocation(allocator) = 1;
            var definition = BuildDef(allocator, source, SRBM_RAX | SRBM_RBX, 0);
            var byteSwap = new GenTreeUnOp(GT_BSWAP, TYP_INT, source) {
                IsContained = true,
            };
            ReferenceBuildLocation(allocator) = 3;

            Assert.That(BuildOperandUses(allocator, byteSwap, SRBM_NONE), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    [Test]
    public static void ContainedComparisonBuildsUsesForBothOperands()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 5);
            var right = compiler.gtNewIconNode(TYP_INT, 7);
            ReferenceBuildLocation(allocator) = 1;
            var leftDefinition = BuildDef(allocator, left, SRBM_RAX | SRBM_RBX, 0);
            var rightDefinition = BuildDef(allocator, right, SRBM_RCX | SRBM_RDX, 0);
            var compare = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, left, right);
            compare.IsContained = true;
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildOperandUses(allocator, compare, SRBM_NONE), Is.EqualTo(2));
            Assert.That(leftDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    [Test]
    public static void DelayFreeUseMarksAndReturnsTheCreatedReference()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_INT, 17);
            ReferenceBuildLocation(allocator) = 1;
            var definition = BuildDef(allocator, source, SRBM_RAX | SRBM_RBX, 0);
            ReferenceBuildLocation(allocator) = 3;
            RefPosition? use = null;

            Assert.That(BuildDelayFreeUses(allocator, source, null, SRBM_NONE, ref use), Is.EqualTo(1));
            var actualUse = use ?? throw new AssertionException("The delay-free use reference was not created.");
            Assert.That(actualUse.delayRegFree, Is.True);
            Assert.That(actualUse, Is.SameAs(definition.nextRefPosition));
            Assert.That(PendingDelayFree(allocator), Is.True);
        });
    }

    [Test]
    public static void ContainedIndirectionDelayFreesItsAddressComponents()
    {
        WithAllocator((compiler, allocator) => {
            var baseNode = compiler.gtNewIconNode(TYP_INT, 8);
            var indexNode = compiler.gtNewIconNode(TYP_INT, 3);
            ReferenceBuildLocation(allocator) = 1;
            var baseDefinition = BuildDef(allocator, baseNode, SRBM_RAX | SRBM_RBX, 0);
            var indexDefinition = BuildDef(allocator, indexNode, SRBM_RCX | SRBM_RDX, 0);
            var address = new GenTreeAddrMode(TYP_BYREF, baseNode, indexNode, 1, 4) {
                IsContained = true,
            };
            var indir = compiler.gtNewIndir(TYP_INT, address);
            indir.IsContained = true;
            ReferenceBuildLocation(allocator) = 4;
            RefPosition? lastUse = null;

            Assert.That(BuildDelayFreeUses(allocator, indir, null, SRBM_NONE, ref lastUse), Is.EqualTo(2));
            Assert.That(baseDefinition.nextRefPosition?.delayRegFree, Is.True);
            Assert.That(indexDefinition.nextRefPosition?.delayRegFree, Is.True);
            Assert.That(lastUse, Is.SameAs(indexDefinition.nextRefPosition));
        });
    }

    [Test]
    public static void RegisterCallArgumentBuildsUseWithItsAssignedRegister()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 29);
            var putArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, value) {
                RegNum = REG_RCX,
            };
            ReferenceBuildLocation(allocator) = 1;
            var definition = BuildDef(allocator, putArg, SRBM_RCX, 0);
            var arg = new CallArg(NewCallArg.CreateForPrimitive(value)) {
                LateNode = putArg,
            };
            var call = new GenTreeCall(TYP_VOID);
            call.Args.PushLateBack(arg);
            ReferenceBuildLocation(allocator) = 3;

            Assert.That(BuildCallArgUses(allocator, call), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(definition.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RCX));
        });
    }

#if FEATURE_MULTIREG_ARGS
    [Test]
    public static void FieldListCallArgumentBuildsAUseForEachRegisterField()
    {
        WithAllocator((compiler, allocator) => {
            var firstValue = compiler.gtNewIconNode(TYP_INT, 29);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 31);
            var firstPutArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, firstValue) {
                RegNum = REG_RCX,
            };
            var secondPutArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, secondValue) {
                RegNum = REG_RDX,
            };
            ReferenceBuildLocation(allocator) = 1;
            var firstDefinition = BuildDef(allocator, firstPutArg, SRBM_RCX, 0);
            var secondDefinition = BuildDef(allocator, secondPutArg, SRBM_RDX, 0);
            var fieldList = new GenTreeFieldList();
            fieldList.AddFieldLIR(compiler, firstPutArg, 0, TYP_INT);
            fieldList.AddFieldLIR(compiler, secondPutArg, 4, TYP_INT);
            var arg = new CallArg(NewCallArg.CreateForPrimitive(fieldList)) {
                LateNode = fieldList,
            };
            var call = new GenTreeCall(TYP_VOID);
            call.Args.PushLateBack(arg);
            ReferenceBuildLocation(allocator) = 3;

            Assert.That(BuildCallArgUses(allocator, call), Is.EqualTo(2));
            Assert.That(firstDefinition.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RCX));
            Assert.That(secondDefinition.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RDX));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildOperandUses")]
    private static extern int BuildOperandUses(LinearScan allocator, GenTree node, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDelayFreeUses")]
    private static extern int BuildDelayFreeUses(
        LinearScan allocator, GenTree node, GenTree? rmwNode, regMask candidates, ref RefPosition? useRefPosition);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCallArgUses")]
    private static extern int BuildCallArgUses(LinearScan allocator, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pendingDelayFree")]
    private static extern ref bool PendingDelayFree(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

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
