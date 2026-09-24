// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CallLoweringTests
{
    [TestCase(InfoAccessType.IAT_VALUE)]
    [TestCase(InfoAccessType.IAT_PVALUE)]
    [TestCase(InfoAccessType.IAT_RELPVALUE)]
    public static void DirectCallTargetsPreserveLookupShape(InfoAccessType accessType)
    {
        WithCompiler(compiler => {
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            var call = new GenTreeCall(var_types.TYP_VOID) {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x5678,
                _entryPoint = new CORINFO_CONST_LOOKUP { accessType = accessType, addr = (void*)0x1234 },
            };
            var result = InvokeLowerDirectCall(lowering, call);
            if (accessType is InfoAccessType.IAT_VALUE)
            {
                Assert.That(result, Is.Null);
                Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x1234));
                return;
            }

            var target = result ?? throw new InvalidOperationException();
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0));
            if (accessType is InfoAccessType.IAT_PVALUE)
            {
                Assert.That(target.Oper, Is.EqualTo(genTreeOps.GT_IND));
                Assert.That(target.Flags & GenTreeFlags.GTF_IND_NONFAULTING, Is.Not.EqualTo(GenTreeFlags.GTF_EMPTY));
                AssertAddress(target.AsIndir().Addr);
#if DEBUG
                Assert.That(target.AsIndir().Addr.AsIntCon().TargetHandle, Is.EqualTo((nint)0x5678));
#endif
            }
            else
            {
                Assert.That(target.Oper, Is.EqualTo(genTreeOps.GT_ADD));
                var add = target.AsOp();
                Assert.That(add.Op1.Oper, Is.EqualTo(genTreeOps.GT_IND));
                AssertAddress(add.Op1.AsIndir().Addr);
                AssertAddress(add.Op2);
                Assert.That(add.Op1.AsIndir().Addr, Is.Not.SameAs(add.Op2));
            }
        });
    }

    [Test]
    public static void FastTailCallReusesItsReadyToRunIndirectionCell()
    {
        WithCompiler(compiler => {
            var call = new GenTreeCall(var_types.TYP_VOID) {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMoreFlags = GenTreeCallFlags.GTF_CALL_M_TAILCALL,
                _entryPoint = new CORINFO_CONST_LOOKUP {
                    accessType = InfoAccessType.IAT_PVALUE,
                    addr = (void*)0x1234,
                },
            };

            Assert.That(call.IndirectionCellArgKind, Is.EqualTo(WellKnownArg.R2RIndirectionCell));
            Assert.That(InvokeLowerDirectCall(new Lowering(compiler, new LinearScan(compiler)), call), Is.Null);
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ArgumentPlacementPreservesOrderAcrossInterferingCalls(bool interference)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            var blockField = typeof(Lowering).GetField("_block", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            blockField.SetValue(lowering, block);

            var outer = new GenTreeCall(var_types.TYP_VOID);
            var nested = new GenTreeCall(var_types.TYP_VOID);
            var firstValue = new GenTreeIntCon(var_types.TYP_INT, 1);
            var secondValue = new GenTreeIntCon(var_types.TYP_INT, 2);
            var firstArg = new GenTreeUnOp(genTreeOps.GT_PUTARG_REG, var_types.TYP_INT, firstValue);
            var secondArg = new GenTreePutArgStk(var_types.TYP_VOID, secondValue, outer, 32, 8, false);
            outer.Args.PushBack(NewCallArg.CreateForPrimitive(firstValue)).EarlyNode = firstArg;
            var lateArg = outer.Args.PushBack(NewCallArg.CreateForPrimitive(secondValue));
            lateArg.LateNode = secondArg;
            lateArg.EarlyNode = null;

            GenTree[] nodes = interference
                ? [firstValue, firstArg, secondValue, secondArg, nested, outer]
                : [firstValue, secondValue, nested, firstArg, secondArg, outer];
            foreach (var node in nodes)
            {
                block.InsertAtEnd(node);
            }

            var legalize = typeof(Lowering).GetMethod("LegalizeArgPlacement", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            _ = legalize.Invoke(lowering, [outer]);

            GenTree[] expected = [firstValue, secondValue, nested, firstArg, secondArg, outer];
            var current = block.FirstNode;
            GenTree? previous = null;
            foreach (var node in expected)
            {
                Assert.That(current, Is.SameAs(node));
                Assert.That(node.Prev, Is.SameAs(previous));
                Assert.That(node._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
                previous = node;
                current = node.Next;
            }
            Assert.That(current, Is.Null);
            Assert.That(block.LastNode, Is.SameAs(outer));
        });
    }

    [TestCase(0, 0, false)]
    [TestCase(32, 0, false)]
    [TestCase(56, 32, false)]
    [TestCase(64, 32, true)]
    public static void OutgoingAreaRequiresFramePointerAtFourExtraSlots(int firstSize, int secondSize, bool required)
    {
        WithCompiler(compiler => {
            var codeGen = new CodeGen(compiler) { IsFramePointerRequired = false };
            compiler.codeGen = codeGen;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            var call = new GenTreeCall(var_types.TYP_VOID);
            var requireSpace = typeof(Lowering).GetMethod("RequireOutgoingArgSpace", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            _ = requireSpace.Invoke(lowering, [call, firstSize]);
            _ = requireSpace.Invoke(lowering, [call, secondSize]);

            var setFramePointer = typeof(Lowering).GetMethod("SetFramePointerFromArgSpaceSize", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            _ = setFramePointer.Invoke(lowering, null);
            Assert.That(codeGen.IsFramePointerRequired, Is.EqualTo(required));
        });
    }

    private static GenTree? InvokeLowerDirectCall(Lowering lowering, GenTreeCall call)
    {
        var method = typeof(Lowering).GetMethod("LowerDirectCall", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        return (GenTree?)method.Invoke(lowering, [call]);
    }

    private static void AssertAddress(GenTree node)
    {
        Assert.That(node.Oper, Is.EqualTo(genTreeOps.GT_CNS_INT));
        Assert.That(node.AsIntCon().IconValue, Is.EqualTo((nint)0x1234));
        Assert.That(node.Flags & GenTreeFlags.GTF_ICON_HDL_MASK, Is.EqualTo(Globals.GTF_ICON_FTN_ADDR));
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
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
