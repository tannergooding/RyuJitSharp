// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insOpts;

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool insOpts64BitExtend(insOpts opt)
    {
        return (opt == INS_OPTS_UXTX) || (opt == INS_OPTS_SXTX);
    }
}
