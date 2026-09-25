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
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanScalarConversionTests
{
    [Test]
    public static void LongToIntCastPreferencesTheSource()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_LONG, 17);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDef = BuildDef(allocator, source, SRBM_NONE, 0);
            var cast = new GenTreeCast(TYP_INT, source, false, TYP_INT);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(sourceDef.nextRefPosition));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(cast));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
        });
    }

    [Test]
    public static void OverflowingLongToIntCastDefinesAnInternalIntegerTemporary()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_LONG, 17);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, source, SRBM_NONE, 0);
            var cast = new GenTreeCast(TYP_INT, source, false, TYP_INT);
            cast.Flags |= GTF_OVERFLOW;
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            var internalDefs = allocator.refPositions.FindAll(
                reference => (reference.treeNode == cast) &&
                    (reference.refType is RefType.RefTypeDef) && reference.getInterval().isInternal);
            Assert.That(internalDefs, Has.Count.EqualTo(1));
            Assert.That(internalDefs[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(TargetPreferredUse(allocator), Is.Not.Null);
        });
    }

    [Test]
    public static void ContainedCastCountsItsAddressWithoutPreferringTheMemoryOperand()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var memory = compiler.gtNewIndir(TYP_LONG, address);
            memory.IsContained = true;
            var cast = new GenTreeCast(TYP_INT, memory, false, TYP_INT);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(TargetPreferredUse(allocator), Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnsignedLongToFloatingCastWithoutEvexDefinesTwoTempsAndRestrictsSource(bool apx)
    {
        WithAllocator((compiler, allocator) => {
            Assert.That(EvexSupported(allocator), Is.False);
            ApxSupported(allocator) = apx;
            if (apx)
            {
                AvailableIntRegs(allocator) |= SRBM_R16;
            }

            var source = compiler.gtNewIconNode(TYP_LONG, 23);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDef = BuildDef(allocator, source, SRBM_NONE, 0);
            var cast = new GenTreeCast(TYP_DOUBLE, source, true, TYP_DOUBLE);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            var internalDefs = allocator.refPositions.FindAll(
                reference => (reference.treeNode == cast) &&
                    (reference.refType is RefType.RefTypeDef) && reference.getInterval().isInternal);
            Assert.That(internalDefs, Has.Count.EqualTo(2));
            var firstCandidates = apx ? LowGprRegs(allocator) : AvailableIntRegs(allocator);
            var sourceCandidates = apx ? LowGprRegs(allocator) : AvailableIntRegs(allocator);
            Assert.That(internalDefs[0].registerAssignment, Is.EqualTo(firstCandidates));
            Assert.That(internalDefs[1].registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
            Assert.That(internalDefs[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(internalDefs[1].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(sourceDef.nextRefPosition?.registerAssignment, Is.EqualTo(sourceCandidates));
        });
    }

    [Test]
    public static void EvexUnsignedLongToFloatingCastNeedsNoIntegerTemporariesOrLowGprRestriction()
    {
        WithAllocator((compiler, allocator) => {
            ApxSupported(allocator) = true;
            EvexSupported(allocator) = true;
            AvailableIntRegs(allocator) |= SRBM_R16;
            var source = compiler.gtNewIconNode(TYP_LONG, 23);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDef = BuildDef(allocator, source, SRBM_NONE, 0);
            var cast = new GenTreeCast(TYP_DOUBLE, source, true, TYP_DOUBLE);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            Assert.That(sourceDef.nextRefPosition?.registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
            Assert.That(allocator.refPositions.FindAll(
                reference => (reference.treeNode == cast) &&
                    (reference.refType is RefType.RefTypeDef) && reference.getInterval().isInternal), Is.Empty);
        });
    }

    [TestCase(NI_System_Math_Abs, true)]
    [TestCase(NI_System_Math_Ceiling, false)]
    [TestCase(NI_System_Math_Floor, false)]
    [TestCase(NI_System_Math_Truncate, false)]
    [TestCase(NI_System_Math_Round, false)]
    [TestCase(NI_System_Math_Sqrt, false)]
    public static void MathIntrinsicsBuildCorrectInternalTemporaries(NamedIntrinsic name, bool expectsTemp)
    {
        WithAllocator((compiler, allocator) => {
            var operand = compiler.gtNewDconNode(TYP_DOUBLE, 4.0);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDef = BuildDef(allocator, operand, SRBM_NONE, 0);
            var intrinsic = new GenTreeIntrinsic(TYP_DOUBLE, operand, name, null);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildIntrinsic(allocator, intrinsic), Is.EqualTo(1));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(sourceDef.nextRefPosition));
            var internalDefs = allocator.refPositions.FindAll(
                reference => (reference.treeNode == intrinsic) &&
                    (reference.refType is RefType.RefTypeDef) && reference.getInterval().isInternal);
            Assert.That(internalDefs.Count, Is.EqualTo(expectsTemp ? 1 : 0));
            if (expectsTemp)
            {
                Assert.That(internalDefs[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            }
        });
    }

    [TestCase(NI_System_Math_Abs, false)]
    [TestCase(NI_System_Math_Abs, true)]
    [TestCase(NI_System_Math_Sqrt, false)]
    [TestCase(NI_System_Math_Sqrt, true)]
    public static void ContainedMathOperandUsesTheAppropriateAddressCandidates(
        NamedIntrinsic name, bool evex)
    {
        WithAllocator((compiler, allocator) => {
            EvexSupported(allocator) = evex;
            ApxSupported(allocator) = true;
            AvailableIntRegs(allocator) |= SRBM_R16;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var memory = compiler.gtNewIndir(TYP_DOUBLE, address);
            memory.IsContained = true;
            var intrinsic = new GenTreeIntrinsic(TYP_DOUBLE, memory, name, null);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildIntrinsic(allocator, intrinsic), Is.EqualTo(1));
            var expected = (name is not NI_System_Math_Abs || !evex)
                ? LowGprRegs(allocator)
                : AvailableIntRegs(allocator);
            Assert.That(addressDef.nextRefPosition?.registerAssignment, Is.EqualTo(expected));
            Assert.That(TargetPreferredUse(allocator), Is.Null);
        });
    }

    [TestCase(GenCondition.FEQ, false)]
    [TestCase(GenCondition.FLT, false)]
    [TestCase(GenCondition.FLE, false)]
    [TestCase(GenCondition.FNEU, true)]
    [TestCase(GenCondition.FGEU, true)]
    [TestCase(GenCondition.FGTU, true)]
    [TestCase(GenCondition.EQ, null)]
    public static void FloatingSelectConditionsDelayTheAdditionalCmovUse(
        GenCondition.CodeKind condition, bool? delayTrue)
    {
        WithAllocator((compiler, allocator) => {
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            ReferenceBuildLocation(allocator) = 2;
            var firstDef = BuildDef(allocator, first, SRBM_NONE, 0);
            var secondDef = BuildDef(allocator, second, SRBM_NONE, 0);
            var select = new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(condition), first, second);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildSelect(allocator, select), Is.EqualTo(2));
            var firstUse = firstDef.nextRefPosition;
            var secondUse = secondDef.nextRefPosition;
            Assert.That(firstUse?.delayRegFree, Is.EqualTo(delayTrue == true));
            Assert.That(secondUse?.delayRegFree, Is.EqualTo(delayTrue == false));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(firstUse));
            Assert.That(TargetPreferredUse2(allocator), Is.Null);
        });
    }

    [Test]
    public static void ConditionalSelectCountsItsConditionAndContainedAddress()
    {
        WithAllocator((compiler, allocator) => {
            var condition = compiler.gtNewIconNode(TYP_INT, 1);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            ReferenceBuildLocation(allocator) = 2;
            var conditionDef = BuildDef(allocator, condition, SRBM_NONE, 0);
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var secondDef = BuildDef(allocator, second, SRBM_NONE, 0);
            var memory = compiler.gtNewIndir(TYP_INT, address);
            memory.IsContained = true;
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, memory, second);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildSelect(allocator, select), Is.EqualTo(3));
            Assert.That(conditionDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(TargetPreferredUse2(allocator), Is.SameAs(secondDef.nextRefPosition));
        });
    }

    [Test]
    public static void SelectDelaysTheFirstUseWhenBothOperandsShareAnInterval()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [
                new() { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;

            var interval = new Interval(TYP_INT, SRBM_ALLINT_INIT);
            allocator.localVarIntervals = [interval];
            interval.setLocalNumber(compiler, 0, allocator);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var select = new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(GenCondition.EQ), local, local);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, compiler.gtNewIconNode(TYP_INT, 1), SRBM_NONE, 0);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildSelect(allocator, select), Is.EqualTo(2));
            var firstUse = allocator.refPositions[1];
            var secondUse = allocator.refPositions[2];
            Assert.That(firstUse.getInterval(), Is.SameAs(secondUse.getInterval()));
            Assert.That(firstUse.delayRegFree, Is.True);
            Assert.That(secondUse.delayRegFree, Is.False);
            Assert.That(allocator.refPositions[^1].getInterval().hasInterferingUses, Is.True);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCast")]
    private static extern int BuildCast(LinearScan allocator, GenTreeCast cast);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildIntrinsic")]
    private static extern int BuildIntrinsic(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildSelect")]
    private static extern int BuildSelect(LinearScan allocator, GenTreeOp select);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse")]
    private static extern ref RefPosition? TargetPreferredUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse2")]
    private static extern ref RefPosition? TargetPreferredUse2(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_evexIsSupported")]
    private static extern ref bool EvexSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_apxIsSupported")]
    private static extern ref bool ApxSupported(LinearScan allocator);

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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrash(Compiler compiler);

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
        CompilerFloatCalleeTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
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
