// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64CallArgumentLoweringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void SplitFieldListKeepsStackBeforeRegistersAndPreservesArgumentOwners(bool late)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.fgNodeThreading = NodeThreading.LIR;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            var first = compiler.gtNewIconNode(TYP_LONG, 1);
            var second = compiler.gtNewIconNode(TYP_LONG, 2);
            var originalFields = new GenTreeFieldList();
            originalFields.AddFieldLIR(compiler, first, 0, TYP_LONG);
            originalFields.AddFieldLIR(compiler, second, 8, TYP_LONG);
            var call = new GenTreeCall(TYP_VOID);
            var argument = call.Args.PushBack(NewCallArg.CreateForStruct(originalFields, TYP_STRUCT, new ClassLayout(16)));
            argument.AbiInfo = new AbiPassingInformation(2);
            argument.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(REG_R0, 0, 8);
            argument.AbiInfo.Segments[1] = AbiPassingSegment.OnStack(32, 8, 8);
            if (late)
            {
                argument.EarlyNode = null;
                argument.LateNode = originalFields;
                LateHead(ref call.Args) = argument;
            }
            block.InsertAtEnd(first);
            block.InsertAtEnd(second);
            block.InsertAtEnd(originalFields);
            block.InsertAtEnd(call);

            SplitArgumentBetweenRegistersAndStack(lowering, call, argument);

            var stackFields = argument.Node.AsFieldList();
            var registerArgument = call.Args.GetArgByIndex(1)
                ?? throw new AssertionException("The split register argument was not inserted.");
            var registerFields = registerArgument.Node.AsFieldList();
            Assert.That(argument.AbiInfo.HasExactlyOneStackSegment, Is.True);
            Assert.That(argument.AbiInfo.Segments[0].StackOffset, Is.EqualTo(32));
            Assert.That(argument.AbiInfo.Segments[0].Size, Is.EqualTo(8));
            Assert.That(registerArgument.AbiInfo.HasExactlyOneRegisterSegment, Is.True);
            Assert.That(registerArgument.AbiInfo.Segments[0].Register, Is.EqualTo(REG_R0));
            Assert.That(registerArgument.AbiInfo.Segments[0].Offset, Is.Zero);
            Assert.That(stackFields.Uses.Head?.Node, Is.SameAs(second));
            Assert.That(stackFields.Uses.Head?.Offset, Is.EqualTo((ushort)0));
            Assert.That(registerFields.Uses.Head?.Node, Is.SameAs(first));
            Assert.That(registerFields.Uses.Head?.Offset, Is.EqualTo((ushort)0));
            Assert.That(first.Next, Is.SameAs(second));
            Assert.That(second.Next, Is.SameAs(stackFields));
            Assert.That(stackFields.Next, Is.SameAs(registerFields));
            Assert.That(registerFields.Next, Is.SameAs(call));
            Assert.That(originalFields.Next, Is.Null);
            Assert.That(originalFields.Prev, Is.Null);
            Assert.That(late ? argument.LateNode : argument.EarlyNode, Is.SameAs(stackFields));
            Assert.That(late ? registerArgument.LateNode : registerArgument.EarlyNode, Is.SameAs(registerFields));
            if (late)
            {
                Assert.That(argument.LateNext, Is.SameAs(registerArgument));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lateHead")]
    private static extern ref CallArg LateHead(ref CallArgs args);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SplitArgumentBetweenRegistersAndStack")]
    private static extern void SplitArgumentBetweenRegistersAndStack(Lowering lowering, GenTreeCall call, CallArg argument);
}
#endif
