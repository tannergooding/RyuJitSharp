// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
#if TARGET_XARCH || TARGET_ARM64
using static RyuJitSharp.instruction;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterIssueBookkeepingClosureTests
{
    [TestCase(true, 0x2000u, 0u)]
    [TestCase(false, 0x2000u, uint.MaxValue)]
    [TestCase(false, 0u, 23u)]
    [TestCase(false, 5u, 41u)]
    public static void CallSitesQuerySignaturesLazilyAndPreserveTheEeOrder(
        bool explicitSignature, uint methodValue, uint offset)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getMethodSig = &GetSignature;
        vtable.recordCallSite = &RecordCallSite;
        var context = new CallbackContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &context.JitInfo;
        var emitter = new View(compiler) { emitCmpHandle = &context.JitInfo };
        CORINFO_SIG_INFO signature = default;
        signature.retType = CorInfoType.CORINFO_TYPE_INT;
        signature.numArgs = 23;

        View.RecordSite(emitter, offset, explicitSignature ? &signature : null,
            (CORINFO_METHOD_STRUCT_*)(nuint)methodValue);

#if DEBUG
        var query = !explicitSignature && methodValue == 0x2000;
        Assert.That(context.Queries, Is.EqualTo(query ? 1 : 0));
        Assert.That(context.CallSites, Is.EqualTo(1));
        Assert.That(context.QueryOrder, Is.EqualTo(query ? 1 : 0));
        Assert.That(context.CallOrder, Is.EqualTo(query ? 2 : 1));
        Assert.That(context.Offset, Is.EqualTo(unchecked((int)offset)));
        Assert.That(context.Method, Is.EqualTo((nuint)methodValue));
        Assert.That(context.HadSignature, Is.EqualTo(explicitSignature || query));
        Assert.That(context.Arguments, Is.EqualTo(explicitSignature ? 23 : query ? 17 : 0));
#else
        Assert.That(context.Queries, Is.Zero);
        Assert.That(context.CallSites, Is.Zero);
#endif
    }

#if TARGET_XARCH || TARGET_ARM64
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(-1)]
    public static void FindingInstructionNumbersPreservesTheFirstAndOnePastEndPositions(int index)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new View(compiler);
        var group = View.Group(index < 0 ? 0 : 3);
        var storage = group.igData ?? throw new AssertionException("Missing descriptor storage.");
        var match = index is >= 0 and < 3 ? storage[index] : null;

        Assert.That(View.Find(emitter, group, match), Is.EqualTo((uint)(index < 0 ? 0 : index)));
    }

    [Test]
    public static void MissingInstructionRetainsTheNativeAssertionAndUnsignedSentinel()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new CallbackContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
#if DEBUG
        using var tls = new JitTls(&context.JitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        var emitter = new View(compiler);
        var group = View.Group(2);
        try
        {
            JitTls.Compiler = compiler;
            Assert.That(View.Find(emitter, group, View.Basic()), Is.EqualTo(uint.MaxValue));
#if DEBUG
            Assert.That(context.Assertions, Is.EqualTo(1));
            Assert.That(context.FindFailureAssertions, Is.EqualTo(1));
#else
            Assert.That(context.Assertions, Is.Zero);
#endif
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }
#endif

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    [TestCase(0, 1)]
    [TestCase(int.MaxValue, int.MinValue)]
    [TestCase(-1, 0)]
    public static void IssueShrinkageWrapsTheNativeSignedAdjustment(int initial, int expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitIns(INS_ret);
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing current instruction group.");
            var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing return instruction.");
            descriptor.idCodeSize(2);
#if DEBUG || LATE_DISASM
            group.igWeight = 16_777_217;
#endif
            var buffer = stackalloc byte[16];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 16;
            emitter.writeableOffset = 0;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            OffsetAdjustment(emitter) = initial;
#if DEBUG || LATE_DISASM
            var cost = View.ExecutionCost(emitter, descriptor);
            var score = group.igWeight / (double)BB_UNITY_WEIGHT * cost;
            var initialScore = compiler.Metrics.PerfScore;
            var initialGroupScore = group.igPerfScore;
#endif
            var end = buffer;
            var size = View.Issue(emitter, group, descriptor, &end);

            Assert.That(size, Is.EqualTo((nuint)descriptor.NativeLogicalSize));
            Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 1)));
            Assert.That(buffer[0], Is.EqualTo(0xC3));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(1u));
            Assert.That(OffsetAdjustment(emitter), Is.EqualTo(expected));
            Assert.That(group.igFlags & InsGroupFlags.UpdatedInstructionSize,
                Is.EqualTo(InsGroupFlags.UpdatedInstructionSize));
#if DEBUG || LATE_DISASM
            Assert.That(compiler.Metrics.PerfScore, Is.EqualTo(initialScore + score));
            Assert.That(group.igPerfScore, Is.EqualTo(initialGroupScore + score));
#endif
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitOffsAdj")]
    private static extern ref int OffsetAdjustment(Emitter emitter);
#endif

    private struct CallbackContext
    {
        public ICorJitInfo JitInfo;
        public int Sequence;
        public int Queries;
        public int CallSites;
        public int QueryOrder;
        public int CallOrder;
        public int Offset;
        public nuint Method;
        public bool HadSignature;
        public ushort Arguments;
        public int Assertions;
        public int FindFailureAssertions;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetSignature(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method,
        CORINFO_SIG_INFO* signature, CORINFO_CLASS_STRUCT_* owner)
    {
        var context = (CallbackContext*)self;
        context->Queries++;
        context->QueryOrder = ++context->Sequence;
        *signature = default;
        signature->retType = CorInfoType.CORINFO_TYPE_INT;
        signature->numArgs = 17;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordCallSite(ICorJitInfo* self, int offset,
        CORINFO_SIG_INFO* signature, CORINFO_METHOD_STRUCT_* method)
    {
        var context = (CallbackContext*)self;
        context->CallSites++;
        context->CallOrder = ++context->Sequence;
        context->Offset = offset;
        context->Method = (nuint)method;
        context->HadSignature = signature != null;
        context->Arguments = signature != null ? signature->numArgs : (ushort)0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (CallbackContext*)self;
        context->Assertions++;
        if (Marshal.PtrToStringUTF8((nint)expression) == "!\"emitFindInsNum failed\"")
        {
            context->FindFailureAssertions++;
        }

        return 0;
    }

    private sealed class View : Emitter
    {
        public View(Compiler compiler) : base(new CodeGen(compiler))
        {
            _compiler = compiler;
        }

#if TARGET_XARCH || TARGET_ARM64
        public static insGroup Group(int count)
        {
            var descriptors = new instrDesc[count];
            var group = new insGroup { igData = descriptors, igInsCnt = (byte)count };
            nuint offset = 0;
            for (var index = 0; index < count; index++)
            {
                var descriptor = Basic();
                descriptor.StorageGroup = group;
                descriptor.StorageIndex = index;
                descriptor.StorageOffset = offset;
                descriptor.StorageSize = (nuint)descriptor.NativeLogicalSize;
                offset += descriptor.StorageSize;
                descriptors[index] = descriptor;
            }

            return group;
        }

        public static instrDesc Basic()
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(INS_nop);
#if TARGET_XARCH
            descriptor.idInsFmt(insFormat.IF_NONE);
#else
            descriptor.idInsFmt(insFormat.IF_SN_0A);
#endif

            return descriptor;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitFindInsNum")]
        public static extern uint Find(Emitter emitter, insGroup group, instrDesc? match);
#endif

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitRecordCallSite")]
        public static extern void RecordSite(Emitter emitter, uint offset,
            CORINFO_SIG_INFO* signature, CORINFO_METHOD_STRUCT_* method);

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIssue1Instr")]
        public static extern nuint Issue(Emitter emitter, insGroup group, instrDesc descriptor, byte** code);

#if DEBUG || LATE_DISASM
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insEvaluateExecutionCost")]
        public static extern float ExecutionCost(Emitter emitter, instrDesc descriptor);
#endif
#endif
    }
}
