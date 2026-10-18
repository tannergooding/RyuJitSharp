using System;
using System.Collections.Generic;
using System.IO;
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "rpNum")]
    private static extern ref uint ReferenceNumber(RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "buildNode")]
    private static extern ref GenTree? BuildNode(RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ShouldDumpAsciiTrees")]
    private static extern bool GetShouldDumpAsciiTrees(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgDebugCheckSsa")]
    private static extern void FgDebugCheckSsa(Compiler compiler);

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

        return ReferenceNumber(reference);
    }

    public static GenTree? GetBuildNode(RefPosition reference)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Reference build nodes are only available in Debug builds.");
        }

        return BuildNode(reference);
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
