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
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanLocalStoreAndReturnTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void CandidateLocalStoreTracksLivenessAndSourcePreference(bool lastUse)
    {
        WithAllocator((compiler, allocator) => {
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true });
            var source = compiler.gtNewIconNode(TYP_INT, 42);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDefinition = BuildDef(allocator, source, SRBM_NONE, 0);
            var store = new GenTreeLclVar(TYP_INT, 0, source);
            if (lastUse)
            {
                store.Flags |= GTF_VAR_DEATH;
            }

            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(1));
            var sourceUse = sourceDefinition.nextRefPosition;
            var destinationInterval = allocator.localVarIntervals![0]
                ?? throw new AssertionException("The destination local has no interval.");
            Assert.That(sourceUse?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(sourceDefinition.getInterval().relatedInterval, Is.SameAs(destinationInterval));
            Assert.That(destinationInterval.lastRefPosition?.refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(destinationInterval.lastRefPosition?.nodeLocation, Is.EqualTo(5));
            Assert.That(destinationInterval.lastRefPosition?.registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
            Assert.That(LiveVariables(allocator)[0] & 1, Is.EqualTo(lastUse ? (nint)0 : (nint)1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LocalCopyPrefersItsDestinationOnlyAfterTheSourcesLastUse(bool lastUse)
    {
        WithAllocator((compiler, allocator) => {
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_INT, _varIndex = 1, lvTracked = true, lvLRACandidate = true });
            LiveVariables(allocator) = [2];
            var source = compiler.gtNewLclvNode(TYP_INT, 1);
            if (lastUse)
            {
                source.Flags |= GTF_VAR_DEATH;
            }

            var store = new GenTreeLclVar(TYP_INT, 0, source);
            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(1));

            var destination = allocator.localVarIntervals![0]
                ?? throw new AssertionException("The destination local has no interval.");
            var sourceInterval = allocator.localVarIntervals[1]
                ?? throw new AssertionException("The source local has no interval.");
            Assert.That(sourceInterval.relatedInterval, Is.EqualTo(lastUse ? destination : null));
            Assert.That(sourceInterval.lastRefPosition?.treeNode, Is.SameAs(source));
            Assert.That(LiveVariables(allocator)[0] & 2, Is.EqualTo(lastUse ? (nint)0 : (nint)2));
        });
    }

    [Test]
    public static void WriteThroughVectorStoreDefinesAnOptionalRegisterAndClearsPartialSpill()
    {
        WithAllocator((compiler, allocator) => {
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_SIMD32, _varIndex = 0, lvTracked = true, lvLRACandidate = true });
            var destination = allocator.localVarIntervals![0]
                ?? throw new AssertionException("The vector local has no interval.");
            destination.isWriteThru = true;
            destination.isPartiallySpilled = true;
            var source = new GenTreeVecCon(TYP_SIMD32);
            source.SimdVal.u32[0] = 1;
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, source, SRBM_NONE, 0);
            var store = new GenTreeLclVar(TYP_SIMD32, 0, source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(1));
            Assert.That(destination.lastRefPosition?.regOptional, Is.True);
            Assert.That(destination.isPartiallySpilled, Is.False);
        });
    }

    [Test]
    public static void NonCandidateContainedConstantDoesNotDefineARegister()
    {
        WithAllocator((compiler, allocator) => {
            SetLocals(compiler, allocator, new LclVarDsc { Type = TYP_INT });
            var source = compiler.gtNewIconNode(TYP_INT, 7);
            source.IsContained = true;
            var store = new GenTreeLclVar(TYP_INT, 0, source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.Zero);
            Assert.That(allocator.refPositions, Is.Empty);
        });
    }

    [Test]
    public static void NonCandidateLocalFieldStoreUsesItsSourceWithoutDefiningTheParent()
    {
        WithAllocator((compiler, allocator) => {
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(8) });
            var source = compiler.gtNewIconNode(TYP_INT, 17);
            ReferenceBuildLocation(allocator) = 2;
            var definition = BuildDef(allocator, source, SRBM_NONE, 0);
            var store = new GenTreeLclFld(TYP_INT, 0, 4, source, null);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions.FindAll(reference => reference.treeNode == store), Is.Empty);
        });
    }

    [Test]
    public static void ContainedBitcastUsesItsSourceAndPreferencesCandidateDestination()
    {
        WithAllocator((compiler, allocator) => {
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_FLOAT, _varIndex = 0, lvTracked = true, lvLRACandidate = true });
            var source = compiler.gtNewIconNode(TYP_INT, 17);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDef = BuildDef(allocator, source, SRBM_NONE, 0);
            var bitcast = new GenTreeUnOp(GT_BITCAST, TYP_FLOAT, source) { IsContained = true };
            var store = new GenTreeLclVar(TYP_FLOAT, 0, bitcast);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(1));
            Assert.That(sourceDef.nextRefPosition?.registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
            Assert.That(sourceDef.getInterval().relatedInterval, Is.SameAs(allocator.localVarIntervals![0]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Simd12StoreUsesAFloatTemporaryUnlessItsSourceIsVectorZero(bool zero)
    {
        WithAllocator((compiler, allocator) => {
            SetLocals(compiler, allocator, new LclVarDsc { Type = TYP_SIMD12 });
            var source = new GenTreeVecCon(TYP_SIMD16);
            if (!zero)
            {
                source.SimdVal.u32[0] = 1;
            }

            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, source, SRBM_NONE, 0);
            var store = new GenTreeLclVar(TYP_SIMD12, 0, source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(1));
            var temporary = allocator.refPositions.FindAll(reference =>
                reference.treeNode == store && (reference.refType is RefType.RefTypeDef) &&
                reference.getInterval().isInternal);
            Assert.That(temporary, Has.Count.EqualTo(zero ? 0 : 1));
            if (!zero)
            {
                Assert.That(temporary[0].registerAssignment, Is.EqualTo(AvailableFloatRegs(allocator)));
                Assert.That(temporary[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiRegisterStoreInterleavesSourceAndDestinationReferences(bool containedSource)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaEnregMultiRegVars = true;
            var layout = new ClassLayout(16);
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_STRUCT, Layout = layout, lvPromoted = true,
                    lvFieldLclStart = 1, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_LONG, _varIndex = 1, lvTracked = true, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_STRUCT, Layout = layout, lvPromoted = true,
                    lvFieldLclStart = 4, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, _varIndex = 2, lvTracked = true, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_LONG, _varIndex = 3, lvTracked = true, lvLRACandidate = true });

            var source = new GenTreeLclVar(TYP_STRUCT, 3) { IsContained = containedSource };
            if (!containedSource)
            {
                source.SetMultiReg();
                source.SetLastUse(0, true);
                source.SetLastUse(1, true);
            }

            var store = new GenTreeLclVar(TYP_STRUCT, 0, source);
            store.SetMultiReg();
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(containedSource ? 0 : 2));
            var firstDef = allocator.localVarIntervals![0]!.lastRefPosition
                ?? throw new AssertionException("The first field has no definition.");
            var secondDef = allocator.localVarIntervals[1]!.lastRefPosition
                ?? throw new AssertionException("The second field has no definition.");
            Assert.That(firstDef.nodeLocation, Is.EqualTo(5));
            Assert.That(secondDef.nodeLocation, Is.EqualTo(containedSource ? 5 : 7));
            Assert.That(LiveVariables(allocator)[0] & 3, Is.EqualTo((nint)3));
            if (!containedSource)
            {
                var firstUse = allocator.localVarIntervals[2]!.lastRefPosition;
                var secondUse = allocator.localVarIntervals[3]!.lastRefPosition;
                Assert.That(firstUse?.nodeLocation, Is.EqualTo(4));
                Assert.That(secondUse?.nodeLocation, Is.EqualTo(6));
                Assert.That(firstUse?.registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
                Assert.That(secondUse?.registerAssignment, Is.EqualTo(AvailableIntRegs(allocator)));
                Assert.That(firstUse?.getInterval().relatedInterval, Is.SameAs(allocator.localVarIntervals[0]));
                Assert.That(secondUse?.getInterval().relatedInterval, Is.SameAs(allocator.localVarIntervals[1]));
            }
        });
    }

    [Test]
    public static void EnregisterableSourceOfMultiRegisterStoreIsDelayFreedOnlyOnce()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaEnregMultiRegVars = true;
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16),
                    lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_LONG, _varIndex = 1, lvTracked = true, lvLRACandidate = true });
            var source = new GenTreeVecCon(TYP_SIMD16);
            source.SimdVal.u32[0] = 1;
            ReferenceBuildLocation(allocator) = 2;
            var sourceDefinition = BuildDef(allocator, source, SRBM_NONE, 0);
            var store = new GenTreeLclVar(TYP_STRUCT, 0, source);
            store.SetMultiReg();
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(1));
            Assert.That(sourceDefinition.nextRefPosition?.delayRegFree, Is.True);
            Assert.That(allocator.localVarIntervals![0]!.lastRefPosition?.nodeLocation, Is.EqualTo(5));
            Assert.That(allocator.localVarIntervals[1]!.lastRefPosition?.nodeLocation, Is.EqualTo(5));
        });
    }

    [TestCase(TYP_INT, SRBM_INTRET)]
    [TestCase(TYP_LONG, SRBM_LNGRET)]
    [TestCase(TYP_FLOAT, SRBM_FLOATRET)]
    [TestCase(TYP_DOUBLE, SRBM_DOUBLERET)]
    public static void PrimitiveReturnsUseTheirAbiRegisters(var_types type, regMask register)
    {
        WithAllocator((compiler, allocator) => {
            InitializeReturnDescriptor(compiler, type);
            GenTree source = varTypeIsFloating(type)
                ? compiler.gtNewDconNode(type, 3.5)
                : compiler.gtNewIconNode(type, 17);
            ReferenceBuildLocation(allocator) = 2;
            var definition = BuildDef(allocator, source, SRBM_NONE, 0);
            var ret = new GenTreeUnOp(GT_RETURN, type, source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildReturn(allocator, ret), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.registerAssignment, Is.EqualTo(register));
            Assert.That(allocator.refPositions.FindAll(reference => reference.refType is RefType.RefTypeKill), Is.Empty);
        });
    }

    [Test]
    public static void SwiftErrorReturnUsesItsSecondOperandAsTheReturnValue()
    {
        WithAllocator((compiler, allocator) => {
            InitializeReturnDescriptor(compiler, TYP_INT);
            compiler.info.compCallConv = CorInfoCallConvExtension.Swift;
            var error = compiler.gtNewIconNode(TYP_INT, 7);
            var value = compiler.gtNewIconNode(TYP_INT, 17);
            ReferenceBuildLocation(allocator) = 2;
            var definition = BuildDef(allocator, value, SRBM_NONE, 0);
            var ret = new GenTreeOp(GT_SWIFT_ERROR_RET, TYP_INT, error, value);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildReturn(allocator, ret), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_INTRET));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedAndVoidReturnsReserveOnlyTheirAbiKillRegisters(bool isVoid)
    {
        WithAllocator((compiler, allocator) => {
            InitializeReturnDescriptor(compiler, isVoid ? TYP_VOID : TYP_INT);
            var source = compiler.gtNewIconNode(TYP_INT, 17);
            source.IsContained = true;
            var ret = new GenTreeUnOp(GT_RETURN, isVoid ? TYP_VOID : TYP_INT, isVoid ? null : source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildReturn(allocator, ret), Is.Zero);
            var kills = allocator.refPositions.FindAll(reference => reference.refType is RefType.RefTypeKill);
            Assert.That(kills, Has.Count.EqualTo(isVoid ? 0 : 1));
            if (!isVoid)
            {
                Assert.That(kills[0].getKilledRegisters(), Is.EqualTo(new regMaskTP(SRBM_INTRET)));
                Assert.That(kills[0].nodeLocation, Is.EqualTo(5));
            }
        });
    }

    [Test]
    public static void SingleRegisterStructLocalReturnUsesTheIntegerAbiRegister()
    {
        WithAllocator((compiler, allocator) => {
            InitializeReturnDescriptor(compiler, TYP_LONG);
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(8) });
            var source = new GenTreeLclVar(TYP_STRUCT, 0);
            ReferenceBuildLocation(allocator) = 2;
            var definition = BuildDef(allocator, source, SRBM_NONE, 0);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildReturn(allocator, ret), Is.EqualTo(1));
            Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(definition.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_INTRET));
        });
    }

    [Test]
    public static void NonCandidateStoreOfMultiRegisterLocalUsesAllSourceFields()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaEnregMultiRegVars = true;
            SetLocals(compiler, allocator,
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16),
                    lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_LONG, _varIndex = 1, lvTracked = true, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16) });
            var source = new GenTreeLclVar(TYP_STRUCT, 0);
            source.SetMultiReg();
            var store = new GenTreeLclVar(TYP_STRUCT, 3, source);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildStoreLoc(allocator, store), Is.EqualTo(2));
            var firstUse = allocator.localVarIntervals![0]!.lastRefPosition;
            var secondUse = allocator.localVarIntervals[1]!.lastRefPosition;
            Assert.That(firstUse?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(secondUse?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(firstUse?.nodeLocation, Is.EqualTo(4));
            Assert.That(secondUse?.nodeLocation, Is.EqualTo(4));
        });
    }

    [Test]
    public static void ContainedFieldListReturnUsesItsAbiRegister()
    {
        WithAllocator((compiler, allocator) => {
            InitializeReturnDescriptor(compiler, TYP_LONG);
            var first = compiler.gtNewIconNode(TYP_INT, 17);
            ReferenceBuildLocation(allocator) = 2;
            var firstDefinition = BuildDef(allocator, first, SRBM_NONE, 0);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, first, 0, TYP_INT);
            fields.IsContained = true;
            var ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, fields);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildReturn(allocator, ret), Is.EqualTo(1));
            Assert.That(firstDefinition.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_INTRET));
            Assert.That(allocator.refPositions.FindAll(reference => reference.refType is RefType.RefTypeKill), Is.Empty);
        });
    }

    private static void SetLocals(Compiler compiler, LinearScan allocator, params LclVarDsc[] locals)
    {
        compiler.lvaTable = locals;
        compiler.lvaCount = locals.Length;
        var trackedCount = 0;
        foreach (var local in locals)
        {
            if (local.lvTracked)
            {
                trackedCount++;
            }
        }

        compiler.lvaTrackedCount = trackedCount;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        allocator.localVarIntervals = new Interval?[trackedCount];
        LiveVariables(allocator) = [0];
        for (var index = 0; index < locals.Length; index++)
        {
            if (!locals[index].lvTracked)
            {
                continue;
            }

            var registerType = locals[index].GetRegisterType();
            if (varTypeUsesFloatReg(registerType))
            {
                compiler.compFloatingPointUsed = true;
            }

            var candidates = varTypeUsesFloatReg(registerType)
                ? CompilerAllFloatRegs(compiler)
                : CompilerAllIntRegs(compiler);
            var interval = new Interval(registerType, candidates);
            interval.setLocalNumber(compiler, checked((uint)index), allocator);
        }
    }

    private static void InitializeReturnDescriptor(Compiler compiler, var_types type)
    {
        var descriptor = new ReturnTypeDesc();
        descriptor.InitializeReturnType(compiler, type, null, CorInfoCallConvExtension.Managed);
        compiler.compRetTypeDesc = descriptor;
        compiler.info.compCallConv = CorInfoCallConvExtension.Managed;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildStoreLoc")]
    private static extern int BuildStoreLoc(LinearScan allocator, GenTreeLclVarCommon store);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildReturn")]
    private static extern int BuildReturn(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentLiveVariables")]
    private static extern ref nint[] LiveVariables(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegs(LinearScan allocator);

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
