// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenAsyncTransferTests
{
    [TestCase(TYP_VOID)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void SuspensionReturnsClearOnlyGcReturnValues(var_types type)
    {
        CodeGenReturnTests.WithReturn(type, (compiler, codeGen) =>
        {
            var source = Register(compiler, TYP_REF, REG_R8);
            var tree = new GenTreeUnOp(GT_RETURN_SUSPEND, TYP_VOID, source);
            codeGen.genReturnSuspend(tree);
            var ids = Descriptors(codeGen);
            var continuationMask = regMaskTP.CreateFromRegNum(REG_ASYNC_CONTINUATION_RET,
                REG_ASYNC_CONTINUATION_RET.SingleTypeMask);

            Assert.That(ids, Has.Count.EqualTo(varTypeIsGC(type) ? 2 : 1));
            Assert.That(ids[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_ASYNC_CONTINUATION_RET));
            if (varTypeIsGC(type))
            {
                Assert.That(ids[1].idIns(), Is.EqualTo(INS_xor));
                Assert.That(ids[1].idReg1(), Is.EqualTo(REG_INTRET));
                Assert.That(ids[1].idReg2(), Is.EqualTo(REG_INTRET));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(continuationMask | (type == TYP_REF ? RBM_RAX : RBM_NONE)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? RBM_RAX : RBM_NONE));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContinuationValuesRetainTheSourceGcRoot(bool same)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF)
            {
                RegNum = same ? REG_ASYNC_CONTINUATION_RET : REG_R8,
            };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_ASYNC_CONTINUATION_RET, TYP_REF);
            codeGen.genCodeForAsyncContinuation(node);
            var sourceMask = regMaskTP.CreateFromRegNum(REG_ASYNC_CONTINUATION_RET,
                REG_ASYNC_CONTINUATION_RET.SingleTypeMask);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(same ? 0 : 1));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(sourceMask | (same ? RBM_NONE : RBM_R8)));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void NonlocalTransfersRetainRegisterLocalAndIndirectOperands(int kind)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            GenTree operand = kind switch
            {
                0 => Register(compiler, TYP_I_IMPL, REG_R8),
                1 => new GenTreeLclFld(GT_LCL_FLD, TYP_I_IMPL, 0, 0) { IsContained = true },
                _ => new GenTreeIndir(GT_IND, TYP_I_IMPL, Register(compiler, TYP_I_IMPL, REG_R8))
                    { IsContained = true },
            };
            var tree = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, operand);
            codeGen.genNonLocalJmp(tree);
            var id = Descriptors(codeGen).Single();

            Assert.That(id.idIns(), Is.EqualTo(INS_i_jmp));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            if (kind == 0)
            {
                Assert.That(id.idReg1(), Is.EqualTo(REG_R8));
            }
            else if (kind == 1)
            {
                Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            }
            else
            {
                Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R8));
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void UnaryOperandDispatchRetainsAllNativeOperandKinds(int kind)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            GenTree operand = kind switch
            {
                0 => Register(compiler, TYP_LONG, REG_R8),
                1 => new GenTreeLclFld(GT_LCL_FLD, TYP_LONG, 0, 0) { IsContained = true },
                2 => new GenTreeIndir(GT_IND, TYP_LONG, Register(compiler, TYP_I_IMPL, REG_R8))
                    { IsContained = true },
                3 => new GenTreeIntCon(TYP_LONG, 42) { IsContained = true },
                _ => new GenTreeDblCon(TYP_DOUBLE, 1.5) { IsContained = true },
            };
            codeGen.inst_TT(INS_push_hide, EA_8BYTE, operand);
            var id = Descriptors(codeGen).Single();

            Assert.That(id.idIns(), Is.EqualTo(INS_push_hide));
            if (kind == 3)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)42));
            }
            if (kind == 4)
            {
                Assert.That((nuint)id.idAddr().iiaFieldHnd, Is.Not.EqualTo((nuint)0));
            }
        });
    }

    [Test]
    public static void FunctionEntryBindsToThePrologRatherThanTheCurrentBodyGroup()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var prolog = codeGen.Emitter.emitGetFirstPrologIG();
            Assert.That(prolog, Is.Not.SameAs(codeGen.Emitter.emitCurIG));
            var node = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL) { RegNum = REG_R8 };
            codeGen.genFtnEntry(node);
            var id = Descriptors(codeGen).Single();

            Assert.That(id.idIns(), Is.EqualTo(INS_lea));
            Assert.That(id.idReg1(), Is.EqualTo(REG_R8));
            Assert.That(id.idIsBound(), Is.True);
            Assert.That(EmitterJumpInstructionTests.JumpView.TargetGroup(id), Is.SameAs(prolog));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(id), Is.Null);
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(7u));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PatchpointsCallTheMatchingHelperThenJumpWithoutAnEpilogPrefix(bool forced)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelper;
            var context = new HelperContext { Info = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compCompHnd = &context.Info;
            compiler.info.compMatchedVM = true;
            var node = forced
                ? new GenTreeUnOp(GT_PATCHPOINT_FORCED, TYP_VOID, Register(compiler, TYP_I_IMPL, REG_R8))
                : new GenTreeOp(GT_PATCHPOINT, TYP_VOID, Register(compiler, TYP_I_IMPL, REG_R8),
                    Register(compiler, TYP_INT, REG_R9));
            codeGen.genPatchpoint(node);
            var ids = Descriptors(codeGen);

            Assert.That(context.Helper, Is.EqualTo(forced ? CORINFO_HELP_PATCHPOINT_FORCED : CORINFO_HELP_PATCHPOINT));
            Assert.That(ids.Count(id => id.idIns() == INS_mov), Is.EqualTo(forced ? 1 : 2));
            Assert.That(ids[^2].idIns(), Is.EqualTo(INS_call));
            Assert.That(ids[^1].idIns(), Is.EqualTo(INS_i_jmp));
            Assert.That(ids[^1].idReg1(), Is.EqualTo(REG_INTRET));
            Assert.That(ids[^1].idCodeSize(), Is.EqualTo(2u));
        });
    }

    private struct HelperContext
    {
        public ICorJitInfo Info;
        public CorInfoHelpFunc Helper;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelper(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        var context = (HelperContext*)self;
        context->Helper = helper;
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x1234;

        return (void*)0x1234;
    }
}
