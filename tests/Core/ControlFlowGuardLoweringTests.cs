// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ControlFlowGuardLoweringTests
{
    [Test]
    public static void DirectCallWithNoTargetDoesNotNeedCFG()
    {
        WithCompiler((compiler, block, lowering) => {
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_USER_FUNC,
                _directCallAddress = (void*)0x1234,
            };
            block.InsertAtEnd(call);

            LowerCFGCall(lowering, call);

            Assert.That(call._callType, Is.EqualTo(CT_USER_FUNC));
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x1234));
            Assert.That(call._controlExpr, Is.Null);
            Assert.That(block.FirstNode, Is.SameAs(call));
        });
    }

    [Test]
    public static void ConstantIndirectTargetDoesNotNeedCFG()
    {
        WithCompiler((compiler, block, lowering) => {
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_INDIRECT,
                _controlExpr = target,
            };
            block.InsertAtEnd(target);
            block.InsertAtEnd(call);

            LowerCFGCall(lowering, call);

            Assert.That(call._callType, Is.EqualTo(CT_INDIRECT));
            Assert.That(call._controlExpr, Is.SameAs(target));
            Assert.That(target.Next, Is.SameAs(call));
        });
    }

    [Test]
    public static void ValidatorHelperDoesNotValidateItself()
    {
        WithCompiler((compiler, block, lowering) => {
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_HELPER,
                _callMethHnd = Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_VALIDATE_INDIRECT_CALL),
            };
            block.InsertAtEnd(call);

            LowerCFGCall(lowering, call);

            Assert.That(call._callType, Is.EqualTo(CT_HELPER));
            Assert.That(call._controlExpr, Is.Null);
            Assert.That(block.FirstNode, Is.SameAs(call));
        });
    }

    [Test]
    public static void VirtualStubCellsRequireValidationRatherThanDispatch()
    {
        WithCompiler((compiler, block, lowering) => {
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_USER_FUNC,
                Flags = GTF_CALL_VIRT_STUB,
            };
            Assert.That(call.IndirectionCellArgKind, Is.EqualTo(WellKnownArg.VirtualStubCell));
            Assert.That(GetCFGCallKind(lowering, call), Is.EqualTo("ValidateAndCall"));
        });
    }

    [Test]
    public static void DispatcherPolicyHonorsDebugOverrideForOrdinaryCalls()
    {
        WithCompiler((compiler, block, lowering) => {
            var call = new GenTreeCall(TYP_VOID) { _callType = CT_INDIRECT };
#if DEBUG
            var expected = Globals.JitConfig.JitCFGUseDispatcher == 0 ? "ValidateAndCall" : "Dispatch";
#else
            const string expected = "Dispatch";
#endif
            Assert.That(call.IndirectionCellArgKind, Is.EqualTo(WellKnownArg.None));
            Assert.That(GetCFGCallKind(lowering, call), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void DispatchMovesTargetIntoItsRegisterArgumentAndRewritesTheCall()
    {
        WithDispatchPolicy(() => WithCompiler((compiler, block, lowering) => {
            var target = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_INDIRECT,
                _controlExpr = target,
            };
#if FEATURE_READYTORUN
            call._entryPoint.addr = (void*)0x1234;
            call._entryPoint.accessType = InfoAccessType.IAT_PVALUE;
#endif
            block.InsertAtEnd(target);
            block.InsertAtEnd(call);

            LowerCFGCall(lowering, call);

            var targetArg = call.Args.FindWellKnownArg(WellKnownArg.DispatchIndirectCallTarget);
            Assert.That(targetArg, Is.Not.Null);
            Assert.That(targetArg?.EarlyNode, Is.Null);
            Assert.That(targetArg?.LateNode?.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
            Assert.That(targetArg?.LateNode?.AsUnOp().Op1, Is.SameAs(target));
            Assert.That(targetArg?.LateNode?.RegNum, Is.EqualTo(Globals.REG_DISPATCH_INDIRECT_CALL_ADDR));
            Assert.That(call.Args.LateHead, Is.SameAs(targetArg));
            Assert.That(call._callType, Is.EqualTo(CT_HELPER));
            Assert.That((nint)call._callMethHnd, Is.EqualTo((nint)Compiler.eeFindHelper(
                CorInfoHelpFunc.CORINFO_HELP_DISPATCH_INDIRECT_CALL)));
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
            Assert.That(call._controlExpr, Is.Null);
#if FEATURE_READYTORUN
            Assert.That((nint)call._entryPoint.addr, Is.EqualTo((nint)0));
            Assert.That(call._entryPoint.accessType, Is.EqualTo(InfoAccessType.IAT_VALUE));
#endif
        }));
    }

    [Test]
    public static void LateTargetArgumentAppendsAfterExistingLateArguments()
    {
        WithCompiler((compiler, block, lowering) => {
            var first = new GenTreeIntCon(TYP_I_IMPL, 1);
            var second = new GenTreeIntCon(TYP_I_IMPL, 2);
            var third = new GenTreeIntCon(TYP_I_IMPL, 3);
            CallArgs args = default;
            var firstArg = args.PushBack(NewCallArg.CreateForPrimitive(first));
            var secondArg = args.PushBack(NewCallArg.CreateForPrimitive(second));
            var lastArg = args.PushBack(NewCallArg.CreateForPrimitive(third)
                .WithWellKnownArg(WellKnownArg.DispatchIndirectCallTarget));
            args.PushLateBack(firstArg);
            args.PushLateBack(secondArg);
            args.PushLateBack(lastArg);

            Assert.That(args.LateHead, Is.SameAs(firstArg));
            Assert.That(firstArg.LateNext, Is.SameAs(secondArg));
            Assert.That(secondArg.LateNext, Is.SameAs(lastArg));
            Assert.That(lastArg.LateNext, Is.Null);
            Assert.That(args.FindWellKnownArg(WellKnownArg.DispatchIndirectCallTarget), Is.SameAs(lastArg));
        });
    }

    private static void LowerCFGCall(Lowering lowering, GenTreeCall call)
    {
        var method = typeof(Lowering).GetMethod("LowerCFGCall", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        _ = method.Invoke(lowering, [call]);
    }

    private static string GetCFGCallKind(Lowering lowering, GenTreeCall call)
    {
        var method = typeof(Lowering).GetMethod("GetCFGCallKind", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        return method.Invoke(lowering, [call])?.ToString() ?? throw new InvalidOperationException();
    }

    private static void WithDispatchPolicy(Action action)
    {
#if DEBUG
        var previousPolicy = CFGDispatchPolicy(ref Globals.JitConfig);
        CFGDispatchPolicy(ref Globals.JitConfig) = 1;
        try
        {
            action();
        }
        finally
        {
            CFGDispatchPolicy(ref Globals.JitConfig) = previousPolicy;
        }
#else
        action();
#endif
    }

    private static void WithCompiler(Action<Compiler, BasicBlock, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.getHelperFtn = &GetHelperFtn;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &ee;
        compiler.info.compMatchedVM = true;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, block, lowering);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitCFGUseDispatcher")]
    private static extern ref int CFGDispatchPolicy(ref JitConfigValues config);
#endif

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x5678;
        return lookup->addr;
    }
}
