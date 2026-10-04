// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64CodeGenHelperCallTests
{
    [Test]
    public static void DirectHelperLookupReachesTheExplicitUnportedCallRecorder()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;

            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genEmitHelperCall(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN)) ??
                throw new AssertionException("The unported ARM64 call recorder did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Call instruction recording requires xarch."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }

    [Test]
    public static void ReturnTrapEmitsTheConditionalSkipBeforeTheUnportedCallRecorder()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var currentBlock = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = currentBlock;
            compiler.fgLastBB = currentBlock;
            compiler.compCurBB = currentBlock;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = false;
#endif

            var data = compiler.gtNewIconNode(TYP_INT, 1);
            data.RegNum = REG_R3;
            var tree = new GenTreeUnOp(GT_RETURNTRAP, TYP_VOID, data);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForReturnTrap(tree)) ??
                throw new AssertionException("The unported ARM64 call recorder did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Call instruction recording requires xarch."));

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_cmp, INS_beq]));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
        });
    }
}
#endif
