// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InlineStrategyDiagnosticsTests
{
    [Test]
    public static void CsvHeaderAndContentsPreserveEmptyBasePolicySchema()
    {
        WithStrategy((strategy, compiler) => {
            using var stream = new MemoryStream();
            using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            strategy.DumpDataHeader(file);
            strategy.DumpDataContents(file);
            file.Write('\n');
            file.Flush();

            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(
                "*** Inline Data: Policy=DefaultPolicy JitInlineLimit=-1 ***\n" +
                "Method,Version,HotSize,ColdSize,JitTime,SizeEstimate,TimeEstimate,\n" +
                "06000042,0,12,3,0,12,37,\n"));
        }, dumpData: 1);
    }

    [Test]
    public static void NativeJitTimeRescalesTheAlreadyConvertedCounter()
    {
        WithStrategy((strategy, compiler) => {
            Assert.That(compiler.InlineDiagnosticJitTimeMicroseconds, Is.Zero);
            SetField(typeof(Compiler), compiler, "_compCycles", Stopwatch.Frequency);
            Assert.That(compiler.InlineDiagnosticJitTimeMicroseconds, Is.EqualTo(1_000_000u));
        });
    }

    [TestCase(0, 1, -1)]
    [TestCase(1, 1, 1)]
    [TestCase(1, 2, -1)]
    public static void CsvSelectionAvoidsStderrWhenDisabledByXmlOrInlineLimit(int dumpData, int dumpXml, int limit)
    {
        WithStrategy((strategy, compiler) => {
            strategy.DumpData();
            Assert.That(GetStaticField<bool>(typeof(InlineStrategy), "s_HasDumpedDataHeader"), Is.False);
        }, dumpData, dumpXml, limit);
    }

    [Test]
    public static void CsvLimitComparesInlineCountAsUnsigned()
    {
        WithStrategy((strategy, compiler) => {
            SetField(typeof(InlineStrategy), strategy, "_inlineCount", -1);
            SetField(typeof(InlineStrategy), strategy, "_lastSuccessfulPolicy", new DiscretionaryPolicy(compiler, false));
            _ = Assert.Throws<NotSupportedException>(() => strategy.DumpData());
            Assert.That(GetStaticField<bool>(typeof(InlineStrategy), "s_HasDumpedDataHeader"), Is.False);
        }, dumpData: 1, limit: 1);
    }

    [TestCase(1, 1, "<Methods>\n  <Method>")]
    [TestCase(2, 2, "<Methods>\n")]
    public static void XmlHeaderSchemaAndMinimalModeFollowNativeOrder(int xmlMode, int dataMode, string start)
    {
        WithStrategy((strategy, compiler) => {
            using var stream = new MemoryStream();
            using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            strategy.DumpXml(file, indent: 2);
            file.Flush();

            var text = Encoding.UTF8.GetString(stream.ToArray());
            var schema = dataMode == 1
                ? "Method,Version,HotSize,ColdSize,JitTime,SizeEstimate,TimeEstimate,"
                : "";
            var header = "<?xml version=\"1.0\"?>\n<InlineForest>\n<Policy>DefaultPolicy</Policy>\n" +
                $"<DataSchema>{schema}</DataSchema>\n";
            Assert.That(text, Does.StartWith(header + start));

            if (xmlMode == 1)
            {
                var hash = unchecked((uint)compiler.info.compMethodHash());
                var methodName = CorInfoHelpFunc.CORINFO_HELP_THROW.ToString();
                Assert.That(text, Is.EqualTo(header +
                    "<Methods>\n" +
                    "  <Method>\n" +
                    "    <Token>06000042</Token>\n" +
                    $"    <Hash>{hash:x8}</Hash>\n" +
                    "    <InlineCount>0</InlineCount>\n" +
                    "    <HotSize>12</HotSize>\n" +
                    "    <ColdSize>3</ColdSize>\n" +
                    "    <JitTime>0</JitTime>\n" +
                    "    <SizeEstimate>12</SizeEstimate>\n" +
                    "    <TimeEstimate>37</TimeEstimate>\n" +
                    $"    <Name>{methodName}</Name>\n" +
                    "    <Inlines/>\n" +
                    "  </Method>\n"));
            }
            else
            {
                Assert.That(text, Is.EqualTo(header + "<Methods>\n"));
            }

            InlineStrategy.FinalizeXml(file);
            file.Flush();
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Does.EndWith("</Methods>\n</InlineForest>\n"));
        }, dataMode, xmlMode);
    }

    [Test]
    public static void XmlDefaultsToJitStdoutAndFinalizesTheSameStream()
    {
        WithStrategy((strategy, compiler) => {
            using var stream = new MemoryStream();
            using var stdout = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            s_jitstdout = stdout;
            try
            {
                strategy.DumpXml();
                InlineStrategy.FinalizeXml();
                stdout.Flush();
#if HOST_WINDOWS
                const string newline = "\r\n";
#else
                const string newline = "\n";
#endif
                var expected = string.Join(newline,
                    "<?xml version=\"1.0\"?>", "<InlineForest>", "<Policy>DefaultPolicy</Policy>",
                    "<Methods>", "</Methods>", "</InlineForest>", "");
                Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
            }
            finally
            {
                s_jitstdout = previous;
            }
        }, dumpXml: 2);
    }

    [Test]
    public static void AotXmlIncludesRootAssessmentBeforeInlineTree()
    {
        WithStrategy((strategy, compiler) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            SetField(typeof(InlineStrategy), strategy, "_prejitRootDecision", InlineDecision.CANDIDATE);
            SetField(typeof(InlineStrategy), strategy, "_prejitRootObservation", InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE);

            using var stream = new MemoryStream();
            using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            strategy.DumpXml(file);
            file.Flush();

            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(output, Does.Contain(
                $"  <PrejitDecision>candidate</PrejitDecision>\n" +
                $"  <PrejitObservation>{InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE.String}</PrejitObservation>\n" +
                "  <Inlines/>\n"));
        }, dumpXml: 1);
    }

    [Test]
    public static void InlineXmlSuppressesFailedNodesAfterProcessingTheirSiblings()
    {
        WithStrategy((strategy, compiler) => {
            var root = new InlineContext(strategy);
            var successful = new InlineContext(strategy) {
                _parent = root,
                _callee = compiler.info.compMethodHnd,
                _ilSize = 7
            };
            SetField(typeof(InlineContext), successful, "_observation", InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE);
            var failed = new InlineContext(strategy) {
                _parent = root,
                _sibling = successful,
                _callee = compiler.info.compMethodHnd,
                _flags = InlineContext.Flags.None
            };
            SetField(typeof(InlineContext), failed, "_observation", InlineObservation.CALLEE_IS_NOINLINE);
            root._child = failed;

            using var stream = new MemoryStream();
            using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            root.DumpXml(file, 2);
            file.Flush();

            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(output, Does.Contain("<Inline>\n"));
            Assert.That(output, Does.Contain("<Offset>4294967295</Offset>\n"));
            Assert.That(output, Does.Contain("<Inlines />\n"));
            Assert.That(output, Does.Not.Contain("<FailedInline>"));
            Assert.That(output, Does.StartWith("  <Inlines>\n    <Inline>\n"));
        }, dumpXml: 3);
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void InlineXmlUsesRootHashOrNativeCalleeNameHash(bool rootHandle)
    {
        WithStrategy((strategy, compiler) => {
            var callee = rootHandle ? compiler.info.compMethodHnd
                : (CORINFO_METHOD_STRUCT_*)(((int)CorInfoHelpFunc.CORINFO_HELP_RNGCHKFAIL << 2) | 1);
            var inline = new InlineContext(strategy) {
                _parent = new InlineContext(strategy),
                _callee = callee
            };
            SetField(typeof(InlineContext), inline, "_observation", InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE);

            using var stream = new MemoryStream();
            using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            inline.DumpXml(file, 0);
            file.Flush();

            var expectedHash = rootHandle ? compiler.info.compMethodHash()
                : HashString(CorInfoHelpFunc.CORINFO_HELP_RNGCHKFAIL.ToString());
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                Does.Contain($"  <Hash>{unchecked((uint)expectedHash):x8}</Hash>\n"));
        }, dumpXml: 1);
    }

    [TestCase("\u00e9", 0x0059688fu)]
    [TestCase("\u00e9\0ignored", 0x0059688fu)]
    public static void CalleeHashUsesNativeSignedUtf8BytesUntilTerminator(string name, uint expected)
    {
        var method = typeof(InlineContext).GetMethod("HashMethodName", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing native callee hash.");
        Assert.That(method.Invoke(null, [name]), Is.EqualTo(expected));
    }

    [TestCase("A<&>Z", "A[#]Z")]
    [TestCase("ABC", "ABC")]
    public static void XmlNamesUseNativeByteReplacement(string name, string expected)
    {
        using var stream = new MemoryStream();
        using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        InlineContext.WriteEscapedName(file, name);
        file.Flush();
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
    }

    [Test]
    public static void XmlNamesTruncateAtNativeByteBoundary()
    {
        using var stream = new MemoryStream();
        using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        InlineContext.WriteEscapedName(file, new string('a', 1022) + "\u00e9");
        file.Flush();
        var bytes = stream.ToArray();

        Assert.That(bytes, Has.Length.EqualTo(1023));
        Assert.That(bytes[1022], Is.EqualTo(0xc3));
    }

    [Test]
    public static void MissingDiscretionarySchemaFailsBeforeWritingHeader()
    {
        WithStrategy((strategy, compiler) => {
            SetField(typeof(InlineStrategy), strategy, "_lastSuccessfulPolicy", new DiscretionaryPolicy(compiler, false));
            using var stream = new MemoryStream();
            using var file = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            _ = Assert.Throws<NotSupportedException>(() => strategy.DumpXml(file));
            file.Flush();
            Assert.That(stream.Length, Is.Zero);
        }, dumpData: 1, dumpXml: 1);
    }

    private static void WithStrategy(Action<InlineStrategy, Compiler> action, int dumpData = 0, int dumpXml = 0, int limit = -1)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getMethodDefFromMethod = &GetMethodToken;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        CORINFO_METHOD_INFO methodInfo = default;
        methodInfo.ILCodeSize = 10;
        JitFlags flags = default;

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info {
            compCompHnd = &jitInfo,
            compMethodHnd = (CORINFO_METHOD_STRUCT_*)(((int)CorInfoHelpFunc.CORINFO_HELP_THROW << 2) | 1),
            compMethodInfo = &methodInfo,
            compFullName = nameof(InlineStrategyDiagnosticsTests),
            compTotalHotCodeSize = 12,
            compTotalColdCodeSize = 3
        };
        compiler.opts.jitFlags = &flags;
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_compiler", compiler);
        SetField(typeof(InlineStrategy), strategy, "_maxInlineSize", 32);
        SetField(typeof(InlineStrategy), strategy, "_currentSizeEstimate", 120);
        SetField(typeof(InlineStrategy), strategy, "_currentTimeEstimate", 37);
        compiler._inlineStrategy = strategy;

        object config = new JitConfigValues();
        SetField(typeof(JitConfigValues), config, "_jitInlineDumpData", dumpData);
        SetField(typeof(JitConfigValues), config, "_jitInlineDumpXml", dumpXml);
        SetField(typeof(JitConfigValues), config, "_jitInlineLimit", limit);
        var previousConfig = JitConfig;
        var previousCompiler = JitTls.Compiler;
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        JitConfig = (JitConfigValues)config;
        JitTls.Compiler = compiler;
        SetField(typeof(InlineStrategy), null, "s_HasDumpedDataHeader", false);
        SetField(typeof(InlineStrategy), null, "s_HasDumpedXmlHeader", false);

        try
        {
            Assert.That(JitConfig.JitInlineDumpData, Is.EqualTo(dumpData));
            Assert.That(JitConfig.JitInlineDumpXml, Is.EqualTo(dumpXml));
            Assert.That(JitConfig.JitInlineLimit, Is.EqualTo(limit));
            action(strategy, compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
            SetField(typeof(InlineStrategy), null, "s_HasDumpedDataHeader", false);
            SetField(typeof(InlineStrategy), null, "s_HasDumpedXmlHeader", false);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetMethodToken(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method) => 0x06000042;

    private static T GetStaticField<T>([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type, string name)
    {
        var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {name}");
        return (T)(field.GetValue(null) ?? throw new InvalidOperationException($"Missing value {name}"));
    }

    private static void SetField([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        object? target, string name, object value)
    {
        var flags = (target is null ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.NonPublic;
        var field = type.GetField(name, flags) ?? throw new InvalidOperationException($"Missing field {name}");
        field.SetValue(target, value);
    }
}
#endif
