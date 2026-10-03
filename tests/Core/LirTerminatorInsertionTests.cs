// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LirTerminatorInsertionTests
{
    [TestCase(BBJ_COND, GT_JTRUE)]
    [TestCase(BBJ_COND, GT_JCC)]
    [TestCase(BBJ_SWITCH, GT_SWITCH)]
    [TestCase(BBJ_SWITCH, GT_SWITCH_TABLE)]
    [TestCase(BBJ_RETURN, GT_RETURN)]
    [TestCase(BBJ_RETURN, GT_SWIFT_ERROR_RET)]
    [TestCase(BBJ_RETURN, GT_JMP)]
    [TestCase(BBJ_RETURN, GT_CALL)]
    [TestCase(BBJ_ALWAYS, GT_NO_OP)]
    [TestCase(BBJ_THROW, GT_NO_OP)]
    public static void InsertionPreservesTheTerminatorAndRangeOwnership(BBKinds kind, genTreeOps lastOper)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var block = NewBlock(compiler, kind);
            var prefix = new GenTree(GT_NO_OP, TYP_VOID);
            var lastType = lastOper switch {
                GT_JTRUE or GT_SWITCH or GT_RETURN => typeof(GenTreeUnOp),
                GT_JCC => typeof(GenTreeCC),
                GT_SWITCH_TABLE or GT_SWIFT_ERROR_RET => typeof(GenTreeOp),
                GT_JMP => typeof(GenTreeVal),
                GT_CALL => typeof(GenTreeCall),
                _ => typeof(GenTree),
            };
            var last = (GenTree)RuntimeHelpers.GetUninitializedObject(lastType);
            last._oper = lastOper;
            last._type = TYP_VOID;
            block.InsertAtEnd(prefix);
            block.InsertAtEnd(last);

            var firstInserted = new GenTree(GT_NO_OP, TYP_VOID);
            var lastInserted = new GenTree(GT_NO_OP, TYP_VOID);
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(firstInserted);
            range.InsertAtEnd(lastInserted);

            LIR.InsertBeforeTerminator(block, range);

            var hasTerminator = kind is BBJ_COND or BBJ_SWITCH or BBJ_RETURN;
            Assert.That(block.FirstNode, Is.SameAs(prefix));
            Assert.That(block.LastNode, Is.SameAs(hasTerminator ? last : lastInserted));
            Assert.That(firstInserted.Prev, Is.SameAs(hasTerminator ? prefix : last));
            Assert.That(firstInserted.Next, Is.SameAs(lastInserted));
            Assert.That(lastInserted.Prev, Is.SameAs(firstInserted));
            Assert.That(lastInserted.Next, Is.SameAs(hasTerminator ? last : null));
            Assert.That(last.Prev, Is.SameAs(hasTerminator ? lastInserted : prefix));
#if DEBUG
            Assert.That(range.IsEmpty, Is.True);
#else
            Assert.That(range.FirstNode, Is.SameAs(firstInserted));
            Assert.That(range.LastNode, Is.SameAs(lastInserted));
#endif
        });
    }

    [Test]
    public static void AnEmptyNonTerminatingBlockReceivesTheWholeRange()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var block = NewBlock(compiler, BBJ_ALWAYS);
            var node = new GenTree(GT_NO_OP, TYP_VOID);
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(node);

            LIR.InsertBeforeTerminator(block, range);

            Assert.That(block.FirstNode, Is.SameAs(node));
            Assert.That(block.LastNode, Is.SameAs(node));
            Assert.That(node.Prev, Is.Null);
            Assert.That(node.Next, Is.Null);
#if DEBUG
            Assert.That(range.IsEmpty, Is.True);
#else
            Assert.That(range.FirstNode, Is.SameAs(node));
            Assert.That(range.LastNode, Is.SameAs(node));
#endif
        });
    }

    [TestCase(false, false, "[--]")]
    [TestCase(false, true, "[-O]")]
    [TestCase(true, false, "[U-]")]
    [TestCase(true, true, "[UO]")]
    public static void FlagDumpUsesTheNativeTwoCharacterEncoding(bool unused, bool optional, string expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
#if DEBUG
            compiler.verbose = true;
#endif
            var node = compiler.gtNewIconNode(TYP_INT, 0);
            node.IsUnusedValue = unused;
            node.IsRegOptional = optional;
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                node.dumpLIRFlags();
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

#if DEBUG
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
#else
            Assert.That(stream.Length, Is.Zero);
#endif
        });
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        compiler.compRationalIRForm = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif

        return BasicBlock.New(compiler, kind);
    }
}
