// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeDebugFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanFinalVerificationTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void ReplayReconstructsAndReleasesAssignments(bool copy, bool move)
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            var interval = NewInterval(allocator);
            var definition = AddReference(allocator, RefType.RefTypeDef, 2, interval, SRBM_RAX);
            var use = AddReference(allocator, RefType.RefTypeUse, 4, interval, copy || move ? SRBM_RBX : SRBM_RAX);
            use.copyReg = copy;
            use.moveReg = move;
            if (copy)
            {
                _ = AddReference(allocator, RefType.RefTypeUse, 6, interval, SRBM_RAX);
                allocator.refPositions[^1].lastUse = true;
            }
            else
            {
                use.lastUse = true;
            }

            interval.physReg = regNumber.REG_RDX;
            interval.assignedReg = allocator.physRegs[(int)regNumber.REG_RDX];
            allocator.physRegs[(int)regNumber.REG_RDX].assignedInterval = interval;
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = interval;

            VerifyFinalAllocation(allocator);

            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(interval.recentRefPosition, Is.SameAs(allocator.refPositions[^1]));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(interval.assignedReg, Is.Null);
            Assert.That(interval.isActive, Is.False);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.Null);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RBX].assignedInterval, Is.Null);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RDX].assignedInterval, Is.Null);
            Assert.That(block, Is.SameAs(compiler.fgFirstBB));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SpillAndReloadReplayRespectWriteThroughLifetime(bool writeThrough)
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            var interval = NewInterval(allocator);
            var definition = AddReference(allocator, RefType.RefTypeDef, 2, interval, SRBM_RAX);
            definition.spillAfter = true;
            definition.writeThru = writeThrough;
            var use = AddReference(allocator, RefType.RefTypeUse, 4, interval, SRBM_RBX);
            use.reload = true;
            use.lastUse = true;
            compiler.verbose = true;

            var output = Capture(() => VerifyFinalAllocation(allocator));

            Assert.That(output, Does.Contain("Final allocation"));
            Assert.That(output, Does.Contain(writeThrough ? "WThru" : "Spill"));
            Assert.That(output, Does.Contain("ReLod"));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(interval.assignedReg, Is.Null);
            Assert.That(interval.isActive, Is.False);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.Null);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RBX].assignedInterval, Is.Null);
        });
    }

    [Test]
    public static void BlockRowsUseNativeColumnsAndNodeLocationWidth()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            _ = AddReference(allocator, RefType.RefTypeBB, 10);
            MaxNodeLocation(allocator) = 8;
            compiler.verbose = true;

            var output = Capture(() => VerifyFinalAllocation(allocator));

            Assert.That(output, Does.Contain($"         0.#0 BB1 PredBB0{new string(' ', 15)}"));
            Assert.That(output, Does.Not.Contain("<-"));
        });
    }

    [Test]
    public static void FixedReferencesKillsAndOptionalUsesPreserveReplayOrdering()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            var interval = NewInterval(allocator);
            var definition = AddReference(allocator, RefType.RefTypeDef, 2, interval, SRBM_RAX);
            var fixedRegister = AddReference(allocator, RefType.RefTypeFixedReg, 4);
            fixedRegister.setReg(allocator.physRegs[(int)regNumber.REG_RBX]);
            var killed = AddReference(allocator, RefType.RefTypeKill, 6);
            killed.killedRegisters = RBM_RBX;
            var gcKill = AddReference(allocator, RefType.RefTypeKillGCRefs, 8);
            gcKill.registerAssignment = SRBM_RAX;
            var optional = AddReference(allocator, RefType.RefTypeUse, 10, interval);
            optional.regOptional = true;
            optional.lastUse = true;

            VerifyFinalAllocation(allocator);

            Assert.That(fixedRegister.getReg().recentRefPosition, Is.SameAs(fixedRegister));
            Assert.That(interval.recentRefPosition, Is.SameAs(optional));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(definition.nodeLocation, Is.EqualTo(2u));
        });
    }

    [Test]
    public static void BlockBoundariesClearPhysicalRegistersBeforeContinuing()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 2);
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            var first = NewInterval(allocator);
            _ = AddReference(allocator, RefType.RefTypeDef, 2, first, SRBM_RAX);
            _ = AddReference(allocator, RefType.RefTypeBB, 4);
            var second = NewInterval(allocator);
            var definition = AddReference(allocator, RefType.RefTypeDef, 6, second, SRBM_RAX);
            definition.lastUse = true;

            VerifyFinalAllocation(allocator);

            Assert.That(first.physReg, Is.EqualTo(regNumber.REG_RAX));
            Assert.That(first.assignedReg, Is.SameAs(allocator.physRegs[(int)regNumber.REG_RAX]));
            Assert.That(second.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.Null);
        });
    }

    [Test]
    public static void AvailableHighVectorRegistersAreResetUsingThePhysicalRegisterMap()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            var interval = NewInterval(allocator);
            interval.physReg = regNumber.REG_XMM16;
            interval.assignedReg = allocator.physRegs[(int)regNumber.REG_XMM16];
            allocator.physRegs[(int)regNumber.REG_XMM16].assignedInterval = interval;

            VerifyFinalAllocation(allocator);

            Assert.That(allocator.physRegs[(int)regNumber.REG_XMM16].assignedInterval, Is.Null);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(interval.assignedReg, Is.Null);
        }, evex: true);
    }

    [Test]
    public static void OptionalDefinitionAndSpecialPutArgRetainTheirNativeReplayPaths()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            var interval = NewInterval(allocator);
            var definition = AddReference(allocator, RefType.RefTypeDef, 2, interval, SRBM_RAX);
            var optionalDefinition = AddReference(allocator, RefType.RefTypeDef, 4, interval);
            optionalDefinition.regOptional = true;
            var special = NewInterval(allocator);
            special.isSpecialPutArg = true;
            _ = AddReference(allocator, RefType.RefTypeUse, 6, special, SRBM_R15);
            compiler.verbose = true;

            var output = Capture(() => VerifyFinalAllocation(allocator));

            Assert.That(output, Does.Contain("NoReg"));
            Assert.That(output, Does.Contain("PtArg    r15"));
            Assert.That(definition.nodeLocation, Is.EqualTo(2u));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.Null);
            Assert.That(special.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(allocator.physRegs[(int)regNumber.REG_R15].assignedInterval, Is.Null);
        });
    }

    [Test]
    public static void ACopyThatSpillsItsHomeRegisterClearsBothAssignments()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            _ = StartBlockSequence(allocator);
            _ = AddReference(allocator, RefType.RefTypeBB, 0);
            var interval = NewInterval(allocator);
            _ = AddReference(allocator, RefType.RefTypeDef, 2, interval, SRBM_RAX);
            var copy = AddReference(allocator, RefType.RefTypeUse, 4, interval, SRBM_RBX);
            copy.copyReg = true;
            copy.spillAfter = true;

            VerifyFinalAllocation(allocator);

            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(interval.assignedReg, Is.Null);
            Assert.That(interval.isActive, Is.False);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.Null);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RBX].assignedInterval, Is.Null);
        });
    }

    [Test]
    public static void ResolutionBlocksAreRejectedBeforeReplayClearsAssignments()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            _ = StartBlockSequence(allocator);
            BbNumMaxBeforeResolution(allocator) = (uint)blocks[0].bbNum;
            var interval = NewInterval(allocator);
            interval.physReg = regNumber.REG_RAX;

            _ = Assert.Throws<FatalJitException>(() => VerifyFinalAllocation(allocator));

            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_RAX));
        });
    }

    [TestCase(genTreeOps.GT_LCL_VAR, false)]
    [TestCase(genTreeOps.GT_LCL_VAR, true)]
    [TestCase(genTreeOps.GT_COPY, false)]
    [TestCase(genTreeOps.GT_COPY, true)]
    [TestCase(genTreeOps.GT_SWAP, false)]
    public static void ResolutionMoveRequiresLsraOriginAndNativeOperatorShape(genTreeOps op, bool unused)
    {
        WithAllocator((compiler, allocator) => {
            var local = new GenTreeLclVar(TYP_INT, 0);
            GenTree node = op switch
            {
                genTreeOps.GT_LCL_VAR => local,
                genTreeOps.GT_COPY => new GenTreeCopyOrReload(op, TYP_INT, local),
                _ => new GenTreeOp(genTreeOps.GT_SWAP, TYP_VOID, local, new GenTreeLclVar(TYP_INT, 1)),
            };
            node._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
            if (op is not genTreeOps.GT_SWAP)
            {
                node.IsUnusedValue = unused;
            }

            Assert.That(IsResolutionMove(null, node), Is.EqualTo(op is genTreeOps.GT_SWAP || unused));
            node._debugFlags &= ~GTF_DEBUG_NODE_LSRA_ADDED;
            Assert.That(IsResolutionMove(null, node), Is.False);
        });
    }

    [Test]
    public static void AddedResolutionOperandsAreRecognizedAndRejectedBeforeReplayMutation()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            _ = StartBlockSequence(allocator);
            var operand = new GenTreeLclVar(TYP_INT, 0);
            operand._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
            var copy = new GenTreeCopyOrReload(genTreeOps.GT_COPY, TYP_INT, operand);
            copy._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
            copy.IsUnusedValue = true;
            block.InsertAtEnd(operand);
            block.InsertAtEnd(copy);
            var interval = NewInterval(allocator);
            interval.physReg = regNumber.REG_RAX;

            Assert.That(IsResolutionNode(null, block, operand), Is.True);
            _ = Assert.Throws<FatalJitException>(() => VerifyFinalAllocation(allocator));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_RAX));
        });
    }

    [Test]
    public static void OptimizedModeIsRejectedBeforeClearingIntervalAssignments()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator);
            interval.physReg = regNumber.REG_RAX;
            EnregisterLocalVars(allocator) = true;

            _ = Assert.Throws<FatalJitException>(() => VerifyFinalAllocation(allocator));

            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_RAX));
        });
    }

    private static Interval NewInterval(LinearScan allocator)
    {
        var interval = new Interval(TYP_INT, SRBM_ALLINT_INIT) {
            intervalIndex = (uint)allocator.intervals.Count,
        };
        allocator.intervals.Add(interval);
        return interval;
    }

    private static RefPosition AddReference(LinearScan allocator, RefType type, uint location,
        Interval? interval = null, regMask assignment = SRBM_NONE)
    {
        var reference = new RefPosition(1, location, null, type) {
            registerAssignment = assignment,
            rpNum = (uint)allocator.refPositions.Count,
        };
        if (interval is not null)
        {
            reference.setInterval(interval);
        }
        allocator.refPositions.Add(reference);
        return reference;
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index != 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = count;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgPredsComputed = true;
        return blocks;
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "verifyFinalAllocationMinimal")]
    private static extern void VerifyFinalAllocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isResolutionMove")]
    private static extern bool IsResolutionMove(LinearScan? _, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isResolutionNode")]
    private static extern bool IsResolutionNode(LinearScan? _, BasicBlock block, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "startBlockSequence")]
    private static extern BasicBlock StartBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocalVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_maxNodeLocation")]
    private static extern ref uint MaxNodeLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool evex = false)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        if (evex)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
        }
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
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
