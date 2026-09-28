// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if DEBUG
[NonParallelizable]
internal static unsafe class MorphStressTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressModeNames")]
    private static extern ref byte* StressNames(ref JitConfigValues config);

    [Test]
    public static void DisabledStressPreservesLocalsAndMultiplication()
    {
        WithCompiler((compiler, block) =>
        {
            compiler.compAllowStress = false;
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 3));
            var statement = Append(compiler, block, multiply);

            compiler.lvaStressLclFld();
            compiler.fgStress64RsltMul();

            Assert.That(statement.RootNode, Is.SameAs(multiply));
            Assert.That(multiply.Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(compiler.lvaTable[2].Type, Is.EqualTo(TYP_INT));
        });
    }

    [TestCase(GT_MUL, TYP_INT, false, true)]
    [TestCase(GT_MUL, TYP_INT, true, false)]
    [TestCase(GT_MUL, TYP_LONG, false, false)]
    [TestCase(GT_ADD, TYP_INT, false, false)]
    public static void MultiplicationStressWidensOnlyUncheckedIntProducts(
        genTreeOps oper, var_types type, bool overflow, bool transforms)
    {
        WithCompiler((compiler, block) =>
        {
            var left = compiler.gtNewZeroConNode(type);
            var right = compiler.gtNewZeroConNode(type);
            var tree = compiler.gtNewBinaryNode(oper, type, left, right);
            if (overflow)
            {
                tree.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }
            var statement = Append(compiler, block, tree);

            compiler.fgStress64RsltMul();

            if (!transforms)
            {
                Assert.That(statement.RootNode, Is.SameAs(tree));
                Assert.That(tree.Op1, Is.SameAs(left));
                return;
            }

            Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_CAST));
            Assert.That(statement.RootNode.Type, Is.EqualTo(TYP_INT));
            Assert.That(statement.RootNode.AsCast().Op1, Is.SameAs(tree));
            Assert.That(tree.Type, Is.EqualTo(TYP_LONG));
            Assert.That(tree.Op1.AsCast().Op1, Is.SameAs(left));
            Assert.That(tree.Op2.AsCast().Op1, Is.SameAs(right));
            Assert.That(tree.Op1.Type, Is.EqualTo(TYP_LONG));
            Assert.That(tree.Op2.Type, Is.EqualTo(TYP_LONG));
            Assert.That(tree.Op1._debugFlags & GenTreeDebugFlags.GTF_DEBUG_CAST_DONT_FOLD,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_CAST_DONT_FOLD));
            Assert.That(tree.Op2._debugFlags & GenTreeDebugFlags.GTF_DEBUG_CAST_DONT_FOLD,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_CAST_DONT_FOLD));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiplicationTraversalSkipsOnlyRewrittenSubtrees(bool checkedParent)
    {
        WithCompiler((compiler, block) =>
        {
            var inner = compiler.gtNewBinaryNode(GT_MUL, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 3));
            var outer = compiler.gtNewBinaryNode(GT_MUL, TYP_INT, inner, compiler.gtNewIconNode(TYP_INT, 4));
            if (checkedParent)
            {
                outer.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }
            _ = Append(compiler, block, outer);

            compiler.fgStress64RsltMul();

            Assert.That(inner.Type, Is.EqualTo(checkedParent ? TYP_LONG : TYP_INT));
            Assert.That(outer.Type, Is.EqualTo(checkedParent ? TYP_INT : TYP_LONG));
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void LocalFieldStressBuildsOnePaddedLayoutAndRewritesEveryUse(var_types type)
    {
        WithCompiler((compiler, block) =>
        {
            compiler.lvaTable[2].Type = type;
            var store = compiler.gtNewStoreLclVarNode(2, compiler.gtNewZeroConNode(type));
            var load = compiler.gtNewLclvNode(type, 2);
            var storeStatement = Append(compiler, block, store);
            var loadStatement = Append(compiler, block, load);

            compiler.lvaStressLclFld();

            var padding = varTypeIsGC(type) ? TARGET_POINTER_SIZE : 2;
            var layout = compiler.lvaTable[2].Layout ?? throw new InvalidOperationException("Expected a padded layout.");
            Assert.That(compiler.lvaTable[2].Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(compiler.lvaTable[2].IsAddressExposed, Is.True);
            Assert.That(layout.Size, Is.EqualTo(roundUp(padding + (type.StSz * sizeof(int)), TARGET_POINTER_SIZE)));
            Assert.That(layout.IsCustomLayout, Is.True);
            Assert.That(layout.HasGCPtr, Is.EqualTo(varTypeIsGC(type)));
            if (varTypeIsGC(type))
            {
                Assert.That(layout.GetGCPtr(0), Is.EqualTo(CorInfoGCType.TYPE_GC_NONE));
                Assert.That(layout.GetGCPtr(padding / TARGET_POINTER_SIZE),
                    Is.EqualTo(type is TYP_REF ? CorInfoGCType.TYPE_GC_REF : CorInfoGCType.TYPE_GC_BYREF));
            }
            Assert.That(layout.ClassName, Is.EqualTo($"{type.Name}_{layout.Size}_Stress"));
            Assert.That(storeStatement.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(loadStatement.RootNode.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(storeStatement.RootNode.AsLclFld().LclOffs, Is.EqualTo(padding));
            Assert.That(loadStatement.RootNode.AsLclFld().LclOffs, Is.EqualTo(padding));
            Assert.That(storeStatement.RootNode.AsLclFld().Data, Is.SameAs(store.Data));
            Assert.That(storeStatement.RootNode.Flags & GTF_VAR_USEASG, Is.EqualTo(GTF_VAR_USEASG));
            Assert.That(loadStatement.RootNode.Flags & GTF_GLOB_REF, Is.EqualTo(GTF_GLOB_REF));
            Assert.That(storeStatement.RootNode.TreeId, Is.EqualTo(store.TreeId));
            Assert.That(loadStatement.RootNode.TreeId, Is.EqualTo(load.TreeId));
        });
    }

    [TestCase("field")]
    [TestCase("context")]
    [TestCase("parameter")]
    [TestCase("temporary")]
    [TestCase("keep-type")]
    [TestCase("pinned")]
    [TestCase("small-type")]
    [TestCase("patchpoint")]
    [TestCase("recursive-tailcall")]
    [TestCase("odd-local")]
    public static void LocalFieldStressRejectsIneligibleLocalsBeforeRewriting(string reason)
    {
        WithCompiler((compiler, block) =>
        {
            var number = reason == "odd-local" ? 1 : 2;
            var local = compiler.gtNewLclvNode(TYP_INT, number);
            var statement = Append(compiler, block, local);
            switch (reason)
            {
                case "field":
                {
                    _ = Append(compiler, block, compiler.gtNewLclFldNode(TYP_INT, number, 0));
                    break;
                }

                case "context":
                {
                    local.Flags |= GTF_VAR_CONTEXT;
                    break;
                }

                case "parameter":
                {
                    compiler.lvaTable[number].lvIsParam = true;
                    break;
                }

                case "temporary":
                {
                    compiler.info.compLocalsCount = number;
                    break;
                }

                case "keep-type":
                {
                    compiler.lvaTable[number].lvKeepType = true;
                    break;
                }

                case "pinned":
                {
                    compiler.lvaTable[number].lvPinned = true;
                    break;
                }

                case "small-type":
                {
                    compiler.lvaTable[number].Type = TYP_BYTE;
                    break;
                }

                case "patchpoint":
                {
                    compiler.optMethodFlags |= OMF_HAS_PATCHPOINT;
                    break;
                }

                case "recursive-tailcall":
                {
                    compiler.optMethodFlags |= OMF_HAS_RECURSIVE_TAILCALL;
                    break;
                }

                case "odd-local":
                {
                    break;
                }

                default:
                {
                    throw new ArgumentException("Unknown exclusion.", nameof(reason));
                }
            }

            compiler.lvaStressLclFld();

            Assert.That(statement.RootNode, Is.SameAs(local));
            Assert.That(compiler.lvaTable[number].lvNoLclFldStress, Is.True);
            Assert.That(compiler.lvaTable[number].Type, Is.Not.EqualTo(TYP_STRUCT));
        });
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = compiler.fgNewStmtFromTree(root);
        compiler.fgInsertStmtAtEnd(block, statement);
        return statement;
    }

    private static void WithCompiler(Action<Compiler, BasicBlock> action)
    {
        SsaLivenessTests.WithCompiler(3, compiler =>
        {
            compiler.compAllowStress = true;
            compiler.info.compMethodName = nameof(MorphStressTests);
            compiler.info.compLocalsCount = 3;
            compiler.fgNodeThreading = NodeThreading.None;
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = block;

            fixed (byte* names = "STRESS_LCL_FLDS STRESS_64RSLT_MUL\0"u8)
            {
                StressNames(ref JitConfig) = names;
                action(compiler, block);
            }
        });
    }
}
#endif
