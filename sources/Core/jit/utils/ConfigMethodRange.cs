// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

/// <summary>describes a set of methods, specified via their hash codes.</summary>
/// <remarks>
///   <para>This can be used for binary search and/or specifying an explicit method set.</para>
///   <para>Note method hash codes are not necessarily unique. For instance many IL stubs may have the same hash.</para>
///   <para>If range string is null or just whitespace, range includes all methods.</para>
///   <para>Parses values as decimal numbers.</para>
///   <list type="bullet">
///     <item>99998888 12345678-23456789 : a range of methods plus a single method</item>
///     <item>         12345678-23456789 : a range of methods</item>
///     <item>                  12345678 : a single method</item>
///     <item> [string with just spaces] : all methods</item>
///   </list>
/// </remarks>
public partial struct ConfigMethodRange
{
    public const int DEFAULT_CAPACITY = 50;

    /// <summary>ranges of functions to include</summary>
    private Range[]? _ranges; 

    /// <summary>count of low-high pairs</summary>
    private int _rangeCount;

    private int _badChar;

    /// <summary>index + 1 of any bad character in range string</summary>
    public readonly int BadCharIndex => _badChar - 1;

    public readonly bool Error => _badChar != 0;

    public readonly bool IsEmpty => _rangeCount == 0;

    /// <summary>check if the range includes a particular hash</summary>
    /// <param name="hash">hash value to check</param>
    /// <returns></returns>
    public readonly bool Contains(int hash)
    {
        assert(_ranges is not null);

        if (_rangeCount == 0)
        {
            // No ranges specified means all methods included.
            return true;
        }

        foreach (var range in _ranges.AsSpan(0, _rangeCount))
        {
            if ((range.Low <= hash) && (hash <= range.High))
            {
                return true;
            }
        }
        return false;
    }

    // Ensure the range string has been parsed.
    [MemberNotNull(nameof(_ranges))]
    public unsafe void EnsureInit(byte* rangeStr, int capacity = DEFAULT_CAPACITY)
    {
        if (_ranges is null)
        {
            InitRanges(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(rangeStr), capacity);
            assert(_ranges is not null);
        }
    }

    /// <summary>dump hash ranges to stdout</summary>
    public readonly void Dump()
    {
        if (_ranges is null)
        {
            jitprintf("<uninitialized method range>\n");
            return;
        }

        if (_rangeCount == 0)
        {
            jitprintf("<empty method range>\n");
            return;
        }

        var ranges = _ranges.AsSpan(0, _rangeCount);
        jitprintf($"<method range with {ranges.Length} entries>\n");

        for (var i = 0; i < ranges.Length; i++)
        {
            var range = ranges[i];

            if (range.Low == range.High)
            {
                jitprintf($"{i} [0x{range.Low:X8}]\n");
            }
            else
            {
                jitprintf($"{i} [0x{range.Low:X8}-0x{range.High:X8}]\n");
            }
        }
    }

    /// <summary>Parse the range string and set up the range info.</summary>
    /// <param name="rangeStr">String to parse (may be null)</param>
    /// <param name="capacity">Number ranges to allocate in the range array</param>
    /// <remarks>Does some internal error checking; clients can use <see cref="Error" /> to determine if the range string couldn't be fully parsed because of bad characters or too many entries, or had values that were too large to represent.</remarks>
    private void InitRanges(ReadOnlySpan<byte> rangeStr, int capacity)
    {
        // Make sure that the memory was zero initialized
        assert(_ranges is null);
        assert(_rangeCount == 0);
        assert(_badChar == 0);

        // Flag any strange-looking requests
        assert(capacity < 100000);

        if (rangeStr.IsEmpty)
        {
            _ranges = [];
            return;
        }

        // Allocate some persistent memory
        var ranges = new Range[capacity];
        var rangeCount = 0;
        var badChar = 0;

        var totalIndex = 0;
        var setHighPart = false;

        while ((rangeStr.Length != 0) && (rangeCount < ranges.Length))
        {
            var nextIndex = rangeStr.IndexOfAnyExcept((byte)(' '), (byte)(','));

            if (nextIndex >= 0)
            {
                rangeStr = rangeStr[nextIndex..];
                totalIndex += nextIndex;
            }

            var value = 0;
            var currentChar = (char)(rangeStr[0]);

            while (char.IsAsciiHexDigit(currentChar))
            {
                var digit = (currentChar is >= '0' and <= '9') ? (currentChar - '0') : ((currentChar | 0x20) - 'a' + 10);
                var newValue = (value * 16) + digit;

                if ((badChar == 0) && (newValue <= value))
                {
                    // Check for overflow
                    badChar = totalIndex + 1;
                }
                value = newValue;

                rangeStr = rangeStr[1..];
                totalIndex++;

                currentChar = (char)(rangeStr[0]);
            }

            ref var range = ref ranges[rangeCount];

            // Was this the high part of a low-high pair?
            if (setHighPart)
            {
                // Yep, set it and move to the next range
                range.High = unchecked((uint)(value));

                // Sanity check that range is proper
                if ((badChar == 0) && (range.High < range.Low))
                {
                    badChar = totalIndex + 1;
                }

                rangeCount++;
                setHighPart = false;
                continue;
            }

            // Must have been looking for the low part of a range
            range.Low = unchecked((uint)(value));

            nextIndex = rangeStr.IndexOfAnyExcept((byte)(' '));

            if (nextIndex >= 0)
            {
                rangeStr = rangeStr[nextIndex..];
                totalIndex += nextIndex;
            }

            // Was that the low part of a low-high pair?
            if (rangeStr[0] == '-')
            {
                // Yep, skip the dash and set high part next time around.
                rangeStr = rangeStr[1..];
                totalIndex++;

                setHighPart = true;
                continue;
            }

            // Else we have a point range, so set high = low
            range.High = unchecked((uint)(value));
            rangeCount++;
        }

        // If we didn't parse the full range string, note index of the the
        // first bad char.
        if ((badChar == 0) && (rangeStr.Length != 0))
        {
            badChar = totalIndex + 1;
        }

        // Finish off any remaining open range
        if (setHighPart)
        {
            ranges[rangeCount].High = uint.MaxValue;
            rangeCount++;
        }

        assert(rangeCount <= ranges.Length);
        _rangeCount = rangeCount;
        _ranges = ranges;
        _badChar = badChar;
    }
}
#endif
