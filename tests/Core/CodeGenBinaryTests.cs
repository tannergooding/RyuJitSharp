// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenBinaryTests
{
    [TestCase(GT_ADD, INS_add)]
    [TestCase(GT_SUB, INS_sub)]
    [TestCase(GT_AND, INS_and)]
    [TestCase(GT_OR, INS_or)]
    [TestCase(GT_XOR, INS_xor)]
    public static void IntegerOperationsPreserveAliasedDestinations(genTreeOps oper, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(oper, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RAX,
            };

            codeGen.genCodeForBinary(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_RCX));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void AdditionUsesLeaOnlyWhenNoFlagsAreRequired(bool immediate, bool setFlags)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var right = Register(compiler, TYP_INT, immediate ? REG_NA : REG_RCX);
            right.IsContained = immediate;
            var tree = new GenTreeOp(GT_ADD, TYP_INT, Register(compiler, TYP_INT, REG_RAX), right)
            {
                RegNum = REG_RDX,
            };
            if (setFlags)
            {
                tree.Flags |= GTF_SET_FLAGS;
            }

            codeGen.genCodeForBinary(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(setFlags ? 2 : 1));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(setFlags ? INS_add : INS_lea));
            if (!setFlags)
            {
                var address = descriptors[0].idAddr().iiaAddrMode;
                Assert.That(address.amBaseReg, Is.EqualTo(REG_RAX));
                Assert.That(address.amIndxReg, Is.EqualTo(immediate ? REG_NA : REG_RCX));
                Assert.That(address.amDisp, Is.EqualTo(immediate ? 7 : 0));
            }
        });
    }

    [TestCase(1, INS_inc)]
    [TestCase(-1, INS_dec)]
    public static void UncheckedUnitAdditionsUseUnaryForms(int value, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var constant = compiler.gtNewIconNode(TYP_INT, value);
            constant.IsContained = true;
            var tree = new GenTreeOp(GT_ADD, TYP_INT, Register(compiler, TYP_INT, REG_RAX), constant)
            {
                RegNum = REG_RAX,
            };
            codeGen.genCodeForBinary(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedFirstOperandsAreSwappedWithoutChangingEvaluation(bool memory)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            GenTree first = memory ? compiler.gtNewLclvNode(TYP_INT, 0) : compiler.gtNewIconNode(TYP_INT, 17);
            first.IsContained = true;
            var tree = new GenTreeOp(GT_XOR, TYP_INT, first, Register(compiler, TYP_INT, REG_RAX))
            {
                RegNum = REG_RAX,
            };
            codeGen.genCodeForBinary(tree);

            var descriptor = Descriptors(codeGen)[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_xor));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(memory
                ? Emitter.insFormat.IF_RRW_SRD : Emitter.insFormat.IF_RRW_CNS));
            Assert.That(tree.Op1, Is.SameAs(first));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public static void DistinctDestinationsUseNddOrAnExplicitCopy(bool ndd)
    {
        var saved = ApxNdd(ref JitConfig);
        ApxNdd(ref JitConfig) = ndd ? 1 : 0;
        try
        {
            WithCodeGen((compiler, codeGen) =>
            {
                codeGen.Emitter.UsePromotedEvexEncodings = ndd;
                var tree = new GenTreeOp(GT_SUB, TYP_LONG,
                    Register(compiler, TYP_LONG, REG_RAX), Register(compiler, TYP_LONG, REG_RCX))
                {
                    RegNum = REG_RDX,
                };
                codeGen.genCodeForBinary(tree);

                var descriptors = Descriptors(codeGen);
                Assert.That(descriptors, Has.Count.EqualTo(ndd ? 1 : 2));
                Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_sub));
                Assert.That(descriptors[^1].idIsEvexNdContextSet(), Is.EqualTo(ndd));
                Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_RDX));
            });
        }
        finally
        {
            ApxNdd(ref JitConfig) = saved;
        }
    }

    [TestCase(GT_ADD, false)]
    [TestCase(GT_ADD, true)]
    [TestCase(GT_SUB, false)]
    [TestCase(GT_SUB, true)]
    [TestCase(GT_MUL, false)]
    [TestCase(GT_MUL, true)]
    [TestCase(GT_DIV, false)]
    [TestCase(GT_DIV, true)]
    public static void FloatingOperationsRetainVexAndLegacyOperandOrder(genTreeOps oper, bool vex)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = vex;
            if (vex)
            {
                EnableAvx2(compiler);
            }
            var first = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            first.RegNum = REG_XMM0;
            var second = compiler.gtNewDconNode(TYP_DOUBLE, 2.0);
            second.RegNum = REG_XMM8;
            var tree = new GenTreeOp(oper, TYP_DOUBLE, first, second) { RegNum = REG_XMM1 };

            codeGen.genCodeForBinary(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(vex ? 1 : 2));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(CodeGen.genGetInsForOper(oper, TYP_DOUBLE)));
            if (vex)
            {
                var swap = oper is GT_ADD or GT_MUL;
                Assert.That(descriptors[^1].idReg2(), Is.EqualTo(swap ? REG_XMM8 : REG_XMM0));
                Assert.That(descriptors[^1].idReg3(), Is.EqualTo(swap ? REG_XMM0 : REG_XMM8));
            }
        });
    }

    [Test]
    public static void PointerArithmeticMovesTheLiveGcRootToItsResult()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            var tree = new GenTreeOp(GT_SUB, TYP_BYREF,
                Register(compiler, TYP_BYREF, REG_RAX), Register(compiler, TYP_I_IMPL, REG_RCX))
            {
                RegNum = REG_RDX,
            };

            codeGen.genCodeForBinary(tree);

            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(regMaskTP.CreateFromRegNum(REG_RDX, REG_RDX.SingleTypeMask)));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(2));
            Assert.That(Descriptors(codeGen)[0].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
        });
    }

    [TestCase(TYP_FLOAT, INS_addss)]
    [TestCase(TYP_DOUBLE, INS_addsd)]
    public static void FloatingContainedFirstOperandsRetainSignedZero(var_types type, instruction ins)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            var constant = compiler.gtNewDconNode(type, -0.0);
            constant.IsContained = true;
            var value = compiler.gtNewDconNode(type, 1.0);
            value.RegNum = REG_XMM0;
            var tree = new GenTreeOp(GT_ADD, type, constant, value) { RegNum = REG_XMM1 };

            codeGen.genCodeForBinary(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(ins));
            var section = codeGen.Emitter.emitConsDsc.dsdLast ??
                throw new AssertionException("Missing floating constant.");
            Assert.That(section.Data,
                Is.EqualTo(type == TYP_FLOAT ? BitConverter.GetBytes(-0.0f) : BitConverter.GetBytes(-0.0)));
        });
    }

    [TestCase(GT_ADD, TYP_INT, false)]
    [TestCase(GT_ADD, TYP_LONG, true)]
    [TestCase(GT_SUB, TYP_INT, true)]
    [TestCase(GT_SUB, TYP_LONG, false)]
    public static void OverflowBranchesPrecedeResultSpilling(genTreeOps oper, var_types type, bool unsigned)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var target = PrepareThrowTarget(compiler);
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(type, 1);
            var temp = codeGen.RegSet.tmpGetTemp(type);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            var constant = compiler.gtNewIconNode(TYP_INT, 1);
            constant.IsContained = true;
            var tree = new GenTreeOp(oper, type, Register(compiler, type, REG_RAX), constant)
            {
                RegNum = REG_RAX,
                Flags = GTF_OVERFLOW | GTF_SPILL | (unsigned ? GTF_UNSIGNED : GTF_EMPTY),
            };

            codeGen.genCodeForBinary(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(oper == GT_ADD ? INS_add : INS_sub));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(unsigned ? INS_jb : INS_jo));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[1]), Is.SameAs(target));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_mov));
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedThrowJumpsHonorExplicitAndLookedUpTargets(bool explicitTarget)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var target = PrepareThrowTarget(compiler);
            codeGen.genJumpToSharedThrowHlpBlk(EJ_jo, SCK_OVERFLOW, explicitTarget ? target : null);
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(Descriptors(codeGen)[0]), Is.SameAs(target));
        });
    }

    [Test]
    public static void InlineThrowModeRejectsBeforeConsumingOperands()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(GT_ADD, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RAX,
                Flags = GTF_OVERFLOW,
            };
            compiler.opts.compDbgCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForBinary(tree));
            Assert.That(Descriptors(codeGen), Is.Empty);
            compiler.opts.compDbgCode = false;
            _ = PrepareThrowTarget(compiler);

            codeGen.genCodeForBinary(tree);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public static void UncheckedArithmeticDoesNotRequireSharedThrowBlocks()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            var tree = new GenTreeOp(GT_ADD, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RAX,
            };

            codeGen.genCodeForBinary(tree);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(INS_add));
        });
    }

    [Test]
    public static void SameLocalRecognitionUnwrapsCopiesWithoutConflatingFields()
    {
        WithCodeGen((compiler, _) =>
        {
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var second = compiler.gtNewLclvNode(TYP_INT, 0);
            var copy = new GenTreeCopyOrReload(GT_COPY, TYP_INT, first);
            Assert.That(CodeGen.genIsSameLocalVar(copy, second), Is.True);
            Assert.That(CodeGen.genIsSameLocalVar(copy, new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 0)), Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectsBeforeArithmeticConsumption()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(GT_XOR, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RAX,
            };
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForBinary(tree));
            compiler.opts.dspCode = false;
            codeGen.genCodeForBinary(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
        });
    }
#endif

    internal static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
        CodeGenShiftTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
            codeGen.RegSet.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            action(compiler, codeGen);
        });
    }

    internal static BasicBlock PrepareThrowTarget(Compiler compiler)
    {
        var target = new BasicBlock(null, null);
        target.SetFlags(BBF_HAS_LABEL | BBF_THROW_HELPER);
        assert(compiler.compCurBB is not null);
        var descriptor = compiler.fgGetExcptnTarget(SCK_OVERFLOW, compiler.compCurBB);
        descriptor.acdUsed = true;
        descriptor.acdDstBlk = target;

        return target;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableApxNDD")]
    private static extern ref int ApxNdd(ref JitConfigValues config);
}
