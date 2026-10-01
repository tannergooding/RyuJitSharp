// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_WASM
using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenRegisterCallOperandTests
{
    private static int s_assertions;
    private static string? s_lastAssertion;

    [Test]
    public static void CallWithoutAnIndirectionCellReturnsNoRegister()
    {
        WithCompiler((_, codeGen) =>
        {
            var call = new GenTreeCall(TYP_VOID);
            Assert.That(codeGen.getCallIndirectionCellReg(call), Is.EqualTo(REG_NA));
            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

    [Test]
    public static void VirtualStubCellUsesTheCompilerSelectedRegister()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var parameter = new Compiler.VirtualStubParamInfo();
            compiler.virtualStubParamInfo = parameter;
            var call = new GenTreeCall(TYP_VOID) { Flags = GTF_CALL | GTF_CALL_VIRT_STUB };
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(new GenTree(GT_NOP, TYP_I_IMPL))
                .WithWellKnownArg(WellKnownArg.VirtualStubCell));
            argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(parameter.Reg, 0, TARGET_POINTER_SIZE));

            Assert.That(codeGen.getCallIndirectionCellReg(call), Is.EqualTo(parameter.Reg));
            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

#if FEATURE_READYTORUN
    [Test]
    public static void ReadyToRunCellUsesTheNativeTargetRegister()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var call = new GenTreeCall(TYP_VOID);
            call._entryPoint.accessType = InfoAccessType.IAT_PVALUE;
#if TARGET_XARCH
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
#endif
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(new GenTree(GT_NOP, TYP_I_IMPL))
                .WithWellKnownArg(WellKnownArg.R2RIndirectionCell));
            argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(REG_R2R_INDIRECT_PARAM, 0, TARGET_POINTER_SIZE));

            Assert.That(codeGen.getCallIndirectionCellReg(call), Is.EqualTo(REG_R2R_INDIRECT_PARAM));
            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }
#endif

#if TARGET_AMD64 && SWIFT_SUPPORT
    [TestCase(TYP_LONG)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void SwiftErrorRegisterPreservesGcStateAndSkipsTheIdentityMove(var_types type)
    {
        EmitterCallInstructionTests.WithEmitter((_, codeGen) =>
        {
            var tree = new GenTree(GT_SWIFT_ERROR, type) { RegNum = REG_SWIFT_ERROR };
            var mask = regMaskTP.CreateFromRegNum(REG_SWIFT_ERROR, REG_SWIFT_ERROR.SingleTypeMask);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_SWIFT_ERROR, type);

            codeGen.genCodeForSwiftErrorReg(tree);

            Assert.That(CodeGenShiftTests.Descriptors(codeGen), Is.Empty);
            Assert.That((codeGen.GCInfo.gcRegGCrefSetCur & mask).IsNonEmpty, Is.EqualTo(type == TYP_REF));
            Assert.That((codeGen.GCInfo.gcRegByrefSetCur & mask).IsNonEmpty, Is.EqualTo(type == TYP_BYREF));
#if DEBUG
            Assert.That((tree._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED) != 0, Is.True);
#endif
        });
    }
#endif

#if DEBUG && TARGET_ARM
    [TestCase(4, 0)]
    [TestCase(8, 1)]
    public static void TailcallEpilogChecksIncludeTheSecondArmDoubleRegister(int size, int expectedAssertions)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var call = new GenTreeCall(TYP_VOID);
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(new GenTree(GT_NOP, TYP_DOUBLE)));
            var firstHalf = (regNumber)((int)REG_FP_FIRST + 15);
            argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(firstHalf, 0, size));

            codeGen.genCheckTailCallEpilogRegisters(call);

            Assert.That(s_assertions, Is.EqualTo(expectedAssertions), s_lastAssertion);
        });
    }
#endif

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
#if DEBUG
        compiler.info.compFullName = nameof(CodeGenRegisterCallOperandTests);
#endif
        JitTls.Compiler = compiler;
        s_assertions = 0;
        s_lastAssertion = null;
        try
        {
            action(compiler, new CodeGen(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        s_lastAssertion = Marshal.PtrToStringUTF8((nint)expression);
        return 0;
    }
#endif
}
#endif
