// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ConditionNodeConstructionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void SourceConstructionPreservesMetadataWithoutTransferringLirLinks(bool threaded)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            var left = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            var right = compiler.gtNewDconNode(TYP_DOUBLE, 2.0);
            var source = new GenTreeOp(GT_NE, TYP_INT, left, right) {
                Flags = GTF_DONT_CSE | GTF_RELOP_NAN_UN,
            };
            source._vnPair.SetBoth(123);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            if (threaded)
            {
                block.InsertAtEnd(left);
                block.InsertAtEnd(right);
                block.InsertAtEnd(source);
            }

            var replacement = new GenTreeCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.FNEU), source,
                threaded ? NodeThreading.LIR : NodeThreading.None);

            Assert.That(replacement.Condition.Code, Is.EqualTo(GenCondition.FNEU));
            Assert.That(replacement.Oper, Is.EqualTo(GT_SETCC));
            Assert.That(replacement.Type, Is.EqualTo(TYP_INT));
            Assert.That(replacement.Flags, Is.EqualTo(source.Flags & GTF_NODE_MASK));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(123));
            Assert.That(replacement._vnPair.Conservative, Is.EqualTo(123));
            Assert.That(replacement.Prev, Is.Null);
            Assert.That(replacement.Next, Is.Null);
            Assert.That(source.Prev, threaded ? Is.SameAs(right) : Is.Null);
#if DEBUG
            Assert.That(replacement.TreeId, Is.EqualTo(source.TreeId));
#endif

            // Native SetOper retains all flags and clears VNs, unlike the
            // source constructor's common-flag filtering and VN preservation.
            replacement.Flags = source.Flags;
            replacement.SetOper(GT_SETCC);

            Assert.That(replacement.Flags, Is.EqualTo(source.Flags));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(replacement._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(source._vnPair.Liberal, Is.EqualTo(123));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
