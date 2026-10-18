using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static partial class TestConfiguration
{
    public static string CaptureDebugOutput(Action action)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Debug output capture is only available in Debug builds.");
        }

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

    public static unsafe (string Output, string[] Assertions) CaptureDebugAssertions(Action action)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Debug assertion capture is only available in Debug builds.");
        }

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordDebugAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        var previousCompiler = JitTls.Compiler;
        using var tls = new JitTls(&ee);
        JitTls.Compiler = previousCompiler;
        var previousConfig = JitConfig;
        var previousOutput = s_jitstdout;
        var previousAssertions = s_debugAssertions.ToArray();
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        JitConfig = new JitConfigValues();
        s_jitstdout = writer;
        s_debugAssertions.Clear();
        try
        {
            action();
            writer.Flush();

            return (Encoding.UTF8.GetString(stream.ToArray()), s_debugAssertions.ToArray());
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
            s_jitstdout = previousOutput;
            s_debugAssertions.Clear();
            s_debugAssertions.AddRange(previousAssertions);
        }
    }

    private static readonly List<string> s_debugAssertions = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe int RecordDebugAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_debugAssertions.Add(Marshal.PtrToStringUTF8((nint)expression)
            ?? throw new AssertionException("Missing native assertion text."));

        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compActiveStressModes")]
    private static extern ref InlineArrayCompilerStressCount<byte> ActiveStressModes(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockStats")]
    private static extern ref uint[]?[]? BlockStats(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compCodeGenDone")]
    private static extern ref bool CodeGenDone(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_NodeTestData")]
    private static extern Dictionary<GenTree, TestLabelAndNum> NodeTestData(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "JitTestCheckSSA")]
    private static extern void SsaTestCheck(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "JitTestCheckVN")]
    private static extern void ValueNumberTestCheck(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lvaStressLclFld")]
    private static extern void LocalFieldStress(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgStress64RsltMul")]
    private static extern void WideMultiplyStress(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_lvNoLclFldStress")]
    private static extern bool NoLocalFieldStress(ref LclVarDsc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispJumpList")]
    private static extern void EmitJumpList(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ShouldDumpAsciiTrees")]
    private static extern bool GetShouldDumpAsciiTrees(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgDebugCheckSsa")]
    private static extern void FgDebugCheckSsa(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Dump")]
    private static extern void UnwindInfoDump(UnwindInfo unwindInfo, bool isHotCode, int indent);

    private unsafe delegate void GlobalsUnwindInfoDumpDelegate(
        Compiler compiler, bool isHotCode, uint startOffset, uint endOffset, byte* header, uint unwindBlockSize);

    private static readonly Lazy<GlobalsUnwindInfoDumpDelegate> s_globalsUnwindInfoDump =
        new(CreateGlobalsUnwindInfoDump);

    private static GlobalsUnwindInfoDumpDelegate CreateGlobalsUnwindInfoDump()
    {
        var method = typeof(Globals).GetMethod(
            "DumpUnwindInfo",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing Debug unwind-info diagnostic.");

        return method.CreateDelegate<GlobalsUnwindInfoDumpDelegate>();
    }

    public static int GetActiveStressMode(Compiler compiler, Compiler.compStressArea area)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Active stress modes are only available in Debug builds.");
        }

        return ActiveStressModes(compiler)[(int)area];
    }

    public static void InitializeLsraStatistics(LinearScan allocator, int blockCount)
    {
        if (IsDebug && Configuration.TRACK_LSRA_STATS)
        {
            BlockStats(allocator) = new uint[]?[blockCount];
        }
    }

    public static uint GetReferenceNumber(RefPosition reference)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Reference numbers are only available in Debug builds.");
        }

        return reference.rpNum;
    }

    public static void SetReferenceNumber(RefPosition reference, uint number)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Reference numbers are only available in Debug builds.");
        }

        reference.rpNum = number;
    }

    public static bool GetCodeGenDone(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Code-generation phase state is only available in Debug builds.");
        }

        return CodeGenDone(compiler);
    }

    public static void SetCodeGenDone(Compiler compiler, bool value)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Code-generation phase state is only available in Debug builds.");
        }

        CodeGenDone(compiler) = value;
    }

    public static Dictionary<GenTree, TestLabelAndNum> GetNodeTestData(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("SSA test annotations are only available in Debug builds.");
        }

        return NodeTestData(compiler);
    }

    public static void CheckSsaTestConstraints(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("SSA test constraints are only available in Debug builds.");
        }

        SsaTestCheck(compiler);
    }

    public static void CheckValueNumberTestConstraints(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Value-number test constraints are only available in Debug builds.");
        }

        ValueNumberTestCheck(compiler);
    }

    public static void ApplyLocalFieldStress(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Local-field stress is only available in Debug builds.");
        }

        LocalFieldStress(compiler);
    }

    public static void ApplyWideMultiplyStress(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Wide-multiply stress is only available in Debug builds.");
        }

        WideMultiplyStress(compiler);
    }

    public static bool GetNoLocalFieldStress(LclVarDsc[] locals, int localNumber)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Local-field stress state is only available in Debug builds.");
        }

        return NoLocalFieldStress(ref locals[localNumber]);
    }

    public static void DumpUnwindInfo(UnwindInfo unwindInfo, bool isHotCode)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Unwind diagnostics are only available in Debug builds.");
        }

        UnwindInfoDump(unwindInfo, isHotCode, indent: 0);
    }

    public static unsafe void DumpUnwindInfo(
        Compiler compiler, bool isHotCode, uint startOffset, uint endOffset, byte* header, uint unwindBlockSize)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Unwind diagnostics are only available in Debug builds.");
        }

        s_globalsUnwindInfoDump.Value(compiler, isHotCode, startOffset, endOffset, header, unwindBlockSize);
    }

    public static void DumpEmitterJumpList(Emitter emitter)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Emitter jump-list diagnostics are only available in Debug builds.");
        }

        EmitJumpList(emitter);
    }

    public static GenTree? GetBuildNode(RefPosition reference)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Reference build nodes are only available in Debug builds.");
        }

        return reference.buildNode;
    }

    public static bool ShouldDumpAsciiTrees(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("ASCII tree dump configuration is only available in Debug builds.");
        }

        return GetShouldDumpAsciiTrees(compiler);
    }

    public static void CheckSsa(Compiler compiler)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("SSA verification is only available in Debug builds.");
        }

        FgDebugCheckSsa(compiler);
    }
}
