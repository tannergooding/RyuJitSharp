// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanNodeBuildingTests
{
    [Test]
    public static void NodeSequenceBuildsConstantIntervalsAndConsumesTheirDefinitions()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 23);
            var right = compiler.gtNewIconNode(TYP_INT, 11);
            Assert.That(Build(allocator, left), Is.Zero);
            var leftDef = allocator.refPositions[^1];
            Assert.That(Build(allocator, right), Is.Zero);
            var rightDef = allocator.refPositions[^1];
            var subtract = compiler.gtNewBinaryNode(GT_SUB, TYP_INT, left, right);

            Assert.That(Build(allocator, subtract), Is.EqualTo(2));
            Assert.That(leftDef.getInterval().isConstant, Is.True);
            Assert.That(rightDef.getInterval().isConstant, Is.True);
            Assert.That(rightDef.nextRefPosition?.delayRegFree, Is.True);
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(leftDef.nextRefPosition));
            Assert.That(PendingDelayFree(allocator), Is.True);
            Assert.That(allocator.refPositions[^1].getInterval().hasInterferingUses, Is.True);

            Assert.That(Build(allocator, compiler.gtNewIconNode(TYP_INT, 7)), Is.Zero);
            Assert.That(TargetPreferredUse(allocator), Is.Null);
            Assert.That(PendingDelayFree(allocator), Is.False);
            Assert.That(allocator.refPositions[^1].getInterval().hasInterferingUses, Is.False);
        });
    }

    [Test]
    public static void InternalRegisterStateDoesNotLeakBetweenNodes()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewDconNode(TYP_DOUBLE, 2.5);
            _ = Build(allocator, value);
            var negate = new GenTreeUnOp(GT_NEG, TYP_DOUBLE, value);

            Assert.That(Build(allocator, negate), Is.EqualTo(1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            Assert.That(allocator.refPositions.Exists(reference =>
                reference.treeNode == negate && reference.refType is RefType.RefTypeDef &&
                reference.getInterval().isInternal), Is.True);

            var next = compiler.gtNewIconNode(TYP_INT, 7);
            Assert.That(Build(allocator, next), Is.Zero);
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
            Assert.That(allocator.refPositions.FindAll(reference => reference.treeNode == next), Has.Count.EqualTo(1));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalLoadsReconcileCandidatesAndOptionalContainment(bool candidate, bool optional)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new() { Type = TYP_INT, lvLRACandidate = candidate }];
            compiler.lvaCount = 1;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.IsRegOptional = optional;

            Assert.That(Build(allocator, local), Is.Zero);
            Assert.That(local.IsContained, Is.EqualTo(!candidate && optional));
            Assert.That(local.IsRegOptional, Is.EqualTo(candidate && optional));
            Assert.That(allocator.refPositions.Count, Is.EqualTo(candidate || optional ? 0 : 1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiRegisterLocalsRetainOnlyIndependentPromotion(bool independent)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaEnregMultiRegVars = true;
            compiler.lvaTable = [
                new() {
                    Type = TYP_STRUCT, lvPromoted = true, lvFieldCnt = 2, lvFieldLclStart = 1,
                    lvDoNotEnregister = !independent,
                },
                new() { Type = TYP_INT, lvLRACandidate = independent },
                new() { Type = TYP_INT, lvLRACandidate = independent },
            ];
            compiler.lvaCount = 3;
            var local = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            local.Flags |= GTF_VAR_MULTIREG;

            Assert.That(Build(allocator, local), Is.Zero);
            Assert.That(local.IsMultiReg, Is.EqualTo(independent));
            Assert.That(local.IsContained, Is.EqualTo(!independent));
            Assert.That(allocator.refPositions, Is.Empty);
            if (independent)
            {
                Assert.That(local.GetRegisterDstCount(compiler), Is.EqualTo(2));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BitcastsKeepNativeIntegerRegisterRestrictions(bool integerSource)
    {
        WithAllocator((compiler, allocator) => {
            AvailableIntRegs(allocator) |= SRBM_R16;
            GenTree source = integerSource
                ? compiler.gtNewIconNode(TYP_LONG, 23)
                : compiler.gtNewDconNode(TYP_DOUBLE, 2.5);
            _ = Build(allocator, source);
            var sourceDef = allocator.refPositions[^1];
            var bitcast = new GenTreeUnOp(GT_BITCAST, integerSource ? TYP_DOUBLE : TYP_LONG, source);

            Assert.That(Build(allocator, bitcast), Is.EqualTo(1));
            Assert.That(integerSource
                ? sourceDef.nextRefPosition?.registerAssignment
                : allocator.refPositions[^1].registerAssignment, Is.EqualTo(LowGprRegs(allocator)));
        });
    }

    [Test]
    public static void CompareExchangeReservesRaxForComparandAndResult()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var data = compiler.gtNewIconNode(TYP_INT, 2);
            var comparand = compiler.gtNewIconNode(TYP_INT, 1);
            _ = Build(allocator, address);
            var addressDef = allocator.refPositions[^1];
            _ = Build(allocator, data);
            var dataDef = allocator.refPositions[^1];
            _ = Build(allocator, comparand);
            var comparandDef = allocator.refPositions[^1];
            var exchange = new GenTreeCmpXchg(TYP_INT, address, data, comparand);

            Assert.That(Build(allocator, exchange), Is.EqualTo(3));
            Assert.That(addressDef.nextRefPosition?.registerAssignment & SRBM_RAX, Is.EqualTo(SRBM_NONE));
            Assert.That(dataDef.nextRefPosition?.registerAssignment & SRBM_RAX, Is.EqualTo(SRBM_NONE));
            Assert.That(comparandDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(allocator.refPositions[^1].registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }

    [TestCase(GT_XORR, false)]
    [TestCase(GT_XORR, true)]
    [TestCase(GT_XAND, false)]
    [TestCase(GT_XAND, true)]
    [TestCase(GT_XADD, false)]
    [TestCase(GT_XCHG, false)]
    public static void AtomicOperationsPreserveLoopTempsAndDelayedAddressUses(genTreeOps operation, bool unused)
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var data = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, address);
            var addressDef = allocator.refPositions[^1];
            _ = Build(allocator, data);
            var atomic = compiler.gtNewBinaryNode(operation, TYP_INT, address, data);
            atomic.IsUnusedValue = unused;
            var needsLoop = !unused && operation is GT_XORR or GT_XAND;

            Assert.That(Build(allocator, atomic), Is.EqualTo(2));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(needsLoop ? 1 : 0));
            Assert.That(addressDef.nextRefPosition?.delayRegFree, Is.EqualTo(!needsLoop));
            var result = allocator.refPositions.FindLast(reference =>
                reference.treeNode == atomic && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal) ?? throw new AssertionException("Missing atomic definition.");
            Assert.That(result.registerAssignment, Is.EqualTo(needsLoop ? SRBM_RAX : AvailableIntRegs(allocator)));
            Assert.That(result.isLocalDefUse, Is.EqualTo(unused));
        });
    }

    [Test]
    public static void IndexAddressesReserveTheirNativeSizedTemporary()
    {
        WithAllocator((compiler, allocator) => {
            var array = compiler.gtNewIconNode(TYP_REF, 0);
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, array);
            _ = Build(allocator, index);
            var address = new GenTreeIndexAddr(array, index, TYP_INT, null, 4, 8, 16, true);

            Assert.That(Build(allocator, address), Is.EqualTo(2));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(address));
            Assert.That(address.GetRegisterDstCount(compiler), Is.EqualTo(1));
        });
    }

    [Test]
    public static void PlacedArgumentsSurviveNodeResetsUntilTheCallConsumesThem()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, value);
            var putArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, value) { RegNum = REG_RCX };
            Assert.That(Build(allocator, putArg), Is.EqualTo(1));
            var definition = allocator.refPositions[^1];
            Assert.That(PlacedArgumentRegisters(allocator).IsSet(REG_RCX), Is.True);

            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            argument.EarlyNode = null;
            argument.LateNode = putArg;
            call.Args.PushLateBack(argument);

            Assert.That(Build(allocator, call), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RCX));
            Assert.That(PlacedArgumentRegisters(allocator).IsEmpty, Is.True);
        });
    }

    [TestCase(GT_COPY)]
    [TestCase(GT_RELOAD)]
    public static void RegisterDestinationCountsFollowCopySources(genTreeOps operation)
    {
        WithAllocator((compiler, _) => {
            var source = compiler.gtNewIconNode(TYP_INT, 23);
            var copy = new GenTreeCopyOrReload(operation, TYP_INT, source);

            Assert.That(copy.IsMultiRegNode, Is.True);
            Assert.That(copy.GetRegisterDstCount(compiler), Is.EqualTo(1));
            Assert.That(new GenTree(GT_NOP, TYP_VOID).GetRegisterDstCount(compiler), Is.Zero);
        });
    }

    [TestCase(NI_X86Base_DivRem)]
    [TestCase(NI_X86Base_X64_DivRem)]
    [TestCase(NI_X86Base_X64_BigMul)]
    public static void MultiRegisterHardwareDispatchPreservesBothDestinations(NamedIntrinsic id)
    {
        WithAllocator((compiler, allocator) => {
            var type = id is NI_X86Base_DivRem ? TYP_INT : TYP_LONG;
            var first = compiler.gtNewIconNode(type, 23);
            var second = compiler.gtNewIconNode(type, 0);
            _ = Build(allocator, first);
            _ = Build(allocator, second);

            var operandCount = 2;
            GenTreeHWIntrinsic intrinsic;
            if (id is NI_X86Base_X64_BigMul)
            {
                intrinsic = new GenTreeHWIntrinsic(TYP_STRUCT, id, type, 0, first, second);
            }
            else
            {
                var divisor = compiler.gtNewIconNode(type, 3);
                _ = Build(allocator, divisor);
                intrinsic = new GenTreeHWIntrinsic(TYP_STRUCT, id, type, 0, first, second, divisor);
                operandCount = 3;
            }

            Assert.That(Build(allocator, intrinsic), Is.EqualTo(operandCount));
            Assert.That(intrinsic.IsMultiRegNode, Is.True);
            Assert.That(intrinsic.GetRegisterDstCount(compiler), Is.EqualTo(2));
            var definitions = allocator.refPositions.FindAll(reference =>
                reference.treeNode == intrinsic && reference.refType is RefType.RefTypeDef);
            Assert.That(definitions, Has.Count.EqualTo(2));
            Assert.That(definitions[0].registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(definitions[1].registerAssignment, Is.EqualTo(SRBM_RDX));
        });
    }

    [Test]
    public static void UnloweredSwitchRejectsInsteadOfBuildingNoReferences()
    {
        WithAllocator((compiler, allocator) => {
            var selector = compiler.gtNewIconNode(TYP_INT, 1);
            var node = new GenTreeUnOp(GT_SWITCH, TYP_VOID, selector);
            _ = Assert.Throws<FatalJitException>(() => Build(allocator, node));
        });
    }

    [TestCase(TYP_SIMD16, TYP_SIMD32)]
    [TestCase(TYP_SIMD32, TYP_SIMD64)]
    public static void SimdInitializationTempIsImplicitlyLiveAndOnlyGrows(var_types initialType, var_types largerType)
    {
        WithAllocator((compiler, _) => {
            compiler.lvaTable = [];
            compiler.lvaSimdInitTempVarNum = BAD_VAR_NUM;
            var temp = compiler.getSIMDInitTempVarNum(initialType);

            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(compiler.lvaGetDesc(temp).Type, Is.EqualTo(initialType));
            Assert.That(compiler.lvaGetDesc(temp).lvImplicitlyReferenced, Is.True);
            Assert.That(compiler.getSIMDInitTempVarNum(largerType), Is.EqualTo(temp));
            Assert.That(compiler.lvaGetDesc(temp).Type, Is.EqualTo(largerType));
            Assert.That(compiler.getSIMDInitTempVarNum(initialType), Is.EqualTo(temp));
            Assert.That(compiler.lvaGetDesc(temp).Type, Is.EqualTo(largerType));
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
        });
    }

    private static int Build(LinearScan allocator, GenTree node)
    {
        ReferenceBuildLocation(allocator) += 2;
        return BuildNode(allocator, node);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildNode")]
    private static extern int BuildNode(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_internalDefinitionCount")]
    private static extern ref int InternalDefinitionCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pendingDelayFree")]
    private static extern ref bool PendingDelayFree(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse")]
    private static extern ref RefPosition? TargetPreferredUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_placedArgumentRegisters")]
    private static extern ref regMaskTP PlacedArgumentRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lowGprRegs")]
    private static extern ref regMask LowGprRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask CompilerIntCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask CompilerMaskCalleeTrash(Compiler compiler);

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
        CompilerIntCalleeTrash(compiler) = SRBM_INT_CALLEE_TRASH_INIT;
        CompilerFloatCalleeTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
        CompilerMaskCalleeTrash(compiler) = SRBM_MSK_CALLEE_TRASH_INIT;
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
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
