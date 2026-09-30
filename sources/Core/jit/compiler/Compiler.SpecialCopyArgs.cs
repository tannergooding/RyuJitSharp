// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && FEATURE_IJW
namespace RyuJitSharp;

public partial class Compiler
{
    private bool recordArgRequiresSpecialCopy(int argNum)
    {
        if ((argNum < 0) || (argNum >= info.compArgsCount))
        {
            return false;
        }

        _specialCopyArgs ??= new bool[info.compArgsCount];
        _specialCopyArgs[argNum] = true;
        return true;
    }

    internal bool argRequiresSpecialCopy(int argNum)
    {
        return (argNum >= 0) && (argNum < info.compArgsCount) &&
            (_specialCopyArgs is not null) && _specialCopyArgs[argNum];
    }

    internal bool compHasSpecialCopyArgs()
    {
        return _specialCopyArgs is not null;
    }
}
#endif
