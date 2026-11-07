// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void AddStackLevel(uint adjustment)
    {
        var newStackLevel = unchecked(genStackLevel + adjustment);
        if (genStackLevel != newStackLevel)
        {
            JITDUMP($"Adjusting stack level from {unchecked((int)genStackLevel)} to {unchecked((int)newStackLevel)}\n");
        }
        genStackLevel = newStackLevel;
    }

    private void genSinglePush()
    {
        AddStackLevel(unchecked((uint)Configuration.REGSIZE_BYTES));
    }
}
