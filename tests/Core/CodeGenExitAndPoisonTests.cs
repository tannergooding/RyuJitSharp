// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenExitAndPoisonTests
{
    [TestCase(4, -16, 1, 1)]
    [TestCase(8, -16, 1, 1)]
    [TestCase(12, -16, 2, 2)]
    [TestCase(12, -20, 3, 1)]
    [TestCase(128, -128, 16, 16)]
    public static void SmallPoisoningPreservesNativePointerQuotientAndAlignedStoreWidths(
        int size, int offset, int stores, int wideStores)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            PreparePoison(compiler, size, offset);
            codeGen.genPoisonFrame(RBM_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(stores + 1));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo(unchecked((nint)0xCDCDCDCDCDCDCDCDUL)));
            Assert.That(ids.Skip(1).Count(id => id.idOpSize() == EA_8BYTE), Is.EqualTo(wideStores));
            Assert.That(ids.Skip(1).Sum(id => (int)EA_SIZE_IN_BYTES(id.idOpSize())),
                Is.EqualTo(compiler.lvaLclStackHomeSize(0)));
            Assert.That(ids.Skip(1).All(id => id.idReg1() == REG_SCRATCH), Is.True);
        });
    }

    [TestCase(132)]
    [TestCase(136)]
    [TestCase(256)]
    public static void LargePoisoningUsesRepStosdAndOnePoisonLoad(int size)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            PreparePoison(compiler, size, -size);
            codeGen.genPoisonFrame(RBM_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_lea, INS_mov, INS_mov, INS_r_stosd]));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RDI));
            Assert.That(ids[1].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[1]),
                Is.EqualTo((nint)(compiler.lvaLclStackHomeSize(0) / 4)));
            Assert.That(ids[2].idReg1(), Is.EqualTo(REG_RAX));
        });
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    public static void PoisoningSkipsParametersInitializedAndUnexposedLocals(bool parameter, bool initialized, bool exposed)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            PreparePoison(compiler, 8, -16);
            compiler.lvaTable[0].lvIsParam = parameter;
            compiler.lvaTable[0].lvMustInit = initialized;
            compiler.lvaTable[0].SetAddressExposed(exposed, AddressExposedReason.NONE);
            codeGen.genPoisonFrame(RBM_NONE);

            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [TestCase(1L, false)]
    [TestCase(-1L, false)]
    [TestCase(2147483647L, false)]
    [TestCase(-2147483648L, false)]
    [TestCase(2147483648L, true)]
    [TestCase(4294967295L, true)]
    [TestCase(4294967296L, true)]
    public static void CookieChecksUseSignedImmediateFit(long cookie, bool load)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.lvaGSSecurityCookie = 0;
            compiler.gsGlobalSecurityCookieVal = (nint)cookie;
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genEmitGSCookieCheck(false);
            var ids = CodeGenLocalHeapTests.AllDescriptors(first, codeGen);

            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(load
                ? (instruction[])[INS_mov, INS_cmp, INS_je, INS_call]
                : [INS_cmp, INS_je, INS_call]));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)cookie));
            if (load)
            {
                Assert.That(ids[0].idReg1(), Is.EqualTo(REG_R9));
            }
        });
    }

    [Test]
    public static void IndirectCookieChecksLoadTheRelocatableAddressThenTheCookie()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.lvaGSSecurityCookie = 0;
            compiler.gsGlobalSecurityCookieAddr = (nint*)0x12345678;
            compiler.opts.compReloc = true;
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genEmitGSCookieCheck(false);
            var ids = CodeGenLocalHeapTests.AllDescriptors(first, codeGen);

            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov, INS_mov, INS_cmp, INS_je, INS_call]));
            Assert.That(ids[0].idIsCnsReloc(), Is.True);
            Assert.That(ids[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R9));
        });
    }

    [TestCase(TYP_VOID)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_INT)]
    public static void ExitReservesAnEpilogWithTheReturnRootState(var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compRetTypeDesc.InitializeReturnType(compiler, type, null, compiler.info.compCallConv);
            var block = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            compiler.compCurBB = block;
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];
            if (type != TYP_VOID)
            {
                codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, type);
            }
            codeGen.genExitCode(block);

            Assert.That(compiler.genIPmappings.Single().ipmdKind, Is.EqualTo(IPmappingDscKind.Epilog));
            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
            Assert.That(CodeGenBlockDriverTests.LastPlaceholder(codeGen.Emitter)?.igFlags & InsGroupFlags.Epilog,
                Is.EqualTo(InsGroupFlags.Epilog));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_RAX : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? RBM_RAX : RBM_NONE));
        });
    }

    private static void PreparePoison(Compiler compiler, int size, int offset)
    {
        compiler.opts.compDbgCode = true;
        compiler.info.compInitMem = false;
        ref var local = ref compiler.lvaTable[0];
        local.Type = TYP_STRUCT;
        local.Layout = new ClassLayout((uint)size);
        local.StackOffset = offset;
        local.SetAddressExposed(true, AddressExposedReason.NONE);
    }
}
