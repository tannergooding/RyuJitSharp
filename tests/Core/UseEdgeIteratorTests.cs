// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class UseEdgeIteratorTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void CallDefinitionsFollowOperandReads(bool retBuffer, bool abort)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [
                new LclVarDsc { Type = Globals.TYP_I_IMPL },
                new LclVarDsc { Type = TYP_INT },
                new LclVarDsc { Type = TYP_INT },
            ];
            compiler.lvaCount = compiler.lvaTable.Length;
#if DEBUG
            compiler.lvaTable[0].IsDefinedViaAddress = true;
            compiler.lvaTable[1].IsDefinedViaAddress = true;
#endif
            var resumed = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var ret = compiler.gtNewLclAddrNode(TYP_BYREF, 1, 0);
            var read = compiler.gtNewLclvNode(TYP_INT, 2);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.SetIsAsync(default);
            if (retBuffer)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(ret).WithWellKnownArg(WellKnownArg.RetBuffer));
            }
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(resumed).WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(read));

            List<GenTree> definitions = [];
            var result = call.VisitPhysicalLocalDefNodes(compiler, node => {
                definitions.Add(node);
                return abort ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue;
            });
            GenTree[] expectedDefinitions = retBuffer && !abort ? [resumed, ret] : [resumed];
            Assert.That(result, Is.EqualTo(abort ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue));
            Assert.That(definitions, Is.EqualTo(expectedDefinitions));

            var statement = new Statement(call, 1);
            var sequencer = new LocalSequencer(compiler);
            sequencer.Sequence(statement);
            GenTree[] expectedLocals = retBuffer ? [read, resumed, ret] : [read, resumed];
            List<GenTree> locals = [];
            for (var node = statement.TreeListBegin; node is not null; node = node.Next)
            {
                Assert.That(locals.Count, Is.LessThan(expectedLocals.Length));
                locals.Add(node);
            }
            Assert.That(locals, Is.EqualTo(expectedLocals));
            Assert.That(statement.TreeListEnd, Is.SameAs(expectedLocals[^1]));
            Assert.That(call.Next, Is.Null);
        });
    }

    [Test]
    public static void RestoredLirUtilitiesFindUsesAndValidateLinks()
    {
        WithCompiler(compiler => {
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            var user = new GenTreeOp(GT_ADD, TYP_INT, first, second);
            first.Next = second;
            second.Prev = first;
            second.Next = user;
            user.Prev = second;
            var range = new LIR.Range(first, user);

            Assert.That(range.TryGetUse(first, out var use), Is.True);
            Assert.That(use.Def(), Is.SameAs(first));
            Assert.That(use.User(), Is.SameAs(user));
            Assert.That(range.TryGetUse(user, out _), Is.False);
            Globals.CheckDoublyLinkedList(first);
#if DEBUG
            Assert.That(range.CheckLir(compiler), Is.True);
#endif
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void RecursiveExecutionOrderPreservesOperandSlots(bool reverse, bool replace)
    {
        WithCompiler(compiler => {
            GenTree first = compiler.gtNewIconNode(TYP_INT, 1);
            GenTree second = compiler.gtNewIconNode(TYP_INT, 2);
            GenTree replacement = compiler.gtNewIconNode(TYP_INT, 3);
            GenTree tree = new GenTreeOp(GT_SUB, TYP_INT, first, second) { IsReverseOp = reverse };
            var visitor = new ReplacingVisitor(replace ? second : null, replacement);
            _ = visitor.WalkTree(ref tree);
            GenTree[] expected = reverse ? [second, first] : [first, second];
            Assert.Multiple(() => {
                Assert.That(visitor.Seen, Is.EqualTo(expected));
                Assert.That(tree.AsOp().Op1, Is.SameAs(first));
                Assert.That(tree.AsOp().Op2, Is.SameAs(replace ? replacement : second));
            });
        });
    }

    private struct ReplacingVisitor(GenTree? target, GenTree replacement) : IGenTreeVisitor<ReplacingVisitor>
    {
        private readonly Stack<GenTree> _ancestors = [];
        public readonly List<GenTree> Seen = [];
        public static bool DoPreOrder => true;
        public static bool UseExecutionOrder => true;

        public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (user is not null)
            {
                Seen.Add(use);
                if (use == target)
                {
                    use = replacement;
                }
            }
            return Compiler.fgWalkResult.WALK_CONTINUE;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
            => Compiler.fgWalkResult.WALK_CONTINUE;

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user = null)
            => IGenTreeVisitor<ReplacingVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    [TestCase("leaf")]
    [TestCase("bashed")]
    [TestCase("unary")]
    [TestCase("void-return")]
    [TestCase("binary")]
    [TestCase("reverse-binary")]
    [TestCase("lea-base")]
    [TestCase("lea-index")]
    [TestCase("reverse-lea-base")]
    [TestCase("reverse-lea-index")]
    [TestCase("phi-empty")]
    [TestCase("phi")]
    [TestCase("field-empty")]
    [TestCase("fields")]
    [TestCase("cmpxchg")]
    [TestCase("select")]
    [TestCase("array")]
    [TestCase("intrinsic-empty")]
    [TestCase("intrinsic")]
    [TestCase("reverse-intrinsic")]
    public static void NativeOperandOrderWritableEdgesAndReset(string shape)
    {
        WithCompiler(compiler => {
            GenTree first = compiler.gtNewIconNode(TYP_INT, 1);
            GenTree second = compiler.gtNewIconNode(TYP_INT, 2);
            GenTree third = compiler.gtNewIconNode(TYP_INT, 3);
            GenTree tree;
            GenTree[] expected;

            switch (shape)
            {
                case "leaf":
                    tree = first;
                    expected = [];
                    break;
                case "bashed":
                    tree = new GenTreeOp(GT_ADD, TYP_INT, first, second);
                    tree.BashToNOP();
                    expected = [];
                    break;
                case "unary":
                    tree = new GenTreeUnOp(GT_NEG, TYP_INT, first);
                    expected = [first];
                    break;
                case "void-return":
                    tree = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
                    expected = [];
                    break;
                case "binary":
                case "reverse-binary":
                    tree = new GenTreeOp(GT_ADD, TYP_INT, first, second) {
                        IsReverseOp = shape == "reverse-binary",
                    };
                    expected = tree.IsReverseOp ? [second, first] : [first, second];
                    break;
                case "lea-base":
                case "lea-index":
                case "reverse-lea-base":
                case "reverse-lea-index":
                    var indexOnly = shape.EndsWith("index", StringComparison.Ordinal);
                    tree = new GenTreeAddrMode(TYP_BYREF, indexOnly ? null : first, indexOnly ? first : null, indexOnly ? (byte)2 : (byte)0, 0) {
                        IsReverseOp = shape.StartsWith("reverse", StringComparison.Ordinal),
                    };
                    expected = [first];
                    break;
                case "phi-empty":
                case "phi":
                    var phi = new GenTreePhi(TYP_INT);
                    if (shape == "phi")
                    {
                        var predecessor = new BasicBlock(null, null);
                        first = new GenTreePhiArg(TYP_INT, 0, 1, predecessor);
                        second = new GenTreePhiArg(TYP_INT, 0, 2, predecessor);
                        phi.FirstUse = new GenTreePhi.Use(first, new GenTreePhi.Use(second));
                        expected = [first, second];
                    }
                    else
                    {
                        expected = [];
                    }
                    tree = phi;
                    break;
                case "field-empty":
                case "fields":
                    var fields = new GenTreeFieldList();
                    if (shape == "fields")
                    {
                        fields.AddField(compiler, first, 0, TYP_INT);
                        fields.AddField(compiler, second, 4, TYP_INT);
                        expected = [first, second];
                    }
                    else
                    {
                        expected = [];
                    }
                    tree = fields;
                    break;
                case "cmpxchg":
                    tree = new GenTreeCmpXchg(TYP_INT, first, second, third);
                    expected = [first, second, third];
                    break;
                case "select":
                    tree = new GenTreeConditional(GT_SELECT, TYP_INT, first, second, third);
                    expected = [first, second, third];
                    break;
                case "array":
                    tree = new GenTreeArrElem(TYP_BYREF, first, 4, [second, third]);
                    expected = [first, second, third];
                    break;
                case "intrinsic-empty":
                case "intrinsic":
                case "reverse-intrinsic":
                    GenTree[] operands = shape switch {
                        "intrinsic-empty" => [],
                        "intrinsic" => [first, second, third],
                        _ => [first, second],
                    };
                    tree = new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_Illegal, TYP_INT, 16, operands) {
                        IsReverseOp = shape == "reverse-intrinsic",
                    };
                    expected = tree.IsReverseOp ? [second, first] : operands;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape));
            }

            AssertEdges(tree, expected);
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public static void CallEarlyLateAndControlTransitions(bool early, bool late, bool control)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1)));
            var second = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2)));
            var third = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 3)));
            var expected = new List<GenTree>();
            if (early)
            {
                expected.Add(first.Node);
                expected.Add(third.Node);
            }
            else
            {
                first.EarlyNodeRef = null;
                third.EarlyNodeRef = null;
            }
            second.EarlyNodeRef = null;

            if (late)
            {
                first.LateNode = compiler.gtNewIconNode(TYP_INT, 4);
                second.LateNode = compiler.gtNewIconNode(TYP_INT, 5);
                third.LateNode = compiler.gtNewIconNode(TYP_INT, 6);
                LateHead(ref call.Args) = second;
                second.LateNext = first;
                first.LateNext = third;
                expected.Add(second.LateNode);
                expected.Add(first.LateNode);
                expected.Add(third.LateNode);
            }

            if (control)
            {
                call.ControlExpr = compiler.gtNewIconNode(TYP_INT, 7);
                expected.Add(call.ControlExpr);
            }

            AssertEdges(call, [.. expected]);
        });
    }

    private static void AssertEdges(GenTree tree, GenTree[] expected)
    {
        var iterator = new GenTreeUseEdgesList(tree).GetEnumerator();
        var replacements = new GenTree[expected.Length];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.That(iterator.MoveNext(), Is.True, $"Missing edge {i}");
            Assert.That(iterator.Current, Is.SameAs(expected[i]));
            replacements[i] = expected[i] is GenTreePhiArg phi
                ? new GenTreePhiArg(TYP_INT, 0, 10 + i, phi.PredBB)
                : new GenTreeLclVar(expected[i].Type, 10 + i);
            iterator.Current = replacements[i];
        }
        Assert.That(iterator.MoveNext(), Is.False);
        Assert.That(iterator.MoveNext(), Is.False);

        iterator.Reset();
        if (expected.Length > 0)
        {
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.Current, Is.SameAs(replacements[0]));
        }
        iterator.Reset();
        for (var i = 0; i < replacements.Length; i++)
        {
            Assert.That(iterator.MoveNext(), Is.True, $"Missing reset edge {i}");
            Assert.That(iterator.Current, Is.SameAs(replacements[i]));
        }
        Assert.That(iterator.MoveNext(), Is.False);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lateHead")]
    private static extern ref CallArg? LateHead(ref CallArgs args);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
