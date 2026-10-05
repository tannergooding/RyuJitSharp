// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmRegAllocTests
{
    [Test]
    public static void NonFixedRegisterStateAndIterationPreserveFuncletAndNodeAssociations()
    {
        WithCompiler(compiler =>
        {
            var codeGen = compiler.codeGen
                ?? throw new AssertionException("The compiler code generator was not initialized.");
            var stackPointer = regNumberExtensions.MakeWasmReg(0, WasmValueType.I32);
            var framePointer = regNumberExtensions.MakeWasmReg(1, WasmValueType.I32);
            var secondRegister = regNumberExtensions.MakeWasmReg(2, WasmValueType.I32);

            codeGen.SetStackPointerReg(0, stackPointer);
            codeGen.SetFramePointerReg(0, framePointer);
            Assert.That(compiler.compFuncInfos[0].funStackPointerReg, Is.EqualTo(stackPointer));
            Assert.That(compiler.compFuncInfos[0].funFramePointerReg, Is.EqualTo(framePointer));

            var firstTree = new GenTreeIntCon(TYP_INT, 1);
            var secondTree = new GenTreeIntCon(TYP_INT, 2);
            codeGen.InternalRegisters.Add(firstTree, stackPointer);
            codeGen.InternalRegisters.Add(firstTree, framePointer);
            codeGen.InternalRegisters.Add(secondTree, secondRegister);

            var foundFirst = false;
            var foundSecond = false;
            var count = 0;
            var iterator = codeGen.InternalRegisters.Iterate();
            while (iterator.MoveNext())
            {
                var entry = iterator.Current;
                if (ReferenceEquals(entry.Key, firstTree))
                {
                    foundFirst = true;
                    Assert.That(entry.Value.Count, Is.EqualTo(2));
                    Assert.That(entry.Value.GetAt(0), Is.EqualTo(stackPointer));
                    Assert.That(entry.Value.GetAt(1), Is.EqualTo(framePointer));
                }
                else if (ReferenceEquals(entry.Key, secondTree))
                {
                    foundSecond = true;
                    Assert.That(entry.Value.Count, Is.EqualTo(1));
                    Assert.That(entry.Value.GetAt(0), Is.EqualTo(secondRegister));
                }

                count++;
            }

            Assert.That(count, Is.EqualTo(2));
            Assert.That(foundFirst, Is.True);
            Assert.That(foundSecond, Is.True);
        });
    }

    [Test]
    public static void ResolveReferencesSkipsUnusedTemporaryRegisterBanks()
    {
        WithCompiler(compiler => {
            var allocator = new WasmRegAlloc(compiler);

            Assert.That(allocator.DoRegisterAllocation(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compRegAllocDone, Is.True);

            var locals = compiler.compFuncInfos[0].funWasmLocalDecls
                ?? throw new AssertionException("Wasm register allocation did not publish local declarations.");
            Assert.That(locals, Has.Count.EqualTo(1));
            Assert.That(locals[0].Type, Is.EqualTo(WasmValueType.I32));
            Assert.That(locals[0].Count, Is.EqualTo(1));
        });
    }

    [Test]
    public static void PhysicalRegisterSourceCanBeRetargetedAfterLirReplacement()
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var source = new GenTreeIntCon(TYP_I_IMPL, 0);
            var following = new GenTreeIntCon(TYP_I_IMPL, 1);
            block.InsertAtEnd(source);
            block.InsertAtEnd(following);

            var virtualStackPointer = regNumberExtensions.MakeWasmReg(0, WasmValueType.I32);
            var resolvedStackPointer = regNumberExtensions.MakeWasmReg(2, WasmValueType.I32);
            var physicalRegister = new GenTreePhysReg(
                virtualStackPointer, TYP_I_IMPL, source, NodeThreading.LIR);
            block.ReplaceNode(source, physicalRegister);
            physicalRegister.SetSrcReg(resolvedStackPointer);

            Assert.That(physicalRegister.SrcReg, Is.EqualTo(resolvedStackPointer));
            Assert.That(block.FirstNode, Is.SameAs(physicalRegister));
            Assert.That(block.LastNode, Is.SameAs(following));
            Assert.That(physicalRegister.Prev, Is.Null);
            Assert.That(physicalRegister.Next, Is.SameAs(following));
            Assert.That(following.Prev, Is.SameAs(physicalRegister));
            Assert.That(source.Prev, Is.Null);
            Assert.That(source.Next, Is.Null);
        });
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
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.opts.SetMinOpts(true);
            compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL }];
            compiler.lvaCount = 1;
            compiler.lvaWasmSpArg = 0;
            compiler.lvaWasmResumeIP = BAD_VAR_NUM;
            compiler.lvaTable[0].setLvRefCnt(1);
            compiler.fgFuncletsCreated = true;
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;

            compiler.codeGen = new CodeGen(compiler);
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
