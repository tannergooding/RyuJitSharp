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

internal static unsafe class CodeGenReturnTrapTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void TrapChecksBranchAroundTheGcHelperAndUseTheAssignedTemporary(bool memory, bool indirect)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            GenTree data = memory ? compiler.gtNewLclvNode(TYP_INT, 0) : Register(compiler, TYP_INT, REG_RAX);
            data.IsContained = memory;
            var tree = new GenTreeUnOp(GT_RETURNTRAP, TYP_VOID, data);
            codeGen.InternalRegisters.Add(tree, RBM_R11 | RBM_XMM5);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            var firstGroup = codeGen.Emitter.emitCurIG;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var context = new HelperContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Indirect = indirect,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;

            codeGen.genCodeForReturnTrap(tree);

            var saved = firstGroup?.igData ?? throw new AssertionException("Missing trap group.");
            Assert.That(saved.Select(id => id.idIns()), Is.EqualTo(indirect
                ? (instruction[])[INS_cmp, INS_je, INS_mov, INS_call]
                : [INS_cmp, INS_je, INS_call]));
            Assert.That(saved[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(InstructionConstant(codeGen.Emitter, saved[0]), Is.EqualTo((nint)0));
            Assert.That(context.Helper, Is.EqualTo(CORINFO_HELP_STOP_FOR_GC));
            if (indirect)
            {
                Assert.That(saved[2].idReg1(), Is.EqualTo(REG_R11));
                Assert.That(saved[^1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R11));
            }
            var label = EmitterJumpInstructionTests.JumpView.Target(saved[1]);
            Assert.That(label?.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
#if DEBUG
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(RBM_XMM5));
#else
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(RBM_R11 | RBM_XMM5));
#endif
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectsBeforeConsumingTrapOperandsOrTemporaries()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var tree = new GenTreeUnOp(GT_RETURNTRAP, TYP_VOID, Register(compiler, TYP_INT, REG_RAX));
            codeGen.InternalRegisters.Add(tree, RBM_R11);
            compiler.opts.dspCode = true;

            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForReturnTrap(tree));

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(RBM_R11));
            compiler.opts.dspCode = false;
            codeGen.genCodeForReturnTrap(tree);
        });
    }
#endif

    private struct HelperContext
    {
        public ICorJitInfo JitInfo;
        public bool Indirect;
        public CorInfoHelpFunc Helper;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        var context = (HelperContext*)self;
        context->Helper = helper;
        lookup->accessType = context->Indirect ? InfoAccessType.IAT_PVALUE : InfoAccessType.IAT_VALUE;
        lookup->addr = context->Indirect ? (void*)0x100000000 : (void*)0x1234;

        return lookup->addr;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* self, void* address) => CorInfoReloc.NONE;
}
