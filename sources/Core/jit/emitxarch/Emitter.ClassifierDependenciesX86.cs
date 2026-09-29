// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    public static bool IsApxZuCompatibleInstruction(instruction ins)
    {
        return false;
    }

    private bool IsSimdEvexEncodableInstruction(instruction ins)
    {
        return IsEvexEncodableInstruction(ins) && !IsApxExtendedEvexInstruction(ins);
    }

    public static bool IsLegacyMap1(ulong code)
    {
        return false;
    }
}
#endif
