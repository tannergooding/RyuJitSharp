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
    public static void InlinePlaceholdersPreserveEffectsAndUnusedNodeIdentity(bool nested, bool unused)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            GenTree value = unused
                ? compiler.gtNewIconNode(TYP_INT, 1)
                : compiler.gtNewIndir(TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 1));
            var sourceBlock = new BasicBlock(null, null);
            sourceBlock.SetFlags(BasicBlockFlags.BBF_HAS_NEWARR);
            compiler.compCurBB = new BasicBlock(null, null);
            var inner = new GenTreeRetExpr(TYP_INT, call) {
                Flags = GenTreeFlags.GTF_CALL,
                SubstExpr = value,
                SubstBB = sourceBlock,
            };
            var placeholder = nested
                ? new GenTreeRetExpr(TYP_INT, call) { Flags = GenTreeFlags.GTF_CALL, SubstExpr = inner }
                : inner;
            var parent = new GenTreeOp(GT_COMMA, TYP_INT, placeholder, compiler.gtNewIconNode(TYP_INT, 2)) {
                Flags = GenTreeFlags.GTF_CALL,
            };
            var walker = new SubstitutePlaceholdersAndDevirtualizeWalker(compiler);
            GenTree use = placeholder;
            Assert.That(walker.PreOrderVisit(ref use, unused ? parent : null), Is.EqualTo(Compiler.fgWalkResult.WALK_CONTINUE));
            Assert.That(walker.PostOrderVisit(ref use, parent), Is.EqualTo(Compiler.fgWalkResult.WALK_CONTINUE));
            Assert.That(walker.MadeChanges, Is.True);
            Assert.That(compiler.compCurBB.HasFlag(BasicBlockFlags.BBF_HAS_NEWARR), Is.True);

            if (unused)
            {
                Assert.That(use, Is.SameAs(placeholder));
                Assert.That(use.Oper, Is.EqualTo(GT_NOP));
                Assert.That(use.Type, Is.EqualTo(TYP_VOID));
            }
            else
            {
                Assert.That(use, Is.SameAs(value));
                Assert.That(parent.Flags & GenTreeFlags.GTF_ALL_EFFECT,
                    Is.EqualTo(GenTreeFlags.GTF_CALL | (value.Flags & GenTreeFlags.GTF_ALL_EFFECT)));
                Assert.That(parent.Flags & GenTreeFlags.GTF_EXCEPT, Is.Not.Zero);
            }
        });
    }

    [Test]
    public static void InlinePlaceholderRetypesNativeIntegerIndirectionAsByref()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_BYREF, gtCallTypes.CT_USER_FUNC, null);
            var value = compiler.gtNewIndir(Globals.TYP_I_IMPL, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 1));
            var placeholder = new GenTreeRetExpr(TYP_BYREF, call) { Flags = GenTreeFlags.GTF_CALL, SubstExpr = value };
            var stmt = new Statement(placeholder, 1);
            var walker = new SubstitutePlaceholdersAndDevirtualizeWalker(compiler);
            Assert.That(walker.WalkStatement(stmt), Is.SameAs(stmt));
            Assert.That(stmt.RootNode, Is.SameAs(value));
            Assert.That(value.Type, Is.EqualTo(TYP_BYREF));
        });
    }

    [Test]
    public static void InlinePlaceholderSelfStoreBashesOriginalNode()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var placeholder = new GenTreeRetExpr(TYP_INT, call) {
                Flags = GenTreeFlags.GTF_CALL,
                SubstExpr = compiler.gtNewLclvNode(TYP_INT, 0),
            };
            var store = new GenTreeLclVar(TYP_INT, 0, placeholder) {
                Flags = GenTreeFlags.GTF_CALL | GenTreeFlags.GTF_ASG,
            };
            var stmt = new Statement(store, 1);
            var walker = new SubstitutePlaceholdersAndDevirtualizeWalker(compiler);
            Assert.That(walker.WalkStatement(stmt), Is.SameAs(stmt));
            Assert.That(stmt.RootNode, Is.SameAs(store));
            Assert.That(store.Oper, Is.EqualTo(GT_NOP));
            Assert.That(walker.MadeChanges, Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    public static void InlinePlaceholderBranchFoldingPreservesEffectsAndEdges(int condition)
    {
        WithCompiler(compiler => {
            compiler.fgPredsComputed = true;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBKinds.BBJ_COND);
            var left = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var right = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            left.bbRefs = 0;
            right.bbRefs = 0;
            compiler.compCurBB = block;
            var trueEdge = compiler.fgAddRefPred(left, block);
            var falseEdge = compiler.fgAddRefPred(right, block);
            block.SetCond(trueEdge, falseEdge);
            trueEdge.Likelihood = 0.5;
            falseEdge.Likelihood = 0.5;
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var firstEffect = new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 1)) { Flags = GenTreeFlags.GTF_ASG };
            var secondEffect = new GenTreeLclVar(TYP_INT, 1, compiler.gtNewIconNode(TYP_INT, 2)) { Flags = GenTreeFlags.GTF_ASG };
            var value = compiler.gtNewCommaNode(TYP_INT, firstEffect,
                compiler.gtNewCommaNode(TYP_INT, secondEffect, compiler.gtNewIconNode(TYP_INT, condition)));
            var placeholder = new GenTreeRetExpr(TYP_INT, call) { Flags = GenTreeFlags.GTF_CALL, SubstExpr = value };
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, placeholder) { Flags = GenTreeFlags.GTF_CALL };
            var stmt = new Statement(branch, 1);
            compiler.fgInsertStmtAtEnd(block, stmt);
            var walker = new SubstitutePlaceholdersAndDevirtualizeWalker(compiler);
            Assert.That(walker.WalkStatement(stmt), Is.SameAs(stmt));
            Assert.That(stmt.RootNode, Is.SameAs(branch));
            Assert.That(branch.Oper, Is.EqualTo(GT_NOP));
            Assert.That(block.Kind, Is.EqualTo(BBKinds.BBJ_ALWAYS));
            Assert.That(block.TargetEdge, Is.SameAs(condition == 0 ? falseEdge : trueEdge));
            Assert.That(block.Target.bbRefs, Is.EqualTo(1));
            Assert.That((condition == 0 ? left : right).bbRefs, Is.Zero);
            var first = block.FirstStmt ?? throw new InvalidOperationException("Missing first side effect.");
            var second = first.NextStmt ?? throw new InvalidOperationException("Missing second side effect.");
            Assert.That(first.RootNode, Is.SameAs(firstEffect));
            Assert.That(second.RootNode, Is.SameAs(secondEffect));
            Assert.That(second.NextStmt, Is.SameAs(stmt));
            Assert.That(stmt.PrevStmt, Is.SameAs(second));
            Assert.That(first.PrevStmt, Is.SameAs(stmt));
            Assert.That(compiler.Metrics.InlinerBranchFold, Is.EqualTo(1));
        });
    }

    [TestCase(NodeThreading.None)]
    [TestCase(NodeThreading.AllLocals)]
    [TestCase(NodeThreading.AllTrees)]
    public static void NewStatementsRespectCurrentThreading(NodeThreading threading)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
            compiler.lvaCount = 1;
            compiler.fgNodeThreading = threading;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 1);
            var root = new GenTreeOp(GT_ADD, TYP_INT, local, constant);
            var stmt = compiler.fgNewStmtFromTree(root, threading == NodeThreading.AllTrees ? new BasicBlock(null, null) : null);

            if (threading == NodeThreading.None)
            {
                Assert.That(stmt.TreeListBegin, Is.Null);
            }
            else
            {
                Assert.That(stmt.TreeListBegin, Is.SameAs(local));
                Assert.That(local.Prev, Is.Null);

                if (threading == NodeThreading.AllLocals)
                {
                    Assert.That(stmt.TreeListEnd, Is.SameAs(local));
                    Assert.That(local.Next, Is.Null);
                }
                else
                {
                    Assert.That(local.Next, Is.SameAs(constant));
                    Assert.That(constant.Next, Is.SameAs(root));
                    Assert.That(root.Next, Is.Null);
                }
            }
        }, minOpts: true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LocalOnlyVisitorAbortDoesNotValidateUnvisitedOperands(bool store)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewIconNode(TYP_INT, 1);
            GenTree tree = store
                ? new GenTreeLclVar(TYP_INT, 0, value)
                : new GenTreeOp(GT_ADD, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0), value);
            var visitor = new AbortingLocalVisitor();
            Assert.That(visitor.WalkTree(ref tree, null), Is.EqualTo(Compiler.fgWalkResult.WALK_ABORT));
            Assert.That(visitor.Count, Is.EqualTo(1));
        });
    }

    private struct AbortingLocalVisitor() : IGenTreeVisitor<AbortingLocalVisitor>
    {
        private readonly Stack<GenTree> _ancestors = [];
        public int Count;

        public static bool DoPreOrder => true;

        public static bool DoLclVarsOnly => true;

        public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            Count++;
            return Compiler.fgWalkResult.WALK_ABORT;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => Compiler.fgWalkResult.WALK_CONTINUE;

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<AbortingLocalVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    [TestCase("sibling")]
    [TestCase("reversed")]
    [TestCase("ancestor")]
    [TestCase("operands")]
    [TestCase("root")]
    [TestCase("none")]
    [TestCase("shared")]
    public static void SplitTreePreservesExecutionOrderAndWritableUseIdentity(string shape)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[8];
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var two = compiler.gtNewIconNode(TYP_INT, 2);
            var before = new GenTreeOp(GT_ADD, TYP_INT, one, two);
            var second = new GenTreeOp(GT_SUB, TYP_INT, one, two);
            GenTree split = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            GenTree root;
            GenTreeOp? owner;
            var firstOperand = false;
            GenTree[] spilled;

            switch (shape)
            {
                case "reversed":
                {
                    owner = new GenTreeOp(GT_ADD, TYP_INT, split, before) { IsReverseOp = true };
                    root = owner;
                    firstOperand = true;
                    spilled = [before];
                    break;
                }

                case "ancestor":
                {
                    owner = new GenTreeOp(GT_ADD, TYP_INT, split, one);
                    root = new GenTreeOp(GT_ADD, TYP_INT, before, owner);
                    firstOperand = true;
                    spilled = [before];
                    break;
                }

                case "operands":
                {
                    split = new GenTreeOp(GT_ADD, TYP_INT, before, second);
                    owner = new GenTreeOp(GT_ADD, TYP_INT, one, split);
                    root = owner;
                    spilled = [before, second];
                    break;
                }

                case "root":
                {
                    split = new GenTreeOp(GT_ADD, TYP_INT, before, second);
                    root = split;
                    owner = null;
                    spilled = [before, second];
                    break;
                }

                case "none":
                case "shared":
                {
                    owner = new GenTreeOp(GT_ADD, TYP_INT, split, shape == "shared" ? split : before);
                    root = owner;
                    firstOperand = true;
                    spilled = [];
                    break;
                }

                default:
                {
                    owner = new GenTreeOp(GT_ADD, TYP_INT, before, split);
                    root = owner;
                    spilled = [before];
                    break;
                }
            }

            var stmt = new Statement(root, 1);
            stmt.PrevStmt = stmt;
            var block = new BasicBlock(null, null) { FirstStmt = stmt };
            ref var use = ref compiler.gtSplitTree(block, stmt, split, out var first, out var changed);
            Assert.That(use, Is.SameAs(split));
            Assert.That(changed, Is.EqualTo(spilled.Length != 0));
            Assert.That(compiler.lvaCount, Is.EqualTo(spilled.Length));
            var current = first;

            for (var i = 0; i < spilled.Length; i++)
            {
                if (current is null)
                {
                    throw new InvalidOperationException("Missing spill statement.");
                }

                Assert.That(current.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(current.RootNode.AsLclVarCommon().Data, Is.SameAs(spilled[i]));
                Assert.That(current.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(i));
                Assert.That(compiler.lvaTable[i].lvSingleDef, Is.True);
                current = current.NextStmt;
            }

            Assert.That(current, Is.SameAs(spilled.Length == 0 ? null : stmt));
            Assert.That(block.FirstStmt, Is.SameAs(first ?? stmt));

            var replacement = compiler.gtNewIconNode(TYP_INT, 3);
            use = replacement;
            Assert.That(owner is null ? stmt.RootNode : firstOperand ? owner.Op1 : owner.Op2, Is.SameAs(replacement));

            if (shape == "shared")
            {
                Assert.That(root.AsOp().Op2, Is.SameAs(split));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalSetsPreserveMembershipAcrossRepresentationsAndReuse(bool expandLeft, bool expandRight)
    {
        WithCompiler(compiler => {
            var left = new LclVarSet();
            var right = new LclVarSet();
            Assert.That(left.IsEmpty, Is.True);
            Assert.That(left.Intersects(right), Is.False);
            left.Add(compiler, 1);
            right.Add(compiler, 2);

            if (expandLeft)
            {
                left.Add(compiler, 1);
            }

            if (expandRight)
            {
                right.Add(compiler, 3);
            }

            Assert.That(left.IsEmpty, Is.EqualTo(!expandLeft));
            Assert.That(left.Contains(1), Is.True);
            Assert.That(left.Contains(2), Is.False);
            Assert.That(left.Intersects(right), Is.False);
            Assert.That(right.Intersects(left), Is.False);
            right.Add(compiler, 1);
            Assert.That(left.Intersects(right), Is.True);
            Assert.That(right.Intersects(left), Is.True);
            left.Clear();
            Assert.That(left.IsEmpty, Is.True);
            Assert.That(left.Contains(1), Is.False);
            Assert.That(left.Intersects(right), Is.False);
            left.Add(compiler, 4);
            Assert.That(left.Contains(4), Is.True);
            Assert.That(left.Contains(1), Is.False);
            Assert.That(left.Intersects(right), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AliasSetsTrackOperandReadsAndContainedIndirections(bool contained)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
            compiler.lvaCount = 2;
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var store = new GenTreeLclVar(TYP_INT, 0, zero);
            var writes = new AliasSet();
            writes.AddNode(compiler, store);
            writes.AddNode(compiler, store);
            GenTree read = contained
                ? new GenTreeIndir(GT_IND, TYP_INT, compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0))
                : compiler.gtNewLclvNode(TYP_INT, 0);

            if (contained)
            {
                read.Flags |= GenTreeFlags.GTF_CONTAINED;
            }

            var user = new GenTreeOp(GT_ADD, TYP_INT, read, zero);
            var reads = new AliasSet();
            reads.AddNode(compiler, user);
            Assert.That(writes.WritesLocal(0), Is.True);
            Assert.That(writes.WritesLocal(1), Is.False);
            Assert.That(writes.InterferesWith(reads), Is.True);
            Assert.That(reads.InterferesWith(writes), Is.True);

            if (!contained)
            {
                Assert.That(writes.InterferesWith(new AliasSet.NodeInfo(compiler, user)), Is.True);
            }

            writes.Clear();
            Assert.That(writes.WritesAnyLocation, Is.False);
            Assert.That(writes.InterferesWith(reads), Is.False);
        });
    }

    [TestCase(-1, false, new[] { 1, 2 })]
    [TestCase(-1, true, new[] { 1 })]
    [TestCase(0, false, new[] { 1 })]
    [TestCase(2, false, new[] { 1, 2 })]
    [TestCase(4, false, new[] { 2 })]
    [TestCase(8, false, new int[0])]
    public static void LogicalDefinitionsRespectPromotedRanges(int offset, bool abort, int[] expected)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(12), lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0, lvFldOffset = 0 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0, lvFldOffset = 4 },
                new LclVarDsc { Type = TYP_INT },
            ];
            compiler.lvaCount = compiler.lvaTable.Length;
            GenTreeLclVarCommon store = offset < 0
                ? new GenTreeLclVar(TYP_STRUCT, 0, new GenTree(GT_NOP, TYP_STRUCT))
                : new GenTreeLclFld(TYP_INT, 0, (ushort)offset, compiler.gtNewIconNode(TYP_INT, 0), null);
            store.Flags |= GenTreeFlags.GTF_ASG;
            var aliases = new AliasSet();
            aliases.AddNode(compiler, store);
            Assert.That(aliases.WritesLocal(0), Is.True);
            Assert.That(aliases.WritesLocal(1), Is.EqualTo(offset is < 4 or 8));
            Assert.That(aliases.WritesLocal(2), Is.EqualTo(offset is -1 or >= 2));
            Assert.That(aliases.WritesLocal(3), Is.False);
            var visitor = new DefinitionVisitor(compiler, abort);
            var result = store.VisitLogicalLocalDefs(compiler, ref visitor);
            Assert.That(visitor.Locals, Is.EqualTo(expected));
            Assert.That(result, Is.EqualTo(abort ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue));
            Assert.That(store.HasAnyLocalDefs(compiler), Is.True);

            foreach (var entry in visitor.Definitions)
            {
                Assert.That(entry.Node, Is.SameAs(store));
                Assert.That(entry.Index, Is.EqualTo(entry.Local - 1));
                Assert.That(entry.Entire, Is.EqualTo(offset != 2));
                Assert.That(entry.Size, Is.EqualTo(offset == 2 ? 2 : 4));
                Assert.That(entry.Offset, Is.EqualTo(offset == 2 && entry.Local == 1 ? 2 : 0));
                Assert.That(entry.ValueOffset, Is.EqualTo(offset < 0 ? (entry.Local - 1) * 4 : offset == 2 && entry.Local == 2 ? 2 : 0));
                Assert.That(entry.StoreSize, Is.EqualTo(offset < 0 ? 12 : 4));
            }

            for (var local = 0; local < compiler.lvaCount; local++)
            {
                var affects = local == 0 ? expected.Length != 0 : Array.IndexOf(expected, local) >= 0;

                if (!abort)
                {
                    Assert.That(compiler.gtTreeHasLocalStore(store, local), Is.EqualTo(affects), $"V{local}");
                }
            }
        });
    }

    [TestCase(0, 4, true, 0, 4)]
    [TestCase(2, 4, true, 2, 2)]
    [TestCase(-2, 4, true, 0, 2)]
    [TestCase(4, 4, false, 0, 0)]
    [TestCase(-4, 4, false, 0, 0)]
    public static void FieldDefinitionOverlapPreservesSignedBounds(int offset, int size, bool overlaps, int relative, int affected)
    {
        WithCompiler(compiler => {
            var field = new LclVarDsc { Type = TYP_INT, lvIsStructField = true };
            Assert.That(compiler.gtStoreMayDefineField(field, offset, new ValueSize(size), out var actualOffset, out var actualSize), Is.EqualTo(overlaps));

            if (overlaps)
            {
                Assert.That(actualOffset, Is.EqualTo((nint)relative));
                Assert.That(actualSize.ExactSize, Is.EqualTo(affected));
            }

            Assert.That(compiler.gtStoreMayDefineField(field, offset, ValueSize.Unknown, out _, out actualSize), Is.True);
            Assert.That(actualSize.IsUnknown, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AsyncLogicalDefinitionsPreserveOffsetsAndAbort(bool abort)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16) }];
            compiler.lvaCount = 1;
#if DEBUG
            compiler.lvaTable[0].IsDefinedViaAddress = true;
#endif
            var resumed = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 8);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.SetIsAsync(default);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(resumed).WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            var visitor = new DefinitionVisitor(compiler, abort);
            Assert.That(call.VisitLogicalLocalDefs(compiler, ref visitor), Is.EqualTo(abort ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue));
            Assert.That(visitor.Locals, Has.Count.EqualTo(1));
            Assert.That(visitor.Locals[0], Is.Zero);
            var def = visitor.Definitions[0];
            Assert.That(def.Node, Is.SameAs(resumed));
            Assert.That(def.Index, Is.EqualTo(Globals.BAD_VAR_NUM));
            Assert.That(def.Entire, Is.False);
            Assert.That(def.Offset, Is.EqualTo(8));
            Assert.That(def.Size, Is.EqualTo(Globals.TARGET_POINTER_SIZE));
            Assert.That(call.IsEntireLocalDef(compiler, resumed), Is.False);
        });
    }

    private struct DefinitionVisitor(Compiler compiler, bool abort) : ILocalDefVisitor
    {
        public readonly List<int> Locals = [];
        public readonly List<(GenTree Node, int Local, int Index, bool Entire, int Offset, int Size, int ValueOffset, int StoreSize)> Definitions = [];

        public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            Locals.Add(def.LclNum);
            Definitions.Add((def.DefNode, def.LclNum, def.MultiDefIndex, def.IsEntire(compiler),
                (int)def.GetOffset(compiler), def.GetSize(compiler).ExactSize,
                (int)def.GetValueOffset(compiler), def.GetStoreSize(compiler).ExactSize));

            return abort ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue;
        }
    }

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
                {
                    tree = first;
                    expected = [];
                    break;
                }

                case "bashed":
                {
                    tree = new GenTreeOp(GT_ADD, TYP_INT, first, second);
                    tree.BashToNOP();
                    expected = [];
                    break;
                }

                case "unary":
                {
                    tree = new GenTreeUnOp(GT_NEG, TYP_INT, first);
                    expected = [first];
                    break;
                }

                case "void-return":
                {
                    tree = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
                    expected = [];
                    break;
                }

                case "binary":
                case "reverse-binary":
                {
                    tree = new GenTreeOp(GT_ADD, TYP_INT, first, second) {
                        IsReverseOp = shape == "reverse-binary",
                    };
                    expected = tree.IsReverseOp ? [second, first] : [first, second];
                    break;
                }

                case "lea-base":
                case "lea-index":
                case "reverse-lea-base":
                case "reverse-lea-index":
                {
                    var indexOnly = shape.EndsWith("index", StringComparison.Ordinal);
                    tree = new GenTreeAddrMode(TYP_BYREF, indexOnly ? null : first, indexOnly ? first : null, indexOnly ? (byte)2 : (byte)0, 0) {
                        IsReverseOp = shape.StartsWith("reverse", StringComparison.Ordinal),
                    };
                    expected = [first];
                    break;
                }

                case "phi-empty":
                case "phi":
                {
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
                }

                case "field-empty":
                case "fields":
                {
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
                }

                case "cmpxchg":
                {
                    tree = new GenTreeCmpXchg(TYP_INT, first, second, third);
                    expected = [first, second, third];
                    break;
                }

                case "select":
                {
                    tree = new GenTreeConditional(GT_SELECT, TYP_INT, first, second, third);
                    expected = [first, second, third];
                    break;
                }

                case "array":
                {
                    tree = new GenTreeArrElem(TYP_BYREF, first, 4, [second, third]);
                    expected = [first, second, third];
                    break;
                }

                case "intrinsic-empty":
                case "intrinsic":
                case "reverse-intrinsic":
                {
                    GenTree[] operands = shape switch {
                        "intrinsic-empty" => [],
                        "intrinsic" => [first, second, third],
                        _ => [first, second],
                    };
                    tree = new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_Vector_Create, TYP_INT, 16, operands) {
                        IsReverseOp = shape == "reverse-intrinsic",
                    };
                    expected = tree.IsReverseOp ? [second, first] : operands;
                    break;
                }

                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(shape));
                }
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
                first.EarlyNode = null;
                third.EarlyNode = null;
            }

            second.EarlyNode = null;

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

    [TestCase(false)]
    [TestCase(true)]
    public static void CallCloningPreservesAbsentEarlyNodesAndLateOrder(bool keepSetup)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
            compiler.lvaCount = 1;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1)));
            var second = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2)));
            first.LateNode = first.EarlyNode;
            first.EarlyNode = keepSetup ? compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 3)) : null;
            second.LateNode = second.EarlyNode;
            second.EarlyNode = null;
            LateHead(ref call.Args) = second;
            second.LateNext = first;

            CallArgs copy = default;
            copy.InternalCopyFrom(compiler, call.Args);
            CallArg[] arguments = [.. copy.Args];
            CallArg[] lateArguments = [.. copy.LateArgs];
            CallArg[] earlyArguments = [.. copy.EarlyArgs];
            CallArg[] expectedEarly = keepSetup ? [arguments[0]] : [];
            Assert.That(lateArguments, Is.EqualTo((CallArg[])[arguments[1], arguments[0]]));
            Assert.That(earlyArguments, Is.EqualTo(expectedEarly));
            Assert.That(arguments[1].EarlyNode, Is.Null);
            if (keepSetup)
            {
                Assert.That(arguments[0].EarlyNode?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(arguments[0].EarlyNode, Is.Not.SameAs(first.EarlyNode));
            }
            else
            {
                Assert.That(arguments[0].EarlyNode, Is.Null);
            }

            for (var i = 0; i < arguments.Length; i++)
            {
                Assert.That(arguments[i].Node, Is.SameAs(arguments[i].LateNode));
                Assert.That(arguments[i].Node, Is.Not.SameAs(i == 0 ? first.Node : second.Node));
                Assert.That(arguments[i].Node.AsIntCon().IconValue, Is.EqualTo((nint)(i + 1)));
            }

            var replacement = compiler.gtNewIconNode(TYP_INT, 4);
            arguments[1].NodeRef = replacement;
            Assert.That(arguments[1].LateNode, Is.SameAs(replacement));
            Assert.That(arguments[1].EarlyNode, Is.Null);
            Assert.That(second.Node.AsIntCon().IconValue, Is.EqualTo((nint)2));
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

    private static void WithCompiler(Action<Compiler> action, bool minOpts = false)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;

        if (minOpts)
        {
            flags.Set(JitFlags.JIT_FLAG_MIN_OPT);
        }

        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
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
