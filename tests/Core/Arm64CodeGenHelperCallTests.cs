// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64CodeGenHelperCallTests
{
    [Test]
    public static void DirectCallNodesRecordTheArchitectureCall()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;
            var call = new GenTreeCall(TYP_VOID)
            {
                _callType = CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x2000,
                _directCallAddress = (void*)0x1234,
            };

            codeGen.genCall(call);

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_bl));
            Assert.That((nint)descriptors[0].idAddr().iiaAddr, Is.EqualTo((nint)call._directCallAddress));
        });
    }

    [Test]
    public static void DirectHelperLookupRecordsTheCall()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;

            codeGen.genEmitHelperCall(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN);

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_bl));
        });
    }

    [Test]
    public static void ReturnTrapEmitsTheConditionalSkipBeforeTheHelperCall()
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

            codeGen.genCodeForReturnTrap(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()).ToArray(),
                Is.EqualTo((instruction[])[INS_cmp, INS_beq, INS_bl]));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
        });
    }
}
#endif
