// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
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
        Assume.That(Configuration.TARGET_ARM64);

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
        Assume.That(Configuration.TARGET_ARM64);

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
    public static void RelocatableHelperLookupForwardsDebugMetadata()
    {
        Assume.That(Configuration.TARGET_ARM64);

        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            vtable.Base.Base.getAddressAlignment = &GetAddressAlignment;
            var context = new HelperContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Address = (void*)0x1234,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;
            compiler.opts.compReloc = true;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;

            codeGen.genEmitHelperCallArm64(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN);

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            var relocation = descriptors.First(descriptor => descriptor.idIns() == INS_adrp);
            var debugInfo = relocation.idDebugOnlyInfo();
            if (TestConfiguration.IsDebug)
            {
                var requiredDebugInfo = debugInfo ?? throw new AssertionException("Missing helper metadata.");
                Assert.That(requiredDebugInfo.idMemCookie,
                    Is.EqualTo((nint)Compiler.eeFindHelper(CORINFO_HELP_STOP_FOR_GC)));
                Assert.That(requiredDebugInfo.idFlags, Is.EqualTo(GTF_ICON_METHOD_HDL));
            }
            else if (debugInfo is not null)
            {
                Assert.That(debugInfo.idMemCookie, Is.Zero);
                Assert.That(debugInfo.idFlags, Is.EqualTo(GTF_EMPTY));
            }
        });
    }

    [Test]
    public static void ReturnTrapEmitsTheConditionalSkipBeforeTheHelperCall()
    {
        Assume.That(Configuration.TARGET_ARM64);

        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;
            TestConfiguration.EnableSafeBasicBlockCreation(compiler);
            var currentBlock = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = currentBlock;
            compiler.fgLastBB = currentBlock;
            compiler.compCurBB = currentBlock;
            TestConfiguration.DisableSafeBasicBlockCreation(compiler);

            var data = compiler.gtNewIconNode(TYP_INT, 1);
            data.RegNum = REG_ARM64_R3;
            var tree = new GenTreeUnOp(GT_RETURNTRAP, TYP_VOID, data);

            codeGen.genCodeForReturnTrap(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()).ToArray(),
                Is.EqualTo((instruction[])[INS_cmp, INS_beq, INS_bl]));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ARM64_R3));
        });
    }

    private struct HelperContext
    {
        public ICorJitInfo JitInfo;
        public void* Address;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        var context = (HelperContext*)self;
        lookup->accessType = IAT_PVALUE;
        lookup->addr = context->Address;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static uint GetAddressAlignment(ICorJitInfo* self, void* address) => 1;
}
