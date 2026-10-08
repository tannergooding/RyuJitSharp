// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32CodeGenAsyncResumeInfoTests
{
    [Test]
    public static void AsyncResumeDispatchUsesTheTargetWidthTableAndArm32AddressInstructions()
    {
        AsyncResumeCodeGenTestSupport.WithCodeGen((compiler, codeGen, context) =>
        {
            codeGen.genCodeForTreeNode(new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 1));
            var offset = codeGen.genEmitAsyncResumeInfoTable(out var table);

            Assert.That(offset, Is.EqualTo(4u));
            Assert.That(table.dsSize, Is.EqualTo(16u));
            Assert.That(table.Locations[0].Valid(), Is.False);
            Assert.That(table.Locations[1].Valid(), Is.True);
            Assert.That(Compiler.eeGetJitDataOffs(codeGen.genEmitAsyncResumeInfo(0)), Is.EqualTo(4));
            Assert.That(Compiler.eeGetJitDataOffs(codeGen.genEmitAsyncResumeInfo(1)), Is.EqualTo(12));

            codeGen.GCInfo.gcMarkRegPtrVal(REG_R4, TYP_REF);
            var tree = new GenTreeVal(GT_ASYNC_RESUME_INFO, TYP_I_IMPL, 0)
            {
                RegNum = REG_R4,
            };

            FatalJitException? failure = null;
            try
            {
                codeGen.genCodeForTreeNode(tree);
            }
            catch (FatalJitException exception) when (exception.Result == CORJIT_SKIPPED)
            {
                failure = exception;
            }

            var descriptors = AsyncResumeCodeGenTestSupport.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Is.Not.Empty);
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movw));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)4));
            Assert.That(failure, Is.Null);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_movt));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptors[1]), Is.EqualTo((nint)4));
            Assert.That(Compiler.eeGetJitDataOffs(
                codeGen.genEmitAsyncResumeInfo(1)), Is.EqualTo(12));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(context->Queries, Is.EqualTo(1));
        });
    }
}
#endif
