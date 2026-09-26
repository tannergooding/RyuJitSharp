// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumTreeTests
{
    [TestCase(GT_LT, false, false, VNF_LT)]
    [TestCase(GT_LT, true, false, VNF_LT_UN)]
    [TestCase(GT_GE, true, false, VNF_GE_UN)]
    [TestCase(GT_GT, true, true, VNF_GT_UN)]
    [TestCase(GT_ADD, false, false, VNF_ADD)]
    [TestCase(GT_ADD, true, false, VNF_ADD_UN_OVF)]
    public static void BinaryDispatcherPreservesSignedUnorderedAndOverflowMapping(
        genTreeOps oper, bool flag, bool floating, VNFunc expected)
    {
        WithStore((compiler, store) =>
        {
            var operandType = floating ? TYP_DOUBLE : TYP_INT;
            var left = compiler.gtNewZeroConNode(operandType);
            var right = compiler.gtNewZeroConNode(operandType);
            left._vnPair.SetBoth(store.VNForExpr(null, operandType));
            right._vnPair.SetBoth(store.VNForExpr(null, operandType));
            var tree = new GenTreeOp(oper, TYP_INT, left, right)
            {
                Flags = (oper is GT_ADD && flag) ? GTF_OVERFLOW | GTF_EXCEPT
                    : (floating && flag) ? GTF_RELOP_NAN_UN : GenTreeFlags.GTF_EMPTY,
                IsUnsigned = !floating && flag,
            };

            compiler.fgValueNumberTree(tree);

            var normal = store.VNNormalValue(tree._vnPair.Liberal);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(normal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(expected));
            if (oper is GT_ADD && flag)
            {
                Assert.That(store.VNExceptionSet(tree._vnPair.Liberal),
                    Is.Not.EqualTo(ValueNumStore.VNForEmptyExcSet()));
            }
        });
    }

    [TestCase(false, VNF_PtrToStatic)]
    [TestCase(true, VNF_PtrToArrElem)]
    public static void UncheckedAddExtendsPointerAndPreservesBothOperandExceptions(bool array, VNFunc expected)
    {
        WithStore((compiler, store) =>
        {
            var address = store.VNForIntPtrCon(0x1000);
            var offset = store.VNForIntPtrCon(4);
            var pointer = array
                ? store.VNForFunc(TYP_BYREF, VNF_PtrToArrElem, address,
                    store.VNForExpr(null, TYP_REF), store.VNForIntCon(2), offset)
                : store.VNForFunc(TYP_BYREF, VNF_PtrToStatic, address,
                    store.VNForFieldSeq(null), offset);
            var leftExc = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc, address));
            var rightExc = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_HelperOpaqueExc, address));
            var left = compiler.gtNewIconNode(TYP_BYREF, 0);
            left._vnPair.SetBoth(store.VNWithExc(pointer, leftExc));
            var right = compiler.gtNewIconNode(TYP_I_IMPL, 8);
            right._vnPair.SetBoth(store.VNWithExc(store.VNForIntPtrCon(8), rightExc));
            var add = new GenTreeOp(GT_ADD, TYP_BYREF, left, right);

            compiler.fgValueNumberTree(add);

            var normal = store.VNNormalValue(add._vnPair.Liberal);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(normal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(expected));
            Assert.That(app.GetArg(array ? 3 : 2), Is.EqualTo(store.VNForIntPtrCon(12)));
            Assert.That(store.VNExceptionSet(add._vnPair.Liberal),
                Is.EqualTo(store.VNExcSetUnion(leftExc, rightExc)));
        });
    }

    [Test]
    public static void CommaRetainsSecondValueAndUnionsFirstExceptions()
    {
        WithStore((compiler, store) =>
        {
            var firstVN = store.VNForExpr(null, TYP_INT);
            var secondVN = store.VNForExpr(null, TYP_INT);
            var exception = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNF_OverflowExc, firstVN));
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            first._vnPair.SetBoth(store.VNWithExc(firstVN, exception));
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            second._vnPair.SetBoth(secondVN);
            var comma = new GenTreeOp(GT_COMMA, TYP_INT, first, second);

            compiler.fgValueNumberTree(comma);

            Assert.That(store.VNNormalValue(comma._vnPair.Liberal), Is.EqualTo(secondVN));
            Assert.That(store.VNExceptionSet(comma._vnPair.Liberal), Is.EqualTo(exception));
        });
    }

    [Test]
    public static void BoundsCheckUnionsIndexBeforeLengthAndMarksConservativeArguments()
    {
        WithStore((compiler, store) =>
        {
            var indexVN = store.VNForExpr(null, TYP_INT);
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var indexExc = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc, indexVN));
            var lengthExc = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_HelperOpaqueExc, lengthVN));
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index._vnPair.SetBoth(store.VNWithExc(indexVN, indexExc));
            var length = compiler.gtNewIconNode(TYP_INT, 0);
            length._vnPair.SetBoth(store.VNWithExc(lengthVN, lengthExc));
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);

            compiler.fgValueNumberTree(check);

            Assert.That(store.VNNormalValue(check._vnPair.Liberal), Is.EqualTo(ValueNumStore.VNForVoid()));
            Assert.That(store.IsVNCheckedBoundIndex(indexVN), Is.True);
            Assert.That(store.IsVNCheckedBound(lengthVN), Is.True);
            var combined = store.VNExcSetUnion(indexExc, lengthExc);
            var expected = store.VNExcSetUnion(combined,
                store.VNExcSetSingleton(store.VNForFuncNoFolding(
                    TYP_REF, VNF_IndexOutOfRangeExc, indexVN, lengthVN)));
            Assert.That(store.VNExceptionSet(check._vnPair.Liberal), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void SsaLocalLoadAndStoreUseTheirEstablishedSsaDefinition()
    {
        WithStore((compiler, store) =>
        {
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_INT;
            var ssa = local.lvPerSsaData.AllocSsaNum();
            var first = store.VNForIntCon(17);
            local.GetPerSsaData(ssa)._vnPair.SetBoth(first);
            var read = compiler.gtNewLclvNode(TYP_INT, 0);
            read.SsaNum = ssa;
            compiler.fgValueNumberTree(read);
            Assert.That(read._vnPair.Liberal, Is.EqualTo(first));

            var input = compiler.gtNewIconNode(TYP_INT, 18);
            var updated = store.VNForIntCon(18);
            input._vnPair.SetBoth(updated);
            var assignment = compiler.gtNewStoreLclVarNode(0, input);
            assignment.SsaNum = ssa;
            compiler.fgValueNumberTree(assignment);
            Assert.That(local.GetPerSsaData(ssa)._vnPair.Liberal, Is.EqualTo(updated));
            Assert.That(assignment._vnPair.Liberal, Is.EqualTo(ValueNumStore.VNForVoid()));
        });
    }

    [Test]
    public static void HelperCallUsesNativeCallDispatcherWithoutGenericOperandFolding()
    {
        WithStore((compiler, store) =>
        {
            var call = new GenTreeCall(TYP_LONG)
            {
                _callType = CT_HELPER,
                _callMethHnd = Compiler.eeFindHelper(CORINFO_HELP_LLSH),
            };
            var value = compiler.gtNewIconNode(TYP_LONG, 0);
            var shift = compiler.gtNewIconNode(TYP_INT, 0);
            var valueVN = store.VNForExpr(null, TYP_LONG);
            var shiftVN = store.VNForIntCon(2);
            value._vnPair.SetBoth(valueVN);
            shift._vnPair.SetBoth(shiftVN);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(shift));

            compiler.fgValueNumberTree(call);

            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(store.VNNormalValue(call._vnPair.Liberal), ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_LSH));
            Assert.That(app.GetArg(0), Is.EqualTo(valueVN));
            Assert.That(app.GetArg(1), Is.EqualTo(shiftVN));
        });
    }

    [Test]
    public static void SelectCollectsConditionAndBothArmsInNativeOrder()
    {
        WithStore((compiler, store) =>
        {
            var normal = new int[3];
            var exceptions = new int[3];
            var operands = new GenTree[3];
            for (var i = 0; i < operands.Length; i++)
            {
                normal[i] = store.VNForExpr(null, TYP_INT);
                exceptions[i] = store.VNExcSetSingleton(store.VNForFunc(
                    TYP_REF, VNF_OverflowExc, normal[i]));
                operands[i] = compiler.gtNewIconNode(TYP_INT, i);
                operands[i]._vnPair.SetBoth(store.VNWithExc(normal[i], exceptions[i]));
            }
            var select = new GenTreeConditional(GT_SELECT, TYP_INT,
                operands[0], operands[1], operands[2]);

            compiler.fgValueNumberTree(select);

            var result = store.VNNormalValue(select._vnPair.Liberal);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_SELECT));
            for (var i = 0; i < operands.Length; i++)
            {
                Assert.That(app.GetArg(i), Is.EqualTo(normal[i]));
            }
            Assert.That(store.VNExceptionSet(select._vnPair.Liberal),
                Is.EqualTo(store.VNExcSetUnion(store.VNExcSetUnion(exceptions[0], exceptions[1]),
                    exceptions[2])));
        });
    }

    private static void WithStore(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 1;
            block.bbMemoryDef = (1 << (int)MemoryKind.GcHeap) | (1 << (int)MemoryKind.ByrefExposed);
            compiler.compCurBB = block;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgPredsComputed = true;
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
