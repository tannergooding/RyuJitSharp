// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    internal static JitConfigValues JitConfig;

    public const int CONST_CSE_ENABLE_ARM_RISCV64 = 0;
    public const int CONST_CSE_DISABLE_ALL = 1;
    public const int CONST_CSE_ENABLE_ARM_RISCV64_NO_SHARING = 2;
    public const int CONST_CSE_ENABLE_ALL = 3;
    public const int CONST_CSE_ENABLE_ALL_NO_SHARING = 4;

    public const int MAX_GDV_TYPE_CHECKS = 5;

    // Quadratic string matching algorithm that supports * and ? wildcards
    public static bool MatchGlob(ReadOnlySpan<byte> pattern, ReadOnlySpan<byte> str)
    {
        // Invariant: [patternStart..backtrackPattern) matches [stringStart..backtrackStr)
        var backtrackPattern = ReadOnlySpan<byte>.Empty;
        var backtrackStr = ReadOnlySpan<byte>.Empty;

        while (true)
        {
            if (pattern.IsEmpty)
            {
                if (str.IsEmpty)
                {
                    return true;
                }
            }
            else if (pattern[0] == '*')
            {
                pattern = pattern[1..];
                backtrackPattern = pattern;
                backtrackStr = str;
                continue;
            }
            else if (str.IsEmpty)
            {
                // No match since pattern needs at least one char in remaining cases.
            }
            else if ((pattern[0] == '?') || (pattern[0] == str[0]))
            {
                pattern = pattern[1..];
                str = str[1..];
                continue;
            }

            // In this case there was no match, see if we can backtrack to a wild
            // card and consume one more character from the string.
            if (backtrackPattern.IsEmpty || backtrackStr.IsEmpty)
            {
                return false;
            }

            // Consume one more character for the wildcard.
            pattern = backtrackPattern;
            backtrackStr = backtrackStr[1..];
            str = backtrackStr;
        }
    }
}
