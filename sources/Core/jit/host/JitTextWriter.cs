// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace RyuJitSharp;

internal sealed class JitTextWriter : StreamWriter
{
    private readonly object _syncRoot = new();

    [SuppressMessage("Reliability", "CA2000", Justification = "The writer owns the file stream through its text-mode wrapper.")]
    public JitTextWriter(string path, bool append)
        : this(new FileStream(path, append ? FileMode.OpenOrCreate : FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
            leaveOpen: false, append)
    { }

    public JitTextWriter(Stream stream, bool leaveOpen)
        : this(stream, leaveOpen, append: false)
    { }

    [SuppressMessage("Reliability", "CA2000", Justification = "The base StreamWriter owns and disposes the text-mode wrapper.")]
    private JitTextWriter(Stream stream, bool leaveOpen, bool append)
        : base(new TextModeStream(stream, leaveOpen, append))
    {
        NewLine = "\n";
        if (append)
        {
            _ = BaseStream.Seek(0, SeekOrigin.End);
        }
    }

    // CRT FILE locking covers the character buffer, not just the underlying stream.
    // Raw-byte and position-dependent operations must use this same lock.
    internal static object GetSyncRoot(StreamWriter writer) => writer is JitTextWriter jitWriter ? jitWriter._syncRoot : writer;

    public override void Flush()
    {
        lock (_syncRoot)
        {
            base.Flush();
        }
    }

    public override void Write(char value)
    {
        lock (_syncRoot)
        {
            base.Write(value);
        }
    }

    public override void Write(char[]? buffer)
    {
        lock (_syncRoot)
        {
            base.Write(buffer);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_syncRoot)
        {
            base.Write(buffer, index, count);
        }
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        lock (_syncRoot)
        {
            base.Write(buffer);
        }
    }

    public override void Write(string? value)
    {
        lock (_syncRoot)
        {
            base.Write(value);
        }
    }

    public override void Write(string format, object? arg0)
    {
        lock (_syncRoot)
        {
            base.Write(format, arg0);
        }
    }

    public override void Write(string format, object? arg0, object? arg1)
    {
        lock (_syncRoot)
        {
            base.Write(format, arg0, arg1);
        }
    }

    public override void Write(string format, object? arg0, object? arg1, object? arg2)
    {
        lock (_syncRoot)
        {
            base.Write(format, arg0, arg1, arg2);
        }
    }

    public override void Write(string format, params object?[] arg)
    {
        lock (_syncRoot)
        {
            base.Write(format, arg);
        }
    }

    public override void Write(string format, params ReadOnlySpan<object?> arg)
    {
        lock (_syncRoot)
        {
            base.Write(format, arg);
        }
    }

    public override void WriteLine(string? value)
    {
        lock (_syncRoot)
        {
            base.WriteLine(value);
        }
    }

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        lock (_syncRoot)
        {
            base.WriteLine(buffer);
        }
    }

    public override void WriteLine(string format, object? arg0)
    {
        lock (_syncRoot)
        {
            base.WriteLine(format, arg0);
        }
    }

    public override void WriteLine(string format, object? arg0, object? arg1)
    {
        lock (_syncRoot)
        {
            base.WriteLine(format, arg0, arg1);
        }
    }

    public override void WriteLine(string format, object? arg0, object? arg1, object? arg2)
    {
        lock (_syncRoot)
        {
            base.WriteLine(format, arg0, arg1, arg2);
        }
    }

    public override void WriteLine(string format, params object?[] arg)
    {
        lock (_syncRoot)
        {
            base.WriteLine(format, arg);
        }
    }

    public override void WriteLine(string format, params ReadOnlySpan<object?> arg)
    {
        lock (_syncRoot)
        {
            base.WriteLine(format, arg);
        }
    }

    protected override void Dispose(bool disposing)
    {
        lock (_syncRoot)
        {
            base.Dispose(disposing);
        }
    }

    // The native Windows CRT translates every LF byte on text-mode output.
    // Translate below StreamWriter so embedded newlines and every Write overload
    // agree. An explicit CR before LF is preserved, just as it is by the CRT.
    private sealed class TextModeStream(Stream stream, bool leaveOpen, bool append) : Stream
    {
        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get
            {
                return stream.Position;
            }

            set
            {
                stream.Position = value;
            }
        }

        public override void Flush() => stream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

        public override void SetLength(long value) => stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // Native append streams follow the current EOF, including other JITs' writes.
            // Flush each encoded block so its buffered offset cannot become stale.
            if (append)
            {
                _ = stream.Seek(0, SeekOrigin.End);
            }

#if HOST_WINDOWS
            var newline = buffer.IndexOf((byte)('\n'));

            while (newline >= 0)
            {
                stream.Write(buffer[..newline]);
                stream.Write("\r\n"u8);
                buffer = buffer[(newline + 1)..];
                newline = buffer.IndexOf((byte)('\n'));
            }
#endif

            stream.Write(buffer);
            if (append)
            {
                stream.Flush();
            }
        }

        public override void WriteByte(byte value)
        {
            ReadOnlySpan<byte> buffer = [value];
            Write(buffer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
            {
                stream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
