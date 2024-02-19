// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public struct CORINFO_HELPER_DESC
{
    public CorInfoHelpFunc helperNum;

    public uint numArgs;

    private _args_e__FixedBuffer _args;

    [UnscopedRef]
    public Span<CORINFO_HELPER_ARG> args => _args;

    [InlineArray(CORINFO_ACCESS_ALLOWED_MAX_ARGS)]
    private struct _args_e__FixedBuffer
    {
        public CORINFO_HELPER_ARG e0;
    }
}
