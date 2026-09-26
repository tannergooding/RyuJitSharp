// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenThrowHelperTests
{
    [TestCase(SCK_RNGCHK_FAIL, CORINFO_HELP_RNGCHKFAIL)]
    [TestCase(SCK_DIV_BY_ZERO, CORINFO_HELP_THROWDIVZERO)]
    [TestCase(SCK_OVERFLOW, CORINFO_HELP_OVERFLOW)]
    [TestCase(SCK_ARG_EXCPN, CORINFO_HELP_THROW_ARGUMENTEXCEPTION)]
    [TestCase(SCK_ARG_RNG_EXCPN, CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION)]
    [TestCase(SCK_FAIL_FAST, CORINFO_HELP_FAIL_FAST)]
    [TestCase(SCK_NULL_CHECK, CORINFO_HELP_THROWNULLREF)]
    public static void InlineThrowsUseTheNativeHelperAndIgnoreAnExplicitFailBlock(
        SpecialCodeKind kind, CorInfoHelpFunc helper)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            var context = new HelperContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;
            compiler.opts.compDbgCode = true;
            var firstGroup = codeGen.Emitter.emitCurIG;
            var failBlock = new BasicBlock(null, null);

            codeGen.genJumpToThrowHlpBlk(EJ_jo, kind, failBlock);

            _ = AssertInlineThrow(firstGroup, INS_jno, 2);
            Assert.That(context.Helper, Is.EqualTo(helper));
            Assert.That(failBlock.bbEmitCookie, Is.Null);
        });
    }

    [TestCase(EJ_jo, INS_jno)]
    [TestCase(EJ_jb, INS_jae)]
    [TestCase(EJ_jae, INS_jb)]
    [TestCase(EJ_je, INS_jne)]
    public static void ConditionalThrowsRestoreNormalPathGcStateAtTheColdContinuation(
        emitJumpKind jump, instruction reverse)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            var block = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
            block.SetFlags(BBF_COLD);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_BYREF);
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genJumpToThrowHlpBlk(jump, SCK_OVERFLOW);

            var target = AssertInlineThrow(firstGroup, reverse, 2);
            Assert.That(target.HasFlag(BBF_COLD | BBF_HAS_LABEL), Is.True);
            Assert.That(target.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
            Assert.That(ThisRefs(codeGen.Emitter), Is.EqualTo(REG_RAX.SingleTypeMask));
            Assert.That(ThisByrefs(codeGen.Emitter), Is.EqualTo(REG_RBX.SingleTypeMask));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RAX));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_RBX));
        });
    }

    [Test]
    public static void UnconditionalThrowsDoNotCreateAContinuationLabel()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            var firstGroup = codeGen.Emitter.emitCurIG;
            var blockCount = compiler.fgBBcount;

            codeGen.genJumpToThrowHlpBlk(EJ_jmp, SCK_FAIL_FAST);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(INS_call));
            Assert.That(codeGen.Emitter.emitCurIG, Is.SameAs(firstGroup));
            Assert.That(compiler.fgBBcount, Is.EqualTo(blockCount));
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsInlineThrowAndContinuation()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            compiler.opts.dspCode = true;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            var context = new HelperContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;
            var first = codeGen.Emitter.emitCurIG;

            var diagnostic = InstructionRecordingTestSupport.Capture(
                () => codeGen.genJumpToThrowHlpBlk(EJ_jo, SCK_OVERFLOW));
            Assert.That(diagnostic, Does.Contain("call"));
            Assert.That(CodeGenLocalHeapTests.AllDescriptors(first, codeGen).Exists(id => id.idIns() == INS_call), Is.True);
        });
    }
#endif

    internal static BasicBlock AssertInlineThrow(insGroup? group, instruction reverseJump, int count)
    {
        var saved = group?.igData ?? throw new AssertionException("Missing inline throw group.");
        Assert.That(saved, Has.Length.EqualTo(count));
        Assert.That(saved[^1].idIns(), Is.EqualTo(INS_call));
        Assert.That(saved[^2].idIns(), Is.EqualTo(reverseJump));
        var target = EmitterJumpInstructionTests.JumpView.Target(saved[^2])
            ?? throw new AssertionException("Missing inline throw continuation.");
        Assert.That(target.bbEmitCookie, Is.SameAs(group.igNext));

        return target;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);

    private struct HelperContext
    {
        public ICorJitInfo JitInfo;
        public CorInfoHelpFunc Helper;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        ((HelperContext*)self)->Helper = helper;
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x1234;

        return lookup->addr;
    }
}
