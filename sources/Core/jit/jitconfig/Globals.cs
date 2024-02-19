// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe partial class Globals
{
    private static readonly JitConfigValues JitConfig = new JitConfigValues();

    // Quadratic string matching algorithm that supports * and ? wildcards
    public static bool matchGlob(sbyte* pattern, sbyte* patternEnd, sbyte* str)
    {
        // Invariant: [patternStart..backtrackPattern) matches [stringStart..backtrackStr)
        sbyte* backtrackPattern = null;
        sbyte* backtrackStr = null;

        while (true)
        {
            if (pattern == patternEnd)
            {
                if (*str == '\0')
                {
                    return true;
                }
            }
            else if (*pattern == '*')
            {
                backtrackPattern = ++pattern;
                backtrackStr = str;
                continue;
            }
            else if (*str == '\0')
            {
                // No match since pattern needs at least one char in remaining cases.
            }
            else if ((*pattern == '?') || (*pattern == *str))
            {
                pattern++;
                str++;
                continue;
            }

            // In this case there was no match, see if we can backtrack to a wild
            // card and consume one more character from the string.
            if ((backtrackPattern == null) || (*backtrackStr == '\0'))
            {
                return false;
            }

            // Consume one more character for the wildcard.
            pattern = backtrackPattern;
            str = ++backtrackStr;
        }
    }
}
