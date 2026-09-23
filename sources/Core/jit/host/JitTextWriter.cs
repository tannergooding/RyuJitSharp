// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace RyuJitSharp;

internal sealed class JitTextWriter : StreamWriter
{
    [SuppressMessage("Reliability", "CA2000", Justification = "The writer owns the file stream through its text-mode wrapper.")]
    public JitTextWriter(string path, bool append)
        : this(new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read), leaveOpen: false)
    { }

    [SuppressMessage("Reliability", "CA2000", Justification = "The base StreamWriter owns and disposes the text-mode wrapper.")]
    public JitTextWriter(Stream stream, bool leaveOpen)
        : base(new TextModeStream(stream, leaveOpen))
    {
        NewLine = "\n";
    }

    // The native Windows CRT translates every LF byte on text-mode output.
    // Translate below StreamWriter so embedded newlines and every Write overload
    // agree. An explicit CR before LF is preserved, just as it is by the CRT.
    private sealed class TextModeStream(Stream stream, bool leaveOpen) : Stream
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
        }

        public override void WriteByte(byte value)
        {
#if HOST_WINDOWS
            if (value == (byte)('\n'))
            {
                stream.WriteByte((byte)('\r'));
            }
#endif

            stream.WriteByte(value);
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
