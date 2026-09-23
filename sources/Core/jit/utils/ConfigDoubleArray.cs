// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public struct ConfigDoubleArray
{
    private double[]? _values;

    public unsafe void EnsureInit(byte* str)
    {
        if (_values is null)
        {
            Init(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(str));
        }
    }

    public readonly ReadOnlySpan<double> GetData() => _values;

    public readonly uint GetLength() => (uint)(_values?.Length ?? 0);

    public readonly void Dump()
    {
        if (_values is null)
        {
            jitprintf("<uninitialized config double array>\n");
            return;
        }

        if (_values.Length == 0)
        {
            jitprintf("<empty config double array>\n");
            return;
        }

        for (var i = 0; i < _values.Length; i++)
        {
            jitprintf($"{(i == 0 ? "" : ",")}{formatFloat(_values[i], "F6")} ");
        }
    }

    private void Init(ReadOnlySpan<byte> str)
    {
        double[] values = [];
        for (var pass = 0; pass < 2; pass++)
        {
            var remaining = str;
            var length = 0;
            while (!remaining.IsEmpty)
            {
                var c = remaining[0];
                if ((c == ',') || (c == ' ') || ((c >= '\t') && (c <= '\r')))
                {
                    remaining = remaining[1..];
                    continue;
                }

                var value = ParseValue(remaining, out var consumed);
                if (pass != 0)
                {
                    values[length] = value;
                }
                length++;
                remaining = remaining[consumed..];
            }

            if (pass == 0)
            {
                values = new double[length];
            }
        }
        _values = values;
    }

    private static double ParseValue(ReadOnlySpan<byte> text, out int consumed)
    {
        var unsigned = text[0] is (byte)'+' or (byte)'-' ? text[1..] : text;
        var isHex = (unsigned.Length >= 2) && (unsigned[0] == '0') && (unsigned[1] is (byte)'x' or (byte)'X');
        var style = isHex ? NumberStyles.HexFloat : NumberStyles.Float;
        if (double.TryParsePartial(text, style, CultureInfo.InvariantCulture, out var value, out consumed))
        {
            return value;
        }

        // The CRT also accepts "inf"; .NET's invariant symbol is "Infinity".
        if ((unsigned.Length >= 3) && Ascii.EqualsIgnoreCase(unsigned[..3], "inf"u8))
        {
            consumed = text.Length - unsigned.Length + 3;
            return text[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
        }

        throw new FormatException("Invalid floating-point JIT configuration value.");
    }
}
#endif
