// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public ReadOnlySpan<char> genInsDisplayName(Emitter.instrDesc id)
    {
        if (Configuration.TARGET_XARCH)
        {
            return GetXarchInstructionDisplayName(id);
        }

        var ins = id.idIns();
        var insName = genInsName(ins);

        return insName;
    }
}
