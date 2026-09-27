// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RyuJitSharp;

public sealed unsafe class ReplayPolicy : DiscretionaryPolicy
{
    private static bool s_wroteReplayBanner;
    private static FileStream? s_replayFile;
    private static readonly Lock s_xmlReaderLock = new();
    private static bool s_endOfFile;

    private InlineContext? _inlineContext;
    private IL_OFFSET _offset = BAD_IL_OFFSET;
    private bool _wasForceInline;

    public ReplayPolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
        if ((s_replayFile is null) && !s_wroteReplayBanner)
        {
            var path = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(JitConfig.JitInlineReplayFile));
            try
            {
                s_replayFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                s_endOfFile = false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Native reports failed opens through the banner, unless XML records the policy.
            }

            if (JitConfig.JitInlineDumpXml == 0)
            {
                using var stream = Console.OpenStandardError();
                using var stderr = new JitTextWriter(stream, leaveOpen: true);
                stderr.Write($"*** {(s_replayFile is null ? "Unable to replay" : "Replaying")} inlines from {path}\n");
            }

            s_wroteReplayBanner = true;
        }
    }

    public override string Name => nameof(ReplayPolicy);

    public static void FinalizeXml()
    {
        s_replayFile?.Dispose();
        s_replayFile = null;
    }

    public override void NoteContext(InlineContext? context) => _inlineContext = context;

    public override void NoteOffset(IL_OFFSET offset) => _offset = offset;

    public override void NoteBool(InlineObservation observation, bool value)
    {
        if (!_isPrejitRoot && (observation == InlineObservation.CALLEE_IS_FORCE_INLINE))
        {
            _wasForceInline = value;
            value = false;
        }

        base.NoteBool(observation, value);
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        if (_isPrejitRoot)
        {
            base.DetermineProfitability(in methodInfo);
            return;
        }

        if (JitConfig.JitInlineDumpData != 0)
        {
            MethodInfoObservations(in methodInfo);
            EstimateCodeSize();
            EstimatePerformanceImpact();
            IsForceInline = _wasForceInline;
        }

        var accept = false;
        lock (s_xmlReaderLock)
        {
            if (FindMethod() && (_inlineContext is not null) && FindContext(_inlineContext))
            {
                accept = FindInline(methodInfo.ftn);
            }
        }

        if (accept)
        {
            _rootCompiler.JITLOG(LL_INFO100000, "Inline accepted via log replay");
            if (_isPrejitRoot)
            {
                SetCandidate(InlineObservation.CALLEE_LOG_REPLAY_ACCEPT);
            }
            else
            {
                SetCandidate(InlineObservation.CALLSITE_LOG_REPLAY_ACCEPT);
            }
        }
        else
        {
            _rootCompiler.JITLOG(LL_INFO100000, "Inline rejected via log replay");
            if (_isPrejitRoot)
            {
                SetNever(InlineObservation.CALLEE_LOG_REPLAY_REJECT);
            }
            else
            {
                SetFailure(InlineObservation.CALLSITE_LOG_REPLAY_REJECT);
            }
        }
    }

    private bool FindMethod()
    {
        if (s_replayFile is null)
        {
            return false;
        }

        var strategy = _rootCompiler._inlineStrategy;
        assert(strategy is not null);
        var position = strategy.MethodXmlFilePosition;
        if (position == -1)
        {
            return false;
        }
        else if (position > 0)
        {
            Seek(position);
            return true;
        }

        var methodToken = unchecked((uint)_rootCompiler.info.compCompHnd->getMethodDefFromMethod(_rootCompiler.info.compMethodHnd));
        var methodHash = unchecked((uint)_rootCompiler.info.compMethodHash());
        var found = false;
        Span<byte> buffer = stackalloc byte[255];
        Seek(0);

        while (TryReadLine(buffer, out var length))
        {
            if (buffer[..length].IndexOf("<Method>"u8) < 0)
            {
                continue;
            }

            if (!TryReadLine(buffer, out length))
            {
                break;
            }

            if (!ScanUnsigned(buffer[..length], "<Token>"u8, 16, 8, out var token) || (token != methodToken))
            {
                continue;
            }

            if (!TryReadLine(buffer, out length))
            {
                break;
            }

            if (!ScanUnsigned(buffer[..length], "<Hash>"u8, 16, 8, out var hash) || (hash != methodHash))
            {
                continue;
            }

            found = true;
            break;
        }

        strategy.MethodXmlFilePosition = found ? unchecked((int)s_replayFile.Position) : -1;
        return found;
    }

    private bool FindContext(InlineContext context)
    {
        if (context.IsRoot)
        {
            return true;
        }

        assert(context.Parent is not null);
        if (!FindContext(context.Parent))
        {
            return false;
        }

        var token = unchecked((uint)_rootCompiler.info.compCompHnd->getMethodDefFromMethod(context.Callee));
        var hash = unchecked((uint)_rootCompiler.compMethodHash(context.Callee));
        var offset = unchecked((uint)context.Location.Offset);
        return FindInline(token, hash, offset);
    }

    private bool FindInline(CORINFO_METHOD_HANDLE callee)
    {
        var token = unchecked((uint)_rootCompiler.info.compCompHnd->getMethodDefFromMethod(callee));
        var hash = unchecked((uint)_rootCompiler.compMethodHash(callee));
        var offset = _offset == BAD_IL_OFFSET ? -1 : _offset;
        return FindInline(token, hash, unchecked((uint)offset));
    }

    private bool FindInline(uint token, uint hash, uint offset)
    {
        Span<byte> buffer = stackalloc byte[255];
        var depth = 0;
        while (TryReadLine(buffer, out var length))
        {
            var line = buffer[..length];
            if (line.IndexOf("</Method>"u8) >= 0)
            {
                break;
            }

            if (line.IndexOf("<Inlines />"u8) >= 0)
            {
                if (depth == 0)
                {
                    break;
                }

                continue;
            }

            if (line.IndexOf("<Inlines>"u8) >= 0)
            {
                depth++;
                continue;
            }

            if (line.IndexOf("</Inlines>"u8) >= 0)
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                continue;
            }

            if ((depth != 1) || (line.IndexOf("<Inline>"u8) < 0))
            {
                continue;
            }

            if (!TryReadLine(buffer, out length))
            {
                break;
            }

            if (!ScanUnsigned(buffer[..length], "<Token>"u8, 16, 8, out var inlineToken) || (inlineToken != token))
            {
                continue;
            }

            if (!TryReadLine(buffer, out length))
            {
                break;
            }

            if (!ScanUnsigned(buffer[..length], "<Hash>"u8, 16, 8, out var inlineHash) || (inlineHash != hash))
            {
                continue;
            }

            if (!TryReadLine(buffer, out length))
            {
                break;
            }

            if (!ScanUnsigned(buffer[..length], "<Offset>"u8, 10, int.MaxValue, out var inlineOffset) || (inlineOffset != offset))
            {
                continue;
            }

            if (TryReadLine(buffer, out length) &&
                ScanUnsigned(buffer[..length], "<CollectData>"u8, 10, int.MaxValue, out var collectData))
            {
                _isDataCollectionTarget = collectData == 1;
            }

            return true;
        }

        return false;
    }

    private static FileStream ReplayFile => s_replayFile ?? throw new InvalidOperationException("Replay file is not open.");

    private static void Seek(int position)
    {
        _ = ReplayFile.Seek(position, SeekOrigin.Begin);
        s_endOfFile = false;
    }

    // Match fgets(buffer, 256) in CRT text mode, including byte limits and physical
    // seek positions. StreamReader.ReadLine would lose both bounds and offsets.
    private static bool TryReadLine(Span<byte> buffer, out int length)
    {
        var file = ReplayFile;
        length = 0;
        while (!s_endOfFile && (length < buffer.Length))
        {
            var value = file.ReadByte();
            if (value == -1)
            {
                s_endOfFile = true;
                break;
            }

#if HOST_WINDOWS
            if (value == 0x1a)
            {
                s_endOfFile = true;
                break;
            }

            if (value == '\r')
            {
                var next = file.ReadByte();
                if (next == '\n')
                {
                    value = '\n';
                }
                else if (next != -1)
                {
                    _ = file.Seek(-1, SeekOrigin.Current);
                }
            }
#endif

            buffer[length++] = (byte)value;
            if (value == '\n')
            {
                break;
            }
        }

        var readAny = length != 0;
        var terminator = buffer[..length].IndexOf((byte)0);
        if (terminator >= 0)
        {
            length = terminator;
        }

        return readAny;
    }

    // sscanf reports a successful conversion even if the closing XML tag does
    // not match. Hex width includes an optional sign and 0x prefix. The Windows
    // CRT accumulates into 64 bits, saturates overflow, then narrows to unsigned.
    private static bool ScanUnsigned(ReadOnlySpan<byte> line, ReadOnlySpan<byte> prefix, uint radix, int width, out uint value)
    {
        value = 0;
        while (!line.IsEmpty && IsSpace(line[0]))
        {
            line = line[1..];
        }

        if (!line.StartsWith(prefix))
        {
            return false;
        }

        line = line[prefix.Length..];
        while (!line.IsEmpty && IsSpace(line[0]))
        {
            line = line[1..];
        }

        line = line[..int.Min(line.Length, width)];
        var negative = !line.IsEmpty && (line[0] == '-');
        if (!line.IsEmpty && ((line[0] == '+') || negative))
        {
            line = line[1..];
        }

        if ((radix == 16) && (line.Length >= 2) && (line[0] == '0') && ((line[1] == 'x') || (line[1] == 'X')))
        {
            line = line[2..];
        }

        var count = 0;
        var accumulator = 0UL;
        var overflow = false;
        foreach (var character in line)
        {
            var digit = character switch {
                >= (byte)'0' and <= (byte)'9' => (uint)(character - '0'),
                >= (byte)'a' and <= (byte)'f' => (uint)(character - 'a' + 10),
                >= (byte)'A' and <= (byte)'F' => (uint)(character - 'A' + 10),
                _ => uint.MaxValue
            };
            if (digit >= radix)
            {
                break;
            }

            if (accumulator > ((ulong.MaxValue - digit) / radix))
            {
                accumulator = ulong.MaxValue;
                overflow = true;
            }
            else
            {
                accumulator = (accumulator * radix) + digit;
            }

            count++;
        }

        value = unchecked((uint)accumulator);
        if (negative && !overflow)
        {
            value = unchecked(0u - value);
        }

        return count != 0;
    }

    private static bool IsSpace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or (byte)'\v' or (byte)'\f';
}
#endif
