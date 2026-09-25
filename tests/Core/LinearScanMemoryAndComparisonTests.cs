// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.RmwStatus;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanMemoryAndComparisonTests
{
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_INT, true)]
    [TestCase(TYP_SIMD32, false)]
    [TestCase(TYP_SIMD32, true)]
    public static void LoadsPreserveAddressConstraintsAndVectorUsage(var_types type, bool evex)
    {
        WithAllocator((compiler, allocator) => {
            EnableAvx(compiler);
            ApxSupported(allocator) = true;
            EvexSupported(allocator) = evex;
            AvailableIntRegs(allocator) |= SRBM_R16;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var load = compiler.gtNewIndir(type, address);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildIndir(allocator, load), Is.EqualTo(1));
            Assert.That(addressDef.nextRefPosition?.registerAssignment,
                Is.EqualTo(evex ? AvailableIntRegs(allocator) : LowGprRegs(allocator)));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(load));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            var emitter = (compiler.codeGen ?? throw new AssertionException("Missing code generator.")).Emitter;
            Assert.That(emitter.ContainsAvxInstruction, Is.EqualTo(type == TYP_SIMD32));
            Assert.That(emitter.Contains256BitOrMoreAvxInstruction, Is.EqualTo(type == TYP_SIMD32));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StoresConsumeUncontainedDataWithoutDefiningAValue(bool containedData)
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var data = compiler.gtNewIconNode(TYP_INT, 23);
            data.IsContained = containedData;
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var dataDef = containedData ? null : BuildDef(allocator, data, SRBM_NONE, 0);
            var store = new GenTreeStoreInd(TYP_INT, address, data);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildIndir(allocator, store), Is.EqualTo(containedData ? 1 : 2));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(dataDef?.nextRefPosition?.refType,
                Is.EqualTo(containedData ? null : RefType.RefTypeUse));
            Assert.That(allocator.refPositions.Exists(reference =>
                ReferenceEquals(reference.treeNode, store) && reference.refType == RefType.RefTypeDef), Is.False);
        });
    }

    [TestCase(GT_ADD, 2)]
    [TestCase(GT_NOT, 1)]
    [TestCase(GT_LSH, 2)]
    public static void ReadModifyWriteStoresBuildContainedOperatorRequirements(genTreeOps operation, int sources)
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var containedAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            containedAddress.IsContained = true;
            var memory = compiler.gtNewIndir(TYP_INT, containedAddress);
            memory.IsContained = true;
            var data = compiler.gtNewIconNode(TYP_INT, 3);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, address, SRBM_NONE, 0);
            var dataDef = operation == GT_NOT ? null : BuildDef(allocator, data, SRBM_NONE, 0);
            var source = operation == GT_NOT
                ? new GenTreeUnOp(operation, TYP_INT, memory)
                : compiler.gtNewBinaryNode(operation, TYP_INT, memory, data);
            source.IsContained = true;
            var store = new GenTreeStoreInd(TYP_INT, address, source) {
                RmwStatus = STOREIND_RMW_DST_IS_OP1,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildIndir(allocator, store), Is.EqualTo(sources));
            if (operation == GT_LSH)
            {
                Assert.That(dataDef?.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RCX));
                Assert.That(allocator.refPositions.Exists(reference =>
                    reference.refType == RefType.RefTypeKill && reference.registerAssignment == SRBM_RCX), Is.True);
            }
            Assert.That(allocator.refPositions.Exists(reference =>
                (ReferenceEquals(reference.treeNode, store) || ReferenceEquals(reference.treeNode, source)) &&
                reference.refType == RefType.RefTypeDef), Is.False);
        });
    }

    [TestCase(GT_EQ, TYP_INT, false)]
    [TestCase(GT_EQ, TYP_INT, true)]
    [TestCase(GT_CMP, TYP_VOID, false)]
    [TestCase(GT_CMP, TYP_VOID, true)]
    public static void FloatingComparisonMemoryOperandsKeepLowGprConstraints(
        genTreeOps operation, var_types resultType, bool memoryFirst)
    {
        WithAllocator((compiler, allocator) => {
            ApxSupported(allocator) = true;
            EvexSupported(allocator) = true;
            AvailableIntRegs(allocator) |= SRBM_R16;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var value = compiler.gtNewDconNode(TYP_DOUBLE, 5.0);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var valueDef = BuildDef(allocator, value, SRBM_NONE, 0);
            var memory = compiler.gtNewIndir(TYP_DOUBLE, address);
            memory.IsContained = true;
            var compare = compiler.gtNewBinaryNode(operation, resultType,
                memoryFirst ? memory : value, memoryFirst ? value : memory);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCmp(allocator, compare), Is.EqualTo(2));
            Assert.That(addressDef.nextRefPosition?.registerAssignment, Is.EqualTo(LowGprRegs(allocator)));
            Assert.That(valueDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions.Exists(reference =>
                ReferenceEquals(reference.treeNode, compare) && reference.refType == RefType.RefTypeDef),
                Is.EqualTo(resultType != TYP_VOID));
        });
    }

    [TestCase(4080L, 4096L, false)]
    [TestCase(4081L, 4096L, true)]
    [TestCase(4096L, 4096L, true)]
    [TestCase(4096L, 65536L, false)]
    [TestCase(65535L, 65536L, true)]
    [TestCase(-1L, 4096L, false)]
    [TestCase(-16L, 4096L, true)]
    public static void ConstantLocalHeapRequirementsUseAlignedNativeSizeAndTargetPageSize(
        long sizeValue, long pageSize, bool needsTemporary)
    {
        WithAllocator((compiler, allocator) => {
            compiler.eeInfo.osPageSize = (nint)pageSize;
            var size = compiler.gtNewIconNode(TYP_I_IMPL, (nint)sizeValue);
            size.IsContained = true;
            var heap = new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, size);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildLclHeap(allocator, heap), Is.Zero);
            AssertHeapDefinitions(allocator, heap, needsTemporary);
            Assert.That(compiler.eeGetPageSize(), Is.EqualTo((nuint)pageSize));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UncontainedLocalHeapSizeNeedsACounterOnlyWithoutInitialization(bool initMem)
    {
        WithAllocator((compiler, allocator) => {
            compiler.info.compInitMem = initMem;
            var size = compiler.gtNewIconNode(TYP_I_IMPL, 128);
            ReferenceBuildLocation(allocator) = 2;
            var sizeDef = BuildDef(allocator, size, SRBM_NONE, 0);
            var heap = new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, size);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildLclHeap(allocator, heap), Is.EqualTo(1));
            Assert.That(sizeDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            AssertHeapDefinitions(allocator, heap, !initMem);
        });
    }

    private static void AssertHeapDefinitions(LinearScan allocator, GenTree heap, bool needsTemporary)
    {
        var temporaries = allocator.refPositions.FindAll(reference =>
            reference.treeNode == heap && reference.refType == RefType.RefTypeDef && reference.getInterval().isInternal);
        Assert.That(temporaries.Count, Is.EqualTo(needsTemporary ? 1 : 0));
        if (needsTemporary)
        {
            Assert.That(temporaries[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(temporaries[0].nextRefPosition?.nodeLocation, Is.EqualTo(4));
        }
        Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(heap));
        Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
        Assert.That(allocator.refPositions[^1].nodeLocation, Is.EqualTo(5));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildIndir")]
    private static extern int BuildIndir(LinearScan allocator, GenTreeIndir tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCmp")]
    private static extern int BuildCmp(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildLclHeap")]
    private static extern int BuildLclHeap(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_apxIsSupported")]
    private static extern ref bool ApxSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_evexIsSupported")]
    private static extern ref bool EvexSupported(LinearScan allocator);

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

    private static void EnableAvx(Compiler compiler)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX);
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
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
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
