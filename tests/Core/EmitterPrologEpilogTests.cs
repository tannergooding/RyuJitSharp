// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insGroupPlaceholderType;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class EmitterPrologEpilogTests
{
    [Test]
    public static void BackwardTraversalUsesSavedDescriptorsWhenCurrentGroupHasBeenSaved()
    {
        CodeGenPrologTests.WithRoot((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitBegProlog();
            emitter.emitIns(INS_nop);
            emitter.emitIns(INS_nop);
            emitter.emitEndProlog();

            var prolog = emitter.emitGetFirstPrologIG();
            var group = (insGroup?)prolog;
            var descriptors = prolog.igData ?? throw new AssertionException("Missing saved prolog.");
            var descriptor = (Emitter.instrDesc?)descriptors[1];
            Assert.That(emitter.emitCurIG, Is.SameAs(group));
            Assert.That(PreviousDescriptor(emitter, ref group, ref descriptor), Is.True);
            Assert.That(descriptor, Is.SameAs(descriptors[0]));
            Assert.That(group, Is.SameAs(emitter.emitCurIG));
        });
    }

    [TestCase(IGPT_EPILOG, 0)]
    [TestCase(IGPT_FUNCLET_PROLOG, 1)]
    [TestCase(IGPT_FUNCLET_EPILOG, 1)]
    public static void MaterializationRestoresSnapshotAndKeepsGroupIdentity(insGroupPlaceholderType kind, int funcIndex)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.compFuncInfos = [new() { funKind = FuncKind.FUNC_ROOT }, new() { funKind = FuncKind.FUNC_HANDLER }];
            compiler.compFuncInfoCount = 2;
            compiler.fgFuncletsCreated = true;
            compiler.compCurrFuncIdx = (ushort)funcIndex;
            var emitter = codeGen.Emitter;
            var vars = VarSetOps.MakeSingleton(compiler, 0);
            var placeholder = emitter.emitAddLabel(vars, RBM_RAX, RBM_RDX);
            var block = new BasicBlock(null, null);
            emitter.emitCreatePlaceholderIG(kind, block, vars, RBM_RCX, RBM_R8, last: true);
            var data = placeholder.igPhData ?? throw new AssertionException("Missing placeholder snapshot.");
            var expectedRefs = data.igPhInitGCrefRegs;
            var expectedByrefs = data.igPhInitByrefRegs;
            compiler.compCurrFuncIdx = 0;

            if (kind == IGPT_EPILOG)
            {
                emitter.emitBegFnEpilog(placeholder);
            }
            else if (kind == IGPT_FUNCLET_PROLOG)
            {
                emitter.emitBegFuncletProlog(placeholder);
            }
            else
            {
                emitter.emitBegFuncletEpilog(placeholder);
            }

            Assert.That(emitter.emitCurIG, Is.SameAs(placeholder));
            Assert.That(placeholder.igPhData, Is.Null);
            Assert.That(placeholder.igFlags & InsGroupFlags.Placeholder, Is.EqualTo(InsGroupFlags.None));
            Assert.That(compiler.compCurrFuncIdx, Is.EqualTo(funcIndex));
            Assert.That(compiler.compCurBB, Is.SameAs(block));
            Assert.That(emitter.InitGCrefRegs, Is.EqualTo(expectedRefs));
            Assert.That(emitter.InitByrefRegs, Is.EqualTo(expectedByrefs));
            Assert.That(VarSetOps.IsMember(compiler, emitter.InitGCrefVars, 0), Is.True);
            emitter.emitIns(INS_nop);
            if (kind == IGPT_EPILOG)
            {
                emitter.emitEndFnEpilog();
            }
            else if (kind == IGPT_FUNCLET_PROLOG)
            {
                emitter.emitEndFuncletProlog();
            }
            else
            {
                emitter.emitEndFuncletEpilog();
            }
            Assert.That(placeholder.igSize, Is.EqualTo(1));
            Assert.That(placeholder.igInsCnt, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RootPrologCanBeEmptyAndKeepsEarlierGcBoundary(bool extraInstruction)
    {
        CodeGenPrologTests.WithRoot((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitBegProlog();
            emitter.emitMarkPrologEnd();
            if (extraInstruction)
            {
                emitter.emitIns(INS_nop);
            }
            emitter.emitEndProlog();

            Assert.That(emitter.emitGetFirstPrologIG().igInsCnt, Is.EqualTo(extraInstruction ? 1 : 0));
            Assert.That(PrologEnd(emitter).GetInsNum(), Is.Zero);
            Assert.That(emitter.InitGCrefRegs, Is.EqualTo(RBM_NONE));
            Assert.That(emitter.InitByrefRegs, Is.EqualTo(RBM_NONE));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrologEndPos")]
    private static extern ref emitLocation PrologEnd(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitPrevID")]
    private static extern bool PreviousDescriptor(Emitter emitter, ref insGroup? group, ref Emitter.instrDesc? descriptor);
}
