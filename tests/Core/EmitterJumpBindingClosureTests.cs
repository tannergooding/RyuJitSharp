// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if (DEBUG && TARGET_AMD64) || !TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
#if DEBUG
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
#endif
#if DEBUG && TARGET_AMD64
using System.Collections.Generic;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterJumpBindingClosureTests
{
#if DEBUG && TARGET_AMD64
    [ThreadStatic]
    private static List<(string? File, string? Expression)>? s_assertionDetails;

    [TestCase(false)]
    [TestCase(true)]
    public static void VerboseBindingPreservesTargetAndDistanceTraceOrdering(bool backward)
    {
        CodeGenSpillVariableTests.WithCompiler(var_types.TYP_LONG, regNumber.REG_RAX,
            (compiler, codeGen, tree) =>
            {
                var emitter = codeGen.Emitter;
                var label = new BasicBlock(null, null);
                label.SetFlags(BasicBlockFlags.BBF_HAS_LABEL);
                var target = emitter.emitCurIG ?? throw new AssertionException("Missing initial group.");
                if (backward)
                {
                    emitter.emitIns_Nop(1);
                    _ = emitter.emitAddInlineLabel();
                }
                emitter.emitIns_J(instruction.INS_jmp, label);
                var end = emitter.emitAddInlineLabel();
                if (!backward)
                {
                    target = end;
                }

                label.bbEmitCookie = target;
                Assert.That(end.igOffs, Is.EqualTo(backward ? 6u : 5u));

                View.Total(emitter) = unchecked((int)end.igOffs);
                compiler.verbose = true;
                compiler.opts.disDiffable = true;

                var text = Capture(emitter.emitJumpDistBind);
                var bind = text.IndexOf("Binding: ", StringComparison.Ordinal);
                var targetBinding = text.IndexOf(
                    $"Binding L_M{unchecked((uint)compiler.compMethodID):D3}_{FMT_BB(label.bbNum)} " +
                    $"to {emitter.emitLabelString(target)}", StringComparison.Ordinal);
                var direction = backward ? "bwd" : "fwd";
                var estimate = text.IndexOf($"Estimate of {direction} jump [", StringComparison.Ordinal);
                var shrink = text.IndexOf("Shrinking jump [", StringComparison.Ordinal);

                Assert.That(bind, Is.GreaterThanOrEqualTo(0));
                Assert.That(targetBinding, Is.GreaterThan(bind));
                Assert.That(estimate, Is.GreaterThan(targetBinding));
                Assert.That(shrink, Is.GreaterThan(estimate));
                Assert.That(text, Does.Contain(
                    (backward ? "0001 -> 0000 = 0003" : "0000 -> 0005 = 0003") + Environment.NewLine));
                Assert.That(text, Does.Contain(
                    $"Adjusted offset of {FMT_BB(checked((int)end.GetDisplayId()))} from {(backward ? 6 : 5):X4} " +
                    $"to {(backward ? 3 : 2):X4}{Environment.NewLine}"));
                Assert.That(View.Total(emitter), Is.EqualTo(backward ? 3 : 2));
            });
    }

    [Test]
    public static void IllegalCrossFuncletBranchReportsBeforeTheNativeAssertion()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var emitter = new View(CreateCompiler());
        var source = new insGroup { igFuncIdx = 0 };
        var target = new insGroup { igFuncIdx = 1 };
        var jump = View.BoundJump(target);

        var text = Capture(() => View.Check(emitter, jump, source));

        Assert.That(text, Is.EqualTo("Hit an illegal branch between funclets!"));
        Assert.That(context.Assertions, Is.EqualTo(1));
        Assert.That(View.Target(jump), Is.SameAs(target));
    }

    [Test]
    public static void MisplacedRemovalCandidatePrintsBothInstructionsAndGroups()
    {
        CodeGenSpillVariableTests.WithCompiler(var_types.TYP_LONG, regNumber.REG_RAX,
            (compiler, codeGen, _) =>
            {
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.doAssert = &RecordAssertion;
                var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
                using var tls = new JitTls(&context.JitInfo);
                JitTls.Compiler = compiler;
                compiler.info.compMethodName = "Mismatch";
                var emitter = codeGen.Emitter;
                var label = new BasicBlock(null, null);
                label.SetFlags(BasicBlockFlags.BBF_HAS_LABEL);
                var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
                emitter.emitIns_J(instruction.INS_jmp, label, isRemovableJmpCandidate: true);
                emitter.emitIns_Nop(1);
                var target = emitter.emitAddInlineLabel();
                label.bbEmitCookie = target;
                source.igFlags |= InsGroupFlags.HasRemovableJump;
                View.Total(emitter) = unchecked((int)target.igOffs);

                Assert.That(context.Assertions, Is.Zero);
                Assert.That(source.igInsCnt, Is.EqualTo(2));
                Assert.That(source.igSize, Is.EqualTo(6));
                var instructions = source.igData ?? throw new AssertionException("Missing saved source descriptors.");
                Assert.That(instructions[0].idCodeSize(), Is.EqualTo(5u));
                Assert.That(instructions[1].idIns(), Is.EqualTo(instruction.INS_nop));

                List<(string? File, string? Expression)> assertions = [];
                var previousAssertions = s_assertionDetails;
                string text;
                try
                {
                    s_assertionDetails = assertions;
                    text = Capture(emitter.emitRemoveJumpToNextInst);
                }
                finally
                {
                    s_assertionDetails = previousAssertions;
                }

                Assert.That(text, Does.Contain("jmp != id, dumping context information" + Environment.NewLine));
                Assert.That(text, Does.Contain("method: Mismatch" + Environment.NewLine));
                Assert.That(text, Does.Contain("  jmp: 1: "));
                Assert.That(text, Does.Contain("   id: 2: "));
                Assert.That(text, Does.Contain("jump group:" + Environment.NewLine));
                Assert.That(text, Does.Contain("target group:" + Environment.NewLine));
                Assert.That(assertions, Has.Count.EqualTo(3));
                // The context dump checks the still-sized candidate, then its nonterminal position,
                // before the removal routine reports the original last-descriptor mismatch.
                Assert.That(assertions[0].File, Does.EndWith("Emitter.InstructionOutputSupport.cs"));
                Assert.That(assertions[0].Expression, Does.StartWith("!result || id.idCodeSize() == 0"));
                Assert.That(assertions[0].Expression, Does.Contain(
                    "|| (((instrDescJmp)id).idjIsAfterCallBeforeEpilog && id.idCodeSize() == 1)"));
                Assert.That(assertions[1].File, Does.EndWith("Emitter.GroupDiagnostics.cs"));
                Assert.That(assertions[1].Expression, Is.EqualTo("count == 1"));
                Assert.That(assertions[2].File, Does.EndWith("Emitter.JumpRemoval.cs"));
                Assert.That(assertions[2].Expression, Is.EqualTo("ReferenceEquals(jmp, lastInstruction)"));
                Assert.That(context.Assertions, Is.EqualTo(assertions.Count));
                Assert.That(source.igSize, Is.EqualTo(1));
                Assert.That(target.igOffs, Is.EqualTo(1u));
            });
    }

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        ((AssertionContext*)self)->Assertions++;
        s_assertionDetails?.Add((Marshal.PtrToStringUTF8((nint)file), Marshal.PtrToStringUTF8((nint)expression)));

        return 0;
    }
#endif

#if !TARGET_XARCH
    [Test]
    public static void JumpRemovalIsTheNativeEmptyOperationOutsideXarch()
    {
        var emitter = new View(CreateCompiler());
        View.Total(emitter) = 123;

        emitter.emitRemoveJumpToNextInst();

        Assert.That(View.Total(emitter), Is.EqualTo(123));
    }
#endif

#if TARGET_ARM || TARGET_ARM64
    [Test]
    public static void DeferredConditionalClassifierTerminatesRatherThanChoosingAnEncoding()
    {
        var exception = Assert.Throws<FatalJitException>(() => View.Classify(View.Descriptor()));
        Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
    }
#endif

    private static Compiler CreateCompiler()
    {
        return (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
    }

    private sealed class View : Emitter
    {
        public View(Compiler compiler) : base(new CodeGen(compiler))
        {
            _compiler = compiler;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTotalCodeSize")]
        public static extern ref int Total(Emitter emitter);

#if DEBUG && TARGET_AMD64
        public static instrDesc BoundJump(insGroup target)
        {
            var jump = new instrDescJmp { idjTargetIG = target };
            jump.idIns(instruction.INS_jmp);
            jump.idInsFmt(insFormat.IF_LABEL);
            jump.idSetIsBound();
            jump.idDebugOnlyInfo(new instrDescDebugInfo());
            return jump;
        }

        public static insGroup? Target(instrDesc jump) => ((instrDescJmp)jump).idjTargetIG;

        public static void Check(Emitter emitter, instrDesc jump, insGroup source)
        {
            CheckBranch(emitter, (instrDescJmp)jump, source);
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitCheckFuncletBranch")]
        private static extern void CheckBranch(Emitter emitter, instrDescJmp jump, insGroup source);
#endif

#if TARGET_ARM || TARGET_ARM64
        public static instrDesc Descriptor()
        {
            var id = new instrDescBasic();
            id.idIns(instruction.INS_nop);
            id.idInsFmt(insFormat.IF_NONE);
            return id;
        }

        public static bool Classify(instrDesc jump) => ClassifyConditional(null, jump);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitIsCondJump")]
        private static extern bool ClassifyConditional(Emitter? emitter, instrDesc jump);
#endif
    }

#if DEBUG
    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif
}
#endif
