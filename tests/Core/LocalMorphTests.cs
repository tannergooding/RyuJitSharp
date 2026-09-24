// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LocalMorphTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ValuesKeepTheExactOwningSlotForSharedOperands(bool reversed)
    {
        WithCompiler(compiler => {
            var shared = compiler.gtNewIconNode(TYP_INT, 1);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, shared, shared);
            tree.Flags |= reversed ? GTF_REVERSE_OPS : GTF_EMPTY;
            var stmt = compiler.gtNewStmt(tree);
            var value = new LocalAddressVisitor.Value(stmt, ref tree.Op2Ref, tree);
            var replacement = compiler.gtNewIconNode(TYP_INT, 2);
            value.Use = replacement;

            Assert.That(tree.Op1, Is.SameAs(shared));
            Assert.That(tree.Op2, Is.SameAs(replacement));
            Assert.That(value.Node, Is.SameAs(replacement));

            var root = new LocalAddressVisitor.Value(stmt, ref stmt.RootNodeRef, owner: null) {
                Use = replacement,
            };
            Assert.That(stmt.RootNode, Is.SameAs(replacement));
            Assert.That(root.Node, Is.SameAs(replacement));

            var call = compiler.gtNewHelperCallNode(TYP_INT, CorInfoHelpFunc.CORINFO_HELP_UDIV, shared, shared);
            var callStmt = compiler.gtNewStmt(call);
            var secondArg = call.Args.GetUserArgByIndex(1);
            assert(secondArg is not null);
            var argument = new LocalAddressVisitor.Value(callStmt, ref secondArg.EarlyNodeRef, call) {
                Use = replacement,
            };

            Assert.That(call.Args.GetUserArgByIndex(0)?.Node, Is.SameAs(shared));
            Assert.That(secondArg.Node, Is.SameAs(replacement));
            Assert.That(argument.Node, Is.SameAs(replacement));
        });
    }

    [TestCase(uint.MaxValue, 0u, true)]
    [TestCase(uint.MaxValue, 1u, false)]
    [TestCase(uint.MaxValue - 1, 1u, true)]
    public static void AddressOffsetsDoNotWrap(uint offset, uint delta, bool succeeds)
    {
        WithCompiler(compiler => {
            var stmt = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_I_IMPL, 0));
            var source = new LocalAddressVisitor.Value(stmt, ref stmt.RootNodeRef, owner: null);
            source.Address(2, offset);
            var result = new LocalAddressVisitor.Value(stmt, ref stmt.RootNodeRef, owner: null);

            Assert.That(result.AddOffset(ref source, delta), Is.EqualTo(succeeds));
            Assert.That(result.IsAddress, Is.EqualTo(succeeds));

            if (succeeds)
            {
                Assert.That(result.LclNum, Is.EqualTo(2));
                Assert.That(result.Offset, Is.EqualTo(offset + delta));
            }
#if DEBUG
            Assert.That(source.IsConsumed, Is.EqualTo(succeeds));
#endif
        });
    }

    [TestCase(8, 0u, 8, false)]
    [TestCase(8, 4u, 4, false)]
    [TestCase(8, 4u, 8, true)]
    [TestCase(8, uint.MaxValue, 1, true)]
    [TestCase(65536, 65534u, 1, false)]
    [TestCase(65536, 65535u, 1, true)]
    public static void WideAccessChecksTheLastByteWithoutOverflow(int localSize, uint offset, int accessSize, bool wide)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(localSize);

            Assert.That(compiler.IsWideAccess(0, offset, new ValueSize(accessSize)), Is.EqualTo(wide));
            Assert.That(compiler.IsWideAccess(0, offset, ValueSize.Unknown), Is.True);
        });
    }

    [Test]
    public static void HiddenReturnBuffersDisableEnregistrationWithoutEscapingTheLocal()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(8);
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldLclStart = 1;
            compiler.lvaTable[0].lvFieldCnt = 2;

            for (var i = 1; i < 3; i++)
            {
                compiler.lvaTable[i].Type = TYP_INT;
                compiler.lvaTable[i].lvIsStructField = true;
                compiler.lvaTable[i].lvParentLcl = 0;
            }

            compiler.lvaSetHiddenBufferStructArg(0);

            foreach (var local in compiler.lvaTable)
            {
                Assert.That(local.lvDoNotEnregister, Is.True);
                Assert.That(local.IsAddressExposed, Is.False);
#if DEBUG
                Assert.That(local.IsDefinedViaAddress, Is.True);
#endif
            }
        });
    }

    [Test]
    public static void EarlyReferenceCountsSaturate()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].setLvRefCnt(ushort.MaxValue, RCS_EARLY);
            var visitor = new LocalAddressVisitor(compiler);
            visitor.UpdateEarlyRefCount(0, node: null, user: null);

            Assert.That(compiler.lvaTable[0].lvRefCnt(RCS_EARLY), Is.EqualTo(ushort.MaxValue));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaRefCountState = RCS_EARLY;
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
