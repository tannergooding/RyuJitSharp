// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct CORINFO_HELPER_DESC
{
    public CorInfoHelpFunc helperNum;

    public int numArgs;

    public InlineArrayCorInfoAccessAllowedMaxArgs<CORINFO_HELPER_ARG> args;
}
