// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public unsafe bool compJitHaltMethod()
    {
        if (JitConfig.JitHalt.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
        {
            return true;
        }

        var hash = unchecked((uint)JitConfig.JitHashHalt);
        return (hash != uint.MaxValue) && (hash == info.compMethodHash());
    }
#endif
}
