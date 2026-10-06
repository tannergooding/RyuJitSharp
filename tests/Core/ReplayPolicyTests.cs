// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ReplayPolicyTests
{
    [TestCase(7, 7, true)]
    [TestCase(7, 8, false)]
    [TestCase(-1, -1, true)]
    public static void ReplaySelectsTheExactCallsite(int recordedOffset, int callOffset, bool expected)
    {
        WithReplay((compiler, path) => {
            var callee = Helper(CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            File.WriteAllText(path, Method(compiler, Inline(compiler, callee, recordedOffset, "1")));
            var policy = Assess(compiler, callee, callOffset);
            Assert.That(policy.Observation, Is.EqualTo(expected
                ? InlineObservation.CALLSITE_LOG_REPLAY_ACCEPT
                : InlineObservation.CALLSITE_LOG_REPLAY_REJECT));
            Assert.That(policy.IsDataCollectionTarget, Is.EqualTo(expected));
            Assert.That(Strategy(compiler).MethodXmlFilePosition, Is.GreaterThan(0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReplayRequiresTheEnclosingContext(bool matchingContext)
    {
        WithReplay((compiler, path) => {
            var parent = Helper(CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            var callee = Helper(CorInfoHelpFunc.CORINFO_HELP_RNGCHKFAIL);
            var root = new InlineContext(Strategy(compiler));
            var context = new InlineContext(Strategy(compiler)) {
                _parent = root,
                _callee = parent,
                _location = new ILLocation(matchingContext ? 3 : 4, ICorDebugInfo.CALL_INSTRUCTION)
            };
            var inner = Inline(compiler, callee, 9, "0");
            File.WriteAllText(path, Method(compiler, Inline(compiler, parent, 3, "0", inner)));
            var policy = Assess(compiler, callee, 9, context);
            Assert.That(policy.Decision.IsCandidate, Is.EqualTo(matchingContext));
            Assert.That(policy.IsDataCollectionTarget, Is.False);
        });
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public static void RootLookupSkipsWrongTokensAndHashesAndCachesThePosition(string newline)
    {
        WithReplay((compiler, path) => {
            var callee = Helper(CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            var valid = Method(compiler, Inline(compiler, callee, 7));
            var wrongToken = valid.Replace($"<Token>{Token(compiler.info.compMethodHnd):x8}", "<Token>00000000", StringComparison.Ordinal);
            var wrongHash = valid.Replace($"<Hash>{compiler.compMethodHash(compiler.info.compMethodHnd):x8}", "<Hash>00000000", StringComparison.Ordinal);
            File.WriteAllText(path, (wrongToken + wrongHash + valid).Replace("\n", newline, StringComparison.Ordinal));
            Assert.That(Assess(compiler, callee, 7).Decision.IsCandidate, Is.True);
            var position = Strategy(compiler).MethodXmlFilePosition;
            Assert.That(position, Is.GreaterThan(Encoding.UTF8.GetByteCount(wrongToken + wrongHash)));
            Assert.That(Assess(compiler, callee, 7).Decision.IsCandidate, Is.True);
            Assert.That(Strategy(compiler).MethodXmlFilePosition, Is.EqualTo(position));
        });
    }

    [Test]
    public static void MissingMethodIsCachedAndForcedInlineStillRequiresReplay()
    {
        WithReplay((compiler, path) => {
            var callee = Helper(CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            File.WriteAllText(path, "<Method>\n<Token>00000000</Token>\n</Method>\n");
            Assert.That(Assess(compiler, callee, 7).Observation, Is.EqualTo(InlineObservation.CALLSITE_LOG_REPLAY_REJECT));
            Assert.That(Strategy(compiler).MethodXmlFilePosition, Is.EqualTo(-1));
            File.WriteAllText(path, Method(compiler, Inline(compiler, callee, 7)));
            Assert.That(Assess(compiler, callee, 7).Observation, Is.EqualTo(InlineObservation.CALLSITE_LOG_REPLAY_REJECT));
        });
    }

    [TestCase("")]
    [TestCase("0")]
    [TestCase("1")]
    [TestCase("2")]
    public static void CollectionMarkerDoesNotAffectAcceptance(string marker)
    {
        WithReplay((compiler, path) => {
            var callee = Helper(CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            File.WriteAllText(path, Method(compiler, Inline(compiler, callee, 7, marker)));
            var policy = Assess(compiler, callee, 7);
            Assert.That(policy.Decision.IsCandidate, Is.True);
            Assert.That(policy.IsDataCollectionTarget, Is.EqualTo(marker == "1"));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void MissingOrFinalizedFileIsNotReopened(bool createFile, bool disableReplayBeforeFinalize)
    {
        WithReplay((compiler, path) => {
            var callee = Helper(CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            if (createFile)
            {
                File.WriteAllText(path, Method(compiler, Inline(compiler, callee, 7)));
            }

            Assert.That(Assess(compiler, callee, 7).Decision.IsCandidate, Is.EqualTo(createFile));
            var file = ReplayFile(null);
            if (disableReplayBeforeFinalize)
            {
                var config = JitConfig;
                ReplayEnabled(ref config) = 0;
                JitConfig = config;
            }

            using var output = new StreamWriter(Stream.Null);
            InlineStrategy.FinalizeXml(output);
            if (file is not null)
            {
                Assert.That(file.CanRead, Is.False);
            }

            Assert.That(ReplayFile(null), Is.Null);
            File.WriteAllText(path, Method(compiler, Inline(compiler, callee, 7)));
            Assert.That(Assess(compiler, callee, 7).Observation, Is.EqualTo(InlineObservation.CALLSITE_LOG_REPLAY_REJECT));
        });
    }

    [TestCase(246, true)]
    [TestCase(247, false)]
    public static void ReplayUsesTheNative255ByteLineLimit(int padding, bool expected)
    {
        WithReplay((compiler, path) => {
            var callee = Helper(CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            var log = Method(compiler, Inline(compiler, callee, 7));
            File.WriteAllText(path, new string(' ', padding) + log);
            Assert.That(Assess(compiler, callee, 7).Decision.IsCandidate, Is.EqualTo(expected));
        });
    }

    [TestCase("<Token>06000042</Token>", 16, 8, true, 0x06000042u)]
    [TestCase(" \t<Token>0x060042</Token>", 16, 8, true, 0x00060042u)]
    [TestCase("<Token>060000429", 16, 8, true, 0x06000042u)]
    [TestCase("<Token>-1garbage", 16, 8, true, uint.MaxValue)]
    [TestCase("<Token>xyz", 16, 8, false, 0u)]
    [TestCase("<Token>+42broken", 10, int.MaxValue, true, 42u)]
    [TestCase("<Token>4294967296", 10, int.MaxValue, true, 0u)]
    [TestCase("<Token>18446744073709551616", 10, int.MaxValue, true, uint.MaxValue)]
    [TestCase("<Token>0x</Token>", 16, 8, false, 0u)]
    public static void ReplayPreservesScanfConversionAndWidth(string text, int radix, int width, bool expected, uint value)
    {
        var result = ScanUnsigned(null, Encoding.UTF8.GetBytes(text), "<Token>"u8, (uint)radix, width, out var actual);
        Assert.That(result, Is.EqualTo(expected));
        Assert.That(actual, Is.EqualTo(value));
    }

    private static ReplayPolicy Assess(Compiler compiler, CORINFO_METHOD_STRUCT_* callee, int offset, InlineContext? context = null)
    {
        var policy = new ReplayPolicy(compiler, false);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, true);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 10);
        policy.NoteContext(context ?? new InlineContext(Strategy(compiler)));
        policy.NoteOffset(offset);
        CORINFO_METHOD_INFO method = new() { ftn = callee };
        policy.DetermineProfitability(in method);
        return policy;
    }

    private static string Method(Compiler compiler, string contents)
    {
        return $"<Method>\n<Token>{Token(compiler.info.compMethodHnd):x8}</Token>\n" +
            $"<Hash>{compiler.compMethodHash(compiler.info.compMethodHnd):x8}</Hash>\n" +
            $"<Inlines>\n{contents}</Inlines>\n</Method>\n";
    }

    private static string Inline(Compiler compiler, CORINFO_METHOD_STRUCT_* callee, int offset, string marker = "", string children = "")
    {
        var collect = marker.Length == 0 ? "" : $"<CollectData>{marker}</CollectData>\n";
        return $"<Inline>\n<Token>{Token(callee):x8}</Token>\n<Hash>{compiler.compMethodHash(callee):x8}</Hash>\n" +
            $"<Offset>{unchecked((uint)offset)}</Offset>\n{collect}<Name>callee</Name>\n" +
            $"<Inlines>\n{children}</Inlines>\n</Inline>\n";
    }

    private static void WithReplay(Action<Compiler, string> action)
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.xml");
        var pathBytes = Encoding.UTF8.GetBytes(path + '\0');
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getMethodDefFromMethod = &GetToken;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info {
            compCompHnd = &jitInfo,
            compMethodHnd = Helper(CorInfoHelpFunc.CORINFO_HELP_THROW),
            compFullName = nameof(ReplayPolicyTests)
        };
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        StrategyCompiler(strategy) = compiler;
        compiler._inlineStrategy = strategy;

        var previousConfig = JitConfig;
        var previousCompiler = JitTls.Compiler;
        var previousFile = ReplayFile(null);
        var previousBanner = WroteReplayBanner(null);
        var previousEof = EndOfFile(null);
        using var tls = new JitTls(null);
        fixed (byte* pointer = pathBytes)
        {
            JitConfigValues config = new();
            ReplayPath(ref config) = pointer;
            ReplayEnabled(ref config) = 1;
            DumpXml(ref config) = 1;
            JitConfig = config;
            JitTls.Compiler = compiler;
            ReplayFile(null) = null;
            WroteReplayBanner(null) = false;
            EndOfFile(null) = false;
            try
            {
                action(compiler, path);
            }
            finally
            {
                ReplayPolicy.FinalizeXml();
                ReplayFile(null) = previousFile;
                WroteReplayBanner(null) = previousBanner;
                EndOfFile(null) = previousEof;
                JitConfig = previousConfig;
                JitTls.Compiler = previousCompiler;
                File.Delete(path);
            }
        }
    }

    private static InlineStrategy Strategy(Compiler compiler) => compiler._inlineStrategy ?? throw new InvalidOperationException("Missing strategy.");

    private static CORINFO_METHOD_STRUCT_* Helper(CorInfoHelpFunc helper) => (CORINFO_METHOD_STRUCT_*)(((int)helper << 2) | 1);

    private static uint Token(CORINFO_METHOD_STRUCT_* method) => 0x06000000u | (uint)(nuint)method;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetToken(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method) => unchecked((int)Token(method));

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_compiler")]
    private static extern ref Compiler StrategyCompiler(InlineStrategy strategy);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlineReplayFile")]
    private static extern ref byte* ReplayPath(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlinePolicyReplay")]
    private static extern ref int ReplayEnabled(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlineDumpXml")]
    private static extern ref int DumpXml(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_replayFile")]
    private static extern ref FileStream? ReplayFile(ReplayPolicy? policy);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_wroteReplayBanner")]
    private static extern ref bool WroteReplayBanner(ReplayPolicy? policy);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_endOfFile")]
    private static extern ref bool EndOfFile(ReplayPolicy? policy);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ScanUnsigned")]
    private static extern bool ScanUnsigned(ReplayPolicy? policy, ReadOnlySpan<byte> line, ReadOnlySpan<byte> prefix, uint radix, int width, out uint value);
}
#endif
