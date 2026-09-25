// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenLocalHeapTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmd")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

    [TestCase(1, 0, false, 16)]
    [TestCase(15, 32, false, 16)]
    [TestCase(16, 32, true, 16)]
    [TestCase(17, 32, false, 32)]
    [TestCase(4080, 8192, false, 4080)]
    public static void SmallConstantsProbeBeforeSubtractingAndRetainOutgoingArea(
        int amount, int outgoing, bool initialize, int aligned)
    {
        WithHeap(outgoing, initialize, (compiler, codeGen) =>
        {
            var tree = Heap(compiler, amount, contained: true);
            codeGen.genLclHeap(tree);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo([INS_test, INS_sub, INS_lea]));
            Assert.That(ids[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(ids[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_SPBASE));
            Assert.That(ids[1].idReg1(), Is.EqualTo(REG_SPBASE));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[1]), Is.EqualTo((nint)aligned));
            Assert.That(Displacement(codeGen.Emitter, ids[2]), Is.EqualTo((nint)outgoing));
            Assert.That(codeGen.getCurrentStackLevel(), Is.Zero);
        });
    }

    [TestCase(4096L, false)]
    [TestCase(4096L, true)]
    [TestCase(4097L, false)]
    [TestCase(4294967295L, false)]
    public static void LargeConstantsMaterializeNativeWidthNegatedAlignedSizeAndProbeDynamically(
        long amount, bool initialize)
    {
        WithHeap(32, initialize, (compiler, codeGen) =>
        {
            var tree = Heap(compiler, (nint)amount, contained: true);
            codeGen.InternalRegisters.Add(tree, RBM_RDX);
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genLclHeap(tree);
            var ids = AllDescriptors(first, codeGen);

            var aligned = (amount + 15) & -16L;
            Assert.That(ids[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)(-aligned)));
            Assert.That(ids.Any(id => id.idIns() == INS_jb), Is.True);
            Assert.That(ids.Any(id => id.idIns() == INS_jae), Is.True);
            Assert.That(ids.Any(id => id.idIns() == INS_push_hide), Is.False);
            Assert.That(ids.Any(id => id.idIns() == INS_sub), Is.False);
            Assert.That(ids.Last().idIns(), Is.EqualTo(INS_lea));
            Assert.That(ids.Last().idAddr().iiaAddrMode.amDisp, Is.EqualTo(32));
        });
    }

    [TestCase(16, false, 16, 1)]
    [TestCase(4095, true, 4095, 1)]
    [TestCase(4096, false, 0, 2)]
    [TestCase(4097, true, 1, 2)]
    [TestCase(8192, true, 0, 3)]
    public static void ConstantProbeLoopsRetainExactPageTailTouches(
        int amount, bool track, int lastTouch, int probes)
    {
        WithHeap(0, false, (_, codeGen) =>
        {
            var result = codeGen.genStackPointerConstantAdjustmentLoopWithProbe(-amount, track);
            var ids = Descriptors(codeGen);

            Assert.That(result, Is.EqualTo((nint)lastTouch));
            Assert.That(ids.Count(id => id.idIns() == INS_test), Is.EqualTo(probes));
            Assert.That(ids.Where(id => id.idIns() == INS_sub)
                .Sum(id => (long)InstructionConstant(codeGen.Emitter, id)), Is.EqualTo(amount));
            Assert.That(ids.Any(id => id.idIns() == INS_sub_hide), Is.False);
            Assert.That(ids[0].idIns(), Is.EqualTo(INS_test));
            if (lastTouch == 0)
            {
                Assert.That(ids.Last().idIns(), Is.EqualTo(INS_test));
                Assert.That(ids.Last().idOpSize(), Is.EqualTo(EA_PTRSIZE));
                Assert.That(ids.Last().idReg1(), Is.EqualTo(REG_RAX));
            }
        });
    }

    [Test]
    public static void DynamicProbesClampWraparoundAndTouchBeforeEveryPageAdjustment()
    {
        WithHeap(0, false, (_, codeGen) =>
        {
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genStackPointerDynamicAdjustmentWithProbe(REG_RDX);
            var ids = AllDescriptors(first, codeGen);

            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(
            [
                INS_add, INS_jb, INS_xor, INS_test, INS_sub_hide, INS_cmp, INS_jae, INS_mov,
            ]));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(ids[0].idReg2(), Is.EqualTo(REG_SPBASE));
            Assert.That(ids[3].idAddr().iiaAddrMode.amDisp, Is.Zero);
            Assert.That(InstructionConstant(codeGen.Emitter, ids[4]), Is.EqualTo((nint)4096));
            Assert.That(ids[^1].idReg1(), Is.EqualTo(REG_SPBASE));
            Assert.That(ids[^1].idReg2(), Is.EqualTo(REG_RDX));
        });
    }

    [TestCase(TYP_INT, 0, false)]
    [TestCase(TYP_I_IMPL, 32, false)]
    [TestCase(TYP_INT, 32, true)]
    [TestCase(TYP_I_IMPL, 4096, true)]
    [TestCase(TYP_I_IMPL, 4112, true)]
    public static void DynamicAllocationsRetainWidthZeroBypassInitializationAndOutgoingRestoration(
        var_types sizeType, int outgoing, bool initialize)
    {
        WithHeap(outgoing, initialize, (compiler, codeGen) =>
        {
            var tree = Heap(compiler, 7, contained: false, sizeType);
            if (!initialize)
            {
                codeGen.InternalRegisters.Add(tree, RBM_RDX);
            }
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genLclHeap(tree);
            var ids = AllDescriptors(first, codeGen);
            var instructions = ids.Select(id => id.idIns()).ToArray();

            Assert.That(instructions[0], Is.EqualTo(INS_mov));
            Assert.That(instructions[1], Is.EqualTo(INS_test));
            Assert.That(ids[1].idOpSize(), Is.EqualTo(sizeType == TYP_INT ? EA_4BYTE : EA_8BYTE));
            Assert.That(instructions[2], Is.EqualTo(INS_je));
            var alignment = ids.First(id => (id.idIns() == INS_add) && (id.idReg1() != REG_SPBASE));
            Assert.That(alignment.idOpSize(), Is.EqualTo(sizeType == TYP_INT ? EA_4BYTE : EA_8BYTE));
            Assert.That(InstructionConstant(codeGen.Emitter, alignment), Is.EqualTo((nint)15));
            Assert.That(ids.Count(id => id.idIns() == INS_push_hide), Is.EqualTo(initialize ? 2 : 0));
            Assert.That(instructions.Contains(INS_neg), Is.EqualTo(!initialize));
            Assert.That(instructions.Contains(INS_shr_N), Is.EqualTo(initialize));
            Assert.That(instructions.Contains(INS_and), Is.EqualTo(!initialize));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
            Assert.That(codeGen.getCurrentStackLevel(), Is.Zero);
            Assert.That(ids.Last().idIns(), Is.EqualTo(INS_lea));
            Assert.That(ids.Last().idAddr().iiaAddrMode.amDisp, Is.EqualTo(outgoing));

            var restore = ids.Where(id => id.idIns() == INS_sub).ToArray();
            Assert.That(restore.Sum(id => (long)InstructionConstant(codeGen.Emitter, id)), Is.EqualTo(outgoing));
            if (initialize && (outgoing <= 4096))
            {
                Assert.That(instructions.Contains(INS_test) && ids.Count(id => id.idIns() == INS_test) == 1, Is.True);
            }
            if (initialize && (outgoing > 4096))
            {
                Assert.That(ids.Count(id => id.idIns() == INS_test), Is.EqualTo(3));
            }
        });
    }

    [Test]
    public static void InternalRegisterCountsRespectTheMaskAndMissingNode()
    {
        WithHeap(0, false, (compiler, codeGen) =>
        {
            var registers = new NodeInternalRegisters();
            var tree = Heap(compiler, 1, contained: true);
            Assert.That(registers.Count(tree), Is.Zero);
            registers.Add(tree, RBM_RAX | RBM_RDX);
            Assert.That(registers.Count(tree), Is.EqualTo(2u));
            Assert.That(registers.Count(tree, RBM_RAX), Is.EqualTo(1u));
            Assert.That(registers.Count(tree, RBM_RCX), Is.Zero);
#if HAS_MORE_THAN_64_REGISTERS
            registers.Add(tree, regMaskTP.CreateFromRegNum(REG_K1, REG_K1.SingleTypeMask));
            Assert.That(registers.Count(tree), Is.EqualTo(3u));
#endif
        });
    }

#if DEBUG
    [Test]
    public static void D005RejectsBeforeConsumptionOrInternalRegisterUse()
    {
        WithHeap(32, false, (compiler, codeGen) =>
        {
            var tree = Heap(compiler, 7, contained: false);
            codeGen.InternalRegisters.Add(tree, RBM_RDX);
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genLclHeap(tree));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.InternalRegisters.Count(tree), Is.EqualTo(1u));
            Assert.That(tree.Op1._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
        });
    }

    [Test]
    public static void DebugStackChecksPrecedeAllocationAndRecordTheNewSP()
    {
        WithHeap(0, false, (compiler, codeGen) =>
        {
            compiler.opts.compStackCheckOnRet = true;
            compiler.lvaReturnSpCheck = 0;
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genLclHeap(Heap(compiler, 16, contained: true));
            var ids = AllDescriptors(first, codeGen);
            Assert.That(ids.Any(id => id.idIns() == INS_int3), Is.True);
            Assert.That(ids.Last().idIns(), Is.EqualTo(INS_mov));
            Assert.That(ids.Last().idReg1(), Is.EqualTo(REG_SPBASE));
            Assert.That(ids.Last().idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(0));
        });
    }
#endif

    private static GenTreeUnOp Heap(Compiler compiler, nint amount, bool contained, var_types sizeType = TYP_I_IMPL)
    {
        var size = compiler.gtNewIconNode(sizeType, amount);
        size.IsContained = contained;
        size.RegNum = contained ? REG_NA : REG_RCX;

        return new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, size) { RegNum = REG_RAX };
    }

    private static void WithHeap(int outgoing, bool initialize, Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = true;
            compiler.info.compInitMem = initialize;
            compiler.eeInfo.osPageSize = 4096;
            compiler.lvaOutgoingArgSpaceSize.Value = outgoing;
            action(compiler, codeGen);
        });
    }

    internal static List<Emitter.instrDesc> AllDescriptors(insGroup? first, CodeGen codeGen)
    {
        var result = new List<Emitter.instrDesc>();
        var group = first;
        while (group != codeGen.Emitter.emitCurIG)
        {
            if (group is null)
            {
                throw new AssertionException("Missing current instruction group.");
            }
            if (group.igData is not null)
            {
                result.AddRange(group.igData);
            }
            group = group.igNext;
        }
        result.AddRange(Descriptors(codeGen));

        return result;
    }
}
