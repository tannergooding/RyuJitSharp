// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanBlockAndStackArgumentTests
{
    [TestCase(true, GenTreeBlk.BlkOpKindUnroll, 8, 0, 0)]
    [TestCase(true, GenTreeBlk.BlkOpKindUnroll, 16, 0, 1)]
    [TestCase(true, GenTreeBlk.BlkOpKindLoop, 8, 1, 0)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnroll, 4, 1, 0)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnroll, 16, 0, 1)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnroll, 19, 0, 1)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnroll, 20, 1, 1)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnroll, 24, 1, 1)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnrollMemmove, 1, 1, 0)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnrollMemmove, 3, 2, 0)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnrollMemmove, 16, 0, 1)]
    [TestCase(false, GenTreeBlk.BlkOpKindUnrollMemmove, 33, 0, 3)]
    public static void BlockOperationKindsReserveNativeInternalRegisterCounts(
        bool initialize, GenTreeBlk.BlkOpKind kind, int size, int integerTemps, int floatTemps)
    {
        WithAllocator((compiler, allocator) => {
            var destinationAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var sourceAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            ReferenceBuildLocation(allocator) = 2;
            var destinationDef = BuildDef(allocator, destinationAddress, SRBM_NONE, 0);
            var sourceDef = initialize ? null : BuildDef(allocator, sourceAddress, SRBM_NONE, 0);

            var layout = new ClassLayout(size);
            GenTree source;
            if (initialize)
            {
                source = compiler.gtNewIconNode(TYP_INT, 0);
                source.IsContained = true;
            }
            else
            {
                source = compiler.gtNewIndir(TYP_STRUCT, sourceAddress);
                source.IsContained = true;
            }

            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, source, layout) { _kind = kind };
            ReferenceBuildLocation(allocator) = 4;
            var initialCount = allocator.refPositions.Count;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(initialize ? 1 : 2));
            Assert.That(destinationDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(sourceDef?.nextRefPosition?.refType,
                Is.EqualTo(initialize ? null : RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, block, TYP_INT), Has.Count.EqualTo(integerTemps));
            Assert.That(InternalDefinitions(allocator, block, TYP_FLOAT), Has.Count.EqualTo(floatTemps));
            Assert.That(allocator.refPositions.FindAll(
                reference => reference.refType is RefType.RefTypeKill &&
                    reference.treeNode == block), Is.Empty);
            foreach (var definition in InternalDefinitions(allocator, block))
            {
                Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            }

            Assert.That(allocator.refPositions[initialCount].refType,
                Is.EqualTo((integerTemps + floatTemps) > 0 ? RefType.RefTypeDef : RefType.RefTypeUse));
        });
    }

    [TestCase(0, true, true)]
    [TestCase(2, true, false)]
    [TestCase(2, false, true)]
    public static void HeapGcZeroingRequiresMultipleWholeNonGcVectorsForSimd(
        int gcSlot, bool heapDestination, bool expectSimd)
    {
        WithAllocator((compiler, allocator) => {
            var builder = new ClassLayoutBuilder(compiler, gcSlot == 0 ? 48 : 32);
            builder.SetGCPtrType(gcSlot, TYP_REF);
            var layout = ClassLayout.Create(compiler, builder);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, address, SRBM_NONE, 0);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            zero.IsContained = true;
            var block = new GenTreeBlk(TYP_STRUCT, address, zero, layout) {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            if (!heapDestination)
            {
                block.Flags |= GTF_IND_TGT_NOT_HEAP;
            }

            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(1));
            Assert.That(InternalDefinitions(allocator, block, TYP_FLOAT), Has.Count.EqualTo(expectSimd ? 1 : 0));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void BlockFillRestrictsApxAddressRegistersOnlyWithoutEvex(bool apx, bool evex)
    {
        WithAllocator((compiler, allocator) => {
            ApxSupported(allocator) = apx;
            EvexSupported(allocator) = evex;
            if (apx)
            {
                AvailableIntRegs(allocator) |= SRBM_R16;
            }

            var destinationAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var fill = compiler.gtNewIconNode(TYP_INT, 0x5A);
            ReferenceBuildLocation(allocator) = 2;
            var destinationDef = BuildDef(allocator, destinationAddress, SRBM_NONE, 0);
            var fillDef = BuildDef(allocator, fill, SRBM_NONE, 0);
            var initValue = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, fill) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, initValue, new ClassLayout(8)) {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(2));
            var expected = apx && !evex ? LowGprRegs(allocator) : AvailableIntRegs(allocator);
            Assert.That(destinationDef.nextRefPosition?.registerAssignment, Is.EqualTo(expected));
            Assert.That(fillDef.nextRefPosition?.registerAssignment, Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedBlockAddressModesUseTheirBasesAndIndicesInOrder(bool evex)
    {
        WithAllocator((compiler, allocator) => {
            ApxSupported(allocator) = true;
            EvexSupported(allocator) = evex;
            AvailableIntRegs(allocator) |= SRBM_R16;
            var destinationBase = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var destinationIndex = compiler.gtNewIconNode(TYP_I_IMPL, 2);
            var sourceBase = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            var sourceIndex = compiler.gtNewIconNode(TYP_I_IMPL, 3);
            ReferenceBuildLocation(allocator) = 2;
            var destinationBaseDef = BuildDef(allocator, destinationBase, SRBM_NONE, 0);
            var destinationIndexDef = BuildDef(allocator, destinationIndex, SRBM_NONE, 0);
            var sourceBaseDef = BuildDef(allocator, sourceBase, SRBM_NONE, 0);
            var sourceIndexDef = BuildDef(allocator, sourceIndex, SRBM_NONE, 0);

            var destination = new GenTreeAddrMode(TYP_I_IMPL, destinationBase, destinationIndex, 1, 8) {
                IsContained = true,
            };
            var sourceAddress = new GenTreeAddrMode(TYP_I_IMPL, sourceBase, sourceIndex, 1, 8) {
                IsContained = true,
            };
            var source = compiler.gtNewIndir(TYP_STRUCT, sourceAddress);
            source.IsContained = true;
            var block = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout(4)) {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(4));
            var useDefinitions = new[] {
                destinationBaseDef, destinationIndexDef, sourceBaseDef, sourceIndexDef,
            };
            var expected = evex ? AvailableIntRegs(allocator) : LowGprRegs(allocator);
            for (var index = 0; index < useDefinitions.Length; index++)
            {
                var use = useDefinitions[index].nextRefPosition
                    ?? throw new AssertionException("An address component was not consumed.");
                Assert.That(use.registerAssignment, Is.EqualTo(expected));
                if (index > 0)
                {
                    var precedingUse = useDefinitions[index - 1].nextRefPosition
                        ?? throw new AssertionException("The preceding address component was not consumed.");
                    Assert.That(allocator.refPositions.IndexOf(precedingUse),
                        Is.LessThan(allocator.refPositions.IndexOf(use)));
                }
            }
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void BlockInitializationMarksTheActualAvxWidth(bool zeroFill, bool wideInstruction)
    {
        WithAllocator((compiler, allocator) => {
            EnableInstructionSet(compiler, InstructionSet_AVX);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, address, SRBM_NONE, 0);
            var fill = compiler.gtNewIconNode(TYP_INT, zeroFill ? 0 : 0x5A);
            if (zeroFill)
            {
                fill.IsContained = true;
            }
            else
            {
                _ = BuildDef(allocator, fill, SRBM_NONE, 0);
            }

            var source = zeroFill ? (GenTree)fill : new GenTreeUnOp(GT_INIT_VAL, TYP_INT, fill) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, address, source, new ClassLayout(32)) {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(zeroFill ? 1 : 2));
            Assert.That(InternalDefinitions(allocator, block, TYP_FLOAT), Has.Count.EqualTo(1));
            var emitter = (compiler.codeGen ?? throw new AssertionException("Code generation was not initialized.")).Emitter;
            Assert.That(emitter.ContainsAvxInstruction, Is.True);
            Assert.That(emitter.Contains256BitOrMoreAvxInstruction, Is.EqualTo(wideInstruction));
        });
    }

    [TestCase(false, false, 1, 1)]
    [TestCase(true, false, 0, 1)]
    [TestCase(false, true, 0, 5)]
    [TestCase(true, true, 0, 3)]
    public static void CopyAndMemmoveTempsFollowTheSelectedSimdWidth(
        bool avx, bool memmove, int integerTemps, int floatTemps)
    {
        WithAllocator((compiler, allocator) => {
            if (avx)
            {
                EnableInstructionSet(compiler, InstructionSet_AVX);
            }

            var destinationAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var sourceAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, destinationAddress, SRBM_NONE, 0);
            _ = BuildDef(allocator, sourceAddress, SRBM_NONE, 0);
            var size = memmove ? 65 : 52;
            var source = compiler.gtNewIndir(TYP_STRUCT, sourceAddress);
            source.IsContained = true;
            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, source, new ClassLayout(size)) {
                _kind = memmove ? GenTreeBlk.BlkOpKindUnrollMemmove : GenTreeBlk.BlkOpKindUnroll,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(2));
            Assert.That(InternalDefinitions(allocator, block, TYP_INT), Has.Count.EqualTo(integerTemps));
            Assert.That(InternalDefinitions(allocator, block, TYP_FLOAT), Has.Count.EqualTo(floatTemps));
            var emitter = (compiler.codeGen ?? throw new AssertionException("Code generation was not initialized.")).Emitter;
            Assert.That(emitter.ContainsAvxInstruction, Is.EqualTo(avx));
            Assert.That(emitter.Contains256BitOrMoreAvxInstruction, Is.EqualTo(avx));
        });
    }

    [TestCase(true, 0)]
    [TestCase(false, 1)]
    public static void HighBitBlockSizesRetainUnsignedSimdAndRemainderArithmetic(
        bool initialize, int integerTemps)
    {
        WithAllocator((compiler, allocator) => {
            const uint size = 0x8000_0014;
            var destinationAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var sourceValue = compiler.gtNewIconNode(TYP_INT, 0x5A);
            var sourceAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, destinationAddress, SRBM_NONE, 0);
            _ = initialize
                ? BuildDef(allocator, sourceValue, SRBM_NONE, 0)
                : BuildDef(allocator, sourceAddress, SRBM_NONE, 0);

            GenTree source;
            if (initialize)
            {
                source = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, sourceValue) { IsContained = true };
            }
            else
            {
                source = compiler.gtNewIndir(TYP_STRUCT, sourceAddress);
                source.IsContained = true;
            }

            var block = new GenTreeBlk(TYP_STRUCT, destinationAddress, source, new ClassLayout(size)) {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildBlockStore(allocator, block), Is.EqualTo(2));
            Assert.That(InternalDefinitions(allocator, block, TYP_INT), Has.Count.EqualTo(integerTemps));
            Assert.That(InternalDefinitions(allocator, block, TYP_FLOAT), Has.Count.EqualTo(1));
        });
    }

    [TestCase(GenTreePutArgStk.Kind.Unroll, 8, 8, false, 1, 0)]
    [TestCase(GenTreePutArgStk.Kind.Unroll, 16, 16, false, 0, 1)]
    [TestCase(GenTreePutArgStk.Kind.Unroll, 24, 17, true, 1, 1)]
    [TestCase(GenTreePutArgStk.Kind.Unroll, 32, 32, true, 0, 1)]
    [TestCase(GenTreePutArgStk.Kind.RepInstr, 24, 24, false, 3, 0)]
    [TestCase(GenTreePutArgStk.Kind.PartialRepInstr, 24, 17, true, 3, 0)]
    public static void StructStackArgumentsReserveNativeTempsAndConsumeTheSourceAddress(
        GenTreePutArgStk.Kind kind, int stackSize, int loadSize, bool incomingArea, int integerTemps, int floatTemps)
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var block = new GenTreeBlk(TYP_STRUCT, address, new ClassLayout(stackSize)) { IsContained = true };
            var putArg = new GenTreePutArgStk(TYP_VOID, block, null, 0, stackSize, incomingArea) {
                _kind = kind,
                ArgLoadSize = loadSize,
            };
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildPutArgStk(allocator, putArg), Is.EqualTo(1));
            var addressUse = addressDef.nextRefPosition;
            Assert.That(addressUse?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, putArg, TYP_INT), Has.Count.EqualTo(integerTemps));
            Assert.That(InternalDefinitions(allocator, putArg, TYP_FLOAT), Has.Count.EqualTo(floatTemps));
            foreach (var definition in InternalDefinitions(allocator, putArg))
            {
                Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
                Assert.That(definition.nextRefPosition?.nodeLocation, Is.EqualTo(4));
            }

            if (kind is GenTreePutArgStk.Kind.RepInstr or GenTreePutArgStk.Kind.PartialRepInstr)
            {
                var fixedRegisters = InternalDefinitions(allocator, putArg, TYP_INT);
                regMask[] expected = [SRBM_RDI, SRBM_RCX, SRBM_RSI];
                Assert.That(fixedRegisters.ConvertAll(definition => definition.registerAssignment),
                    Is.EqualTo(expected));
                var fixedUse = fixedRegisters[0].nextRefPosition
                    ?? throw new AssertionException("The first fixed-register temporary has no use.");
                Assert.That(allocator.refPositions.IndexOf(addressUse
                    ?? throw new AssertionException("The source address has no use.")),
                    Is.LessThan(allocator.refPositions.IndexOf(fixedUse)));
            }
        });
    }

    [Test]
    public static void Simd12FieldListSharesOneInternalFloatRegister()
    {
        WithAllocator((compiler, allocator) => {
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            ReferenceBuildLocation(allocator) = 2;
            var firstDef = BuildDef(allocator, first, SRBM_NONE, 0);
            var secondDef = BuildDef(allocator, second, SRBM_NONE, 0);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, first, 0, TYP_SIMD12);
            fields.AddFieldLIR(compiler, second, 12, TYP_SIMD12);
            var putArg = new GenTreePutArgStk(TYP_VOID, fields, null, 0, 32, false);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildPutArgStk(allocator, putArg), Is.EqualTo(2));
            Assert.That(firstDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(secondDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            var internalDefinitions = InternalDefinitions(allocator, putArg, TYP_FLOAT);
            Assert.That(internalDefinitions, Has.Count.EqualTo(1));
            Assert.That(internalDefinitions[0].registerAssignment, Is.EqualTo(AvailableFloatRegs(allocator)));
            Assert.That(internalDefinitions[0].nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    [Test]
    public static void ScalarStackArgumentUsesContainedMemoryAddressWithoutInternalTemp()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var load = compiler.gtNewIndir(TYP_INT, address);
            load.IsContained = true;
            var putArg = new GenTreePutArgStk(TYP_VOID, load, null, 0, 8, false);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildPutArgStk(allocator, putArg), Is.EqualTo(1));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, putArg), Is.Empty);
        });
    }

    private static System.Collections.Generic.List<RefPosition> InternalDefinitions(
        LinearScan allocator, GenTree tree, var_types? type = null)
        => allocator.refPositions.FindAll(reference =>
            reference.treeNode == tree &&
            reference.refType is RefType.RefTypeDef &&
            reference.getInterval().isInternal &&
            ((type is null) || (reference.getInterval().registerType == type)));

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildBlockStore")]
    private static extern int BuildBlockStore(LinearScan allocator, GenTreeBlk block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPutArgStk")]
    private static extern int BuildPutArgStk(LinearScan allocator, GenTreePutArgStk argument);

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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lowGprRegs")]
    private static extern ref regMask LowGprRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrash(Compiler compiler);

    private static void EnableInstructionSet(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
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
