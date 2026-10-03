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

#if !TARGET_WASM
    private void genSinglePush()
    {
        AddStackLevel(REGSIZE_BYTES);
    }
#endif

#if UNIX_X86_ABI
    private uint maxNestedAlignment;

    private void SubtractNestedAlignment(uint adjustment)
    {
        assert(curNestedAlignment >= adjustment);
        var newNestedAlignment = curNestedAlignment - adjustment;
        if (curNestedAlignment != newNestedAlignment)
        {
            JITDUMP($"Adjusting stack nested alignment from {unchecked((int)curNestedAlignment)} to {unchecked((int)newNestedAlignment)}\n");
        }
        curNestedAlignment = newNestedAlignment;
    }

    private void AddNestedAlignment(uint adjustment)
    {
        var newNestedAlignment = unchecked(curNestedAlignment + adjustment);
        if (curNestedAlignment != newNestedAlignment)
        {
            JITDUMP($"Adjusting stack nested alignment from {unchecked((int)curNestedAlignment)} to {unchecked((int)newNestedAlignment)}\n");
        }
        curNestedAlignment = newNestedAlignment;

        if (curNestedAlignment > maxNestedAlignment)
        {
            JITDUMP($"Max stack nested alignment changed from {unchecked((int)maxNestedAlignment)} to {unchecked((int)curNestedAlignment)}\n");
            maxNestedAlignment = curNestedAlignment;
        }
    }
#endif
}
