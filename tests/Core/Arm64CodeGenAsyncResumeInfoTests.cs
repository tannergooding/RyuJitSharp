// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CodeGenAsyncResumeInfoTests
{
    [Test]
    public static void AsyncResumeDispatchUsesTheTargetWidthTableAndArm64AddressInstruction()
    {
        AsyncResumeCodeGenTestSupport.WithCodeGen((compiler, codeGen, context) =>
        {
            codeGen.genCodeForTreeNode(new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 1));
            var offset = codeGen.genEmitAsyncResumeInfoTable(out var table);

            Assert.That(offset, Is.EqualTo(8u));
            Assert.That(table.dsSize, Is.EqualTo(32u));
            Assert.That(table.Locations[0].Valid(), Is.False);
            Assert.That(table.Locations[1].Valid(), Is.True);
            Assert.That(Compiler.eeGetJitDataOffs(codeGen.genEmitAsyncResumeInfo(0)), Is.EqualTo(8));
            Assert.That(Compiler.eeGetJitDataOffs(codeGen.genEmitAsyncResumeInfo(1)), Is.EqualTo(24));

            codeGen.GCInfo.gcMarkRegPtrVal(REG_R3, TYP_REF);
            codeGen.genCodeForTreeNode(new GenTreeVal(GT_ASYNC_RESUME_INFO, TYP_I_IMPL, 0)
            {
                RegNum = REG_R3,
            });

            var descriptors = AsyncResumeCodeGenTestSupport.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_adr));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(Compiler.eeGetJitDataOffs(descriptors[0].idAddr().iiaFieldHnd), Is.EqualTo(8));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(context->Queries, Is.EqualTo(1));
        });
    }
}
#endif
