// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.FuncKind;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class FuncletCreationTests
{
    [Test]
    public static void RootOnlyMethodsPublishARealDescriptorWithoutClauseMaps()
    {
        WithCompiler((compiler, _) =>
        {
            var root = NewBlock(compiler);
            Link(compiler, root);
            Assert.That(compiler.fgCreateFunclets(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgFuncletsCreated, Is.True);
            Assert.That(compiler.compFuncInfoCount, Is.EqualTo(1));
            Assert.That(compiler.funCurrentFunc().funKind, Is.EqualTo(FUNC_ROOT));
            Assert.That(compiler.funCurrentFunc().GetStartBlock(compiler), Is.SameAs(root));
            Assert.That(compiler.compVMClauseOrderToEHTabOrder, Is.Null);
            Assert.That(compiler.compEHTabOrderToVMClauseOrder, Is.Null);
        });
    }

    [Test]
    public static void SharedTryClausesAndFiltersProduceNativeTableAndBlockOrder()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var root = NewBlock(compiler);
            var tryA = NewBlock(compiler);
            var handlerA = NewBlock(compiler);
            var tryB = NewBlock(compiler);
            var filterB = NewBlock(compiler);
            var handlerB = NewBlock(compiler);
            var handlerC = NewBlock(compiler);
            var exit = NewBlock(compiler);
            Link(compiler, root, tryA, handlerA, tryB, filterB, handlerB, handlerC, exit);
            tryA.TryIndex = 2;
            tryB.TryIndex = 1;
            handlerA.HndIndex = 0;
            filterB.HndIndex = 1;
            handlerB.HndIndex = 1;
            handlerC.HndIndex = 2;
            compiler.compHndBBtab = [
                Clause(tryA, handlerA),
                Clause(tryB, handlerB, filterB),
                Clause(tryA, handlerC),
            ];
            compiler.compHndBBtabCount = 3;

            Assert.That(compiler.ehFuncletCount(), Is.EqualTo(4));
            Assert.That(compiler.fgCreateFunclets(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            ushort[] order = [1, 0, 2];
            BasicBlock[] blocks = [root, tryA, tryB, exit, filterB, handlerB, handlerA, handlerC];
            FuncKind[] kinds = [FUNC_ROOT, FUNC_FILTER, FUNC_HANDLER, FUNC_HANDLER, FUNC_HANDLER];
            Assert.That(compiler.compVMClauseOrderToEHTabOrder, Is.EqualTo(order));
            Assert.That(compiler.compEHTabOrderToVMClauseOrder, Is.EqualTo(order));
            Assert.That(compiler.Blocks.ToArray(), Is.EqualTo(blocks));
            Assert.That(compiler.compFuncInfos.Select(info => info.funKind), Is.EqualTo(kinds));
            Assert.That(compiler.fgFirstFuncletBB, Is.SameAs(filterB));
            Assert.That(compiler.compHndBBtab[0].ebdFuncIndex, Is.EqualTo(3));
            Assert.That(compiler.compHndBBtab[1].ebdFuncIndex, Is.EqualTo(2));
            Assert.That(compiler.compHndBBtab[2].ebdFuncIndex, Is.EqualTo(4));

            foreach (var block in blocks.Skip(4))
            {
                codeGen.genUpdateCurrentFunclet(block);
                Assert.That(compiler.funCurrentFunc().GetStartBlock(compiler), Is.SameAs(block));
            }
            ref var descriptor = ref compiler.funGetFunc(3);
            descriptor.funFlags = 7;
            Assert.That(compiler.compFuncInfos[3].funFlags, Is.EqualTo(7));
        });
    }

    [Test]
    public static void FinallyBackedgesBypassTheNewPrologAndRetainProfileWeight()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var entry = NewBlock(compiler);
            var call = NewBlock(compiler);
            var exit = NewBlock(compiler);
            var handler = NewBlock(compiler);
            Link(compiler, entry, call, exit, handler);
            entry.TryIndex = 0;
            entry.bbRefs = 1;
            handler.HndIndex = 0;
            handler.CatchType = BBCT_FINALLY;
            handler.bbRefs = 1;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(call, entry));
            call.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(handler, call));
            call.SetFlags(BBF_RETLESS_CALL);
            handler.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(handler, handler));
            call.setBBProfileWeight(7);
            handler.setBBProfileWeight(100);
            var clause = Clause(entry, handler);
            clause.ebdHandlerType = EH_HANDLER_FINALLY;
            compiler.compHndBBtab = [clause];
            compiler.compHndBBtabCount = 1;

            _ = compiler.fgCreateFunclets();

            var prolog = compiler.compHndBBtab[0].ebdHndBeg;
            Assert.That(prolog, Is.Not.SameAs(handler));
            Assert.That(call.Target, Is.SameAs(prolog));
            Assert.That(prolog.Target, Is.SameAs(handler));
            Assert.That(handler.Target, Is.SameAs(handler));
            Assert.That(prolog.CatchType, Is.EqualTo(BBCT_FINALLY));
            Assert.That(handler.CatchType, Is.EqualTo(BBCT_NONE));
            Assert.That(prolog.bbRefs, Is.EqualTo(2));
            Assert.That(handler.bbRefs, Is.EqualTo(2));
            Assert.That(prolog.bbWeight, Is.EqualTo(7));
            Assert.That(handler.bbWeight, Is.EqualTo(100));
            Assert.That(compiler.fgFirstFuncletBB, Is.SameAs(prolog));
            Assert.That(compiler.compFuncInfoCount, Is.EqualTo(2));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RelocatingNestedHandlersShrinksAnEnclosingSharedEnd(bool enclosingHandler)
    {
        WithCompiler((compiler, _) =>
        {
            var entry = NewBlock(compiler);
            var outerBegin = NewBlock(compiler);
            var innerTry = NewBlock(compiler);
            var innerHandler = NewBlock(compiler);
            var sharedEnd = NewBlock(compiler);
            var exit = NewBlock(compiler);
            var outerHandler = NewBlock(compiler);
            Link(compiler, entry, outerBegin, innerTry, innerHandler, sharedEnd, exit, outerHandler);
            innerTry.TryIndex = 0;
            innerHandler.HndIndex = 0;
            sharedEnd.HndIndex = 0;
            outerHandler.HndIndex = 1;
            var inner = Clause(innerTry, innerHandler);
            inner.ebdHndLast = sharedEnd;
            var outer = Clause(enclosingHandler ? entry : outerBegin, enclosingHandler ? outerBegin : outerHandler);
            if (enclosingHandler)
            {
                outerBegin.HndIndex = 1;
                innerTry.HndIndex = 1;
                outer.ebdHndLast = sharedEnd;
                inner.ebdEnclosingHndIndex = 1;
            }
            else
            {
                outerBegin.TryIndex = 1;
                innerHandler.TryIndex = 1;
                sharedEnd.TryIndex = 1;
                outer.ebdTryLast = sharedEnd;
                inner.ebdEnclosingTryIndex = 1;
            }
            compiler.compHndBBtab = [inner, outer];
            compiler.compHndBBtabCount = 2;

            var moved = compiler.fgRelocateEHRange(0, Compiler.FG_RELOCATE_TYPE.FG_RELOCATE_HANDLER);

            Assert.That(moved, Is.SameAs(sharedEnd));
            ref var adjusted = ref compiler.compHndBBtab[1];
            Assert.That(enclosingHandler ? adjusted.ebdHndLast : adjusted.ebdTryLast, Is.SameAs(innerTry));
            Assert.That(innerTry.Next, Is.SameAs(exit));
            Assert.That(outerHandler.Next, Is.SameAs(innerHandler));
            Assert.That(compiler.fgLastBB, Is.SameAs(sharedEnd));
        });
    }

    private static EHblkDsc Clause(BasicBlock tryBegin, BasicBlock handler, BasicBlock? filter = null)
    {
        return new EHblkDsc
        {
            ebdTryBeg = tryBegin,
            ebdTryLast = tryBegin,
            ebdHndBeg = handler,
            ebdHndLast = handler,
            ebdFilter = filter,
            ebdHandlerType = filter is null ? EH_HANDLER_CATCH : EH_HANDLER_FILTER,
            ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
        };
    }

    private static BasicBlock NewBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_THROW);
        block.bbRefs = 0;
        return block;
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        for (var index = 1; index < blocks.Length; index++)
        {
            blocks[index - 1].Next = blocks[index];
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
    }

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.compHndBBtab = [];
            compiler.fgPredsComputed = true;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            action(compiler, codeGen);
        });
    }
}
