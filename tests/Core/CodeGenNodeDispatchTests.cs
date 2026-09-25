// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenNodeDispatchTests
{
    [TestCase(GT_NEG, INS_neg)]
    [TestCase(GT_NOT, INS_not)]
    [TestCase(GT_BSWAP, INS_bswap)]
    public static void UnaryNodesUseTheManagedUnaryRepresentation(genTreeOps oper, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeUnOp(oper, TYP_INT, Register(compiler, TYP_INT, REG_RAX)) { RegNum = REG_RAX };
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo([expected]));
        });
    }

    [TestCase(GT_ADD, INS_add)]
    [TestCase(GT_SUB, INS_sub)]
    [TestCase(GT_OR, INS_or)]
    [TestCase(GT_XOR, INS_xor)]
    [TestCase(GT_AND, INS_and)]
    [TestCase(GT_MUL, INS_imul)]
    public static void BinaryNodesDispatchToTheNativeOperation(genTreeOps oper, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(oper, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RAX,
            };
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo([expected]));
        });
    }

    [TestCase(GT_DIV, TYP_FLOAT, INS_divss)]
    [TestCase(GT_DIV, TYP_DOUBLE, INS_divsd)]
    [TestCase(GT_MUL, TYP_FLOAT, INS_mulss)]
    [TestCase(GT_MUL, TYP_DOUBLE, INS_mulsd)]
    public static void FloatingDivisionAndMultiplicationUseTheBinaryGenerator(
        genTreeOps oper, var_types type, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var first = compiler.gtNewDconNode(type, 1);
            first.RegNum = REG_XMM0;
            var second = compiler.gtNewDconNode(type, 2);
            second.RegNum = REG_XMM1;
            var tree = new GenTreeOp(oper, type, first, second) { RegNum = REG_XMM0 };
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo([expected]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedConstantsAreSkippedButReuseIsProcessedFirst(bool reuse)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewIconNode(TYP_INT, 0);
            tree.IsContained = true;
            tree.IsReuseRegVal = reuse;
            codeGen.instGen(INS_nop);
            var before = codeGen.Emitter.emitCurIG;
            codeGen.genCodeForTreeNode(tree);

            Assert.That(ReferenceEquals(before, codeGen.Emitter.emitCurIG), Is.EqualTo(!reuse));
            Assert.That(CodeGenLocalHeapTests.AllDescriptors(before, codeGen).Select(id => id.idIns()),
                Is.EqualTo([INS_nop]));
        });
    }

    [Test]
    public static void ConstantsProduceTheirAllocatedRegister()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewIconNode(TYP_INT, 17);
            tree.RegNum = REG_RAX;
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RAX);
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_mov));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
#if DEBUG
            Assert.That(tree._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED));
#endif
        });
    }

    [TestCase(GT_EQ, INS_sete)]
    [TestCase(GT_NE, INS_setne)]
    public static void ComparisonsConsumeTheirOperandsBeforeGeneratingTheCondition(genTreeOps oper, instruction set)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var first = new GenTreePhysReg(REG_RAX, TYP_REF) { RegNum = REG_RAX };
            var second = new GenTreePhysReg(REG_RCX, TYP_REF) { RegNum = REG_RCX };
            var tree = new GenTreeOp(oper, TYP_INT, first, second) { RegNum = REG_RDX };
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RAX | RBM_RCX);
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_cmp, set, INS_movzx]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
#if DEBUG
            Assert.That(first._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(second._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
#endif
        });
    }

    [TestCase(GT_COPY)]
    [TestCase(GT_RELOAD)]
    [TestCase(GT_NOP)]
    [TestCase(GT_IL_OFFSET)]
    public static void MarkersDoNotGenerateOrConsumeOperands(genTreeOps oper)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreePhysReg(REG_RAX, TYP_REF) { RegNum = REG_RAX };
            var tree = oper switch
            {
                GT_COPY or GT_RELOAD => new GenTreeCopyOrReload(oper, TYP_REF, operand) { RegNum = REG_RCX },
                GT_IL_OFFSET => new GenTreeILOffset(default),
                _ => new GenTree(oper, TYP_VOID),
            };
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RAX);
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
        });
    }

    [Test]
    public static void KeepAliveConsumesItsOperandWithoutAnInstruction()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreePhysReg(REG_RAX, TYP_REF) { RegNum = REG_RAX };
            var tree = new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, operand);
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RAX);
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void NoOpEmitsAnInstructionRatherThanAMarker()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.genCodeForTreeNode(new GenTree(GT_NO_OP, TYP_VOID));

            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_nop));
            Assert.That(Descriptors(codeGen).Single().idCodeSize(), Is.EqualTo(1));
        });
    }

    [TestCase(GTF_EMPTY, 2)]
    [TestCase(GTF_MEMORYBARRIER_LOAD, 0)]
    [TestCase(GTF_MEMORYBARRIER_STORE, 0)]
    [TestCase(GTF_MEMORYBARRIER_LOAD | GTF_MEMORYBARRIER_STORE, 0)]
    public static void MemoryBarriersPreserveNativeFlagSelection(GenTreeFlags flags, int count)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.genCodeForTreeNode(new GenTree(GT_MEMORYBARRIER, TYP_VOID) { Flags = flags });

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(count));
            if (count != 0)
            {
                Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_lock, INS_or]));
            }
        });
    }

    [Test]
    public static void PreemptiveGcKillsCalleeSavedRootsAndPublishesTheBoundary()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RAX | RBM_RBX);
            codeGen.GCInfo.gcMarkRegSetByref(RBM_RCX | RBM_RSI);
            codeGen.instGen(INS_nop);
            var before = codeGen.Emitter.emitCurIG;
            codeGen.genCodeForTreeNode(new GenTree(GT_START_PREEMPTGC, TYP_VOID));

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_RCX));
            Assert.That(codeGen.Emitter.emitCurIG, Is.Not.SameAs(before));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [Test]
    public static void NonGcMarkersStartANonInterruptibleGroup()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.genCodeForTreeNode(new GenTree(GT_START_NONGC, TYP_VOID));

            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            Assert.That(group.igFlags & InsGroupFlags.NoGCInterrupt, Is.EqualTo(InsGroupFlags.NoGCInterrupt));
            codeGen.Emitter.emitEnableGC();
        });
    }

    [Test]
    public static void LabelsRemainPendingUntilTheCorrespondingCallReturns()
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            codeGen.genCodeForTreeNode(new GenTree(GT_LABEL, TYP_I_IMPL) { RegNum = REG_R10 });
            var label = PendingLabel(codeGen) ?? throw new AssertionException("Missing call label.");
            var address = Descriptors(codeGen).Single();
            Assert.That(address.idIns(), Is.EqualTo(INS_lea));
            Assert.That(address.idIsDspReloc(), Is.True);
            Assert.That(address.idReg1(), Is.EqualTo(REG_R10));
            Assert.That(label.bbEmitCookie, Is.Null);
            var call = new GenTreeCall(TYP_VOID)
            {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x4000,
                _directCallAddress = (void*)0x1234,
                _returnType = TYP_VOID,
                RegNum = REG_NA,
            };
            codeGen.genCodeForTreeNode(call);

            Assert.That(PendingLabel(codeGen), Is.Null);
            Assert.That(label.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
        });
    }

    [Test]
    public static void HardwareNodesReachTheTableDrivenGenerator()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            CodeGenHardwareGenerationTests.Enable(compiler, codeGen);
            var first = new GenTreePhysReg(REG_XMM0, TYP_SIMD16) { RegNum = REG_XMM0 };
            var second = new GenTreePhysReg(REG_XMM1, TYP_SIMD16) { RegNum = REG_XMM1 };
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_X86Base_Add,
                TYP_FLOAT, 16, first, second) { RegNum = REG_XMM2 };
            codeGen.genCodeForTreeNode(tree);

            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_addps));
            Assert.That(Descriptors(codeGen).Single().idReg1(), Is.EqualTo(REG_XMM2));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FieldListsMustBeContained(bool contained)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeFieldList { IsContained = contained };
            if (contained)
            {
                codeGen.genCodeForTreeNode(tree);
            }
            else
            {
                _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
            }
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [Test]
    public static void UnsupportedTargetOperatorsFailExplicitly()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTree(GT_SWIFT_ERROR, TYP_I_IMPL);
            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

#if DEBUG
    [Test]
    public static void EachNodeStartsANewOperandConsumptionOrder()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            for (var i = 0; i < 2; i++)
            {
                var operand = Register(compiler, TYP_INT, REG_RAX);
                var useNum = 0;
                codeGen.genNumberOperandUse(operand, ref useNum);
                var tree = new GenTreeUnOp(GT_NEG, TYP_INT, operand) { RegNum = REG_RAX };
                codeGen.genCodeForTreeNode(tree);
            }
            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_neg, INS_neg]));
        });
    }

    [Test]
    public static void DisassemblyRejectsBeforeMarkerStateChanges()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RBX);
            var group = codeGen.Emitter.emitCurIG;
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() =>
                codeGen.genCodeForTreeNode(new GenTree(GT_START_PREEMPTGC, TYP_VOID)));

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
            Assert.That(codeGen.Emitter.emitCurIG, Is.SameAs(group));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genPendingCallLabel")]
    private static extern ref BasicBlock? PendingLabel(CodeGen codeGen);
}
