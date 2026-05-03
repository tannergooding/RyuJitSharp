// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct LogEnv
{
    private readonly unsafe ICorJitInfo* _jitInfo;
    private Compiler? _compiler;

    public unsafe LogEnv(ICorJitInfo* jitInfo)
    {
        _jitInfo = jitInfo;
    }

    public readonly unsafe ICorJitInfo* JitInfo => _jitInfo;

    public Compiler? Compiler
    {
        readonly get
        {
            return _compiler;
        }

        set
        {
            _compiler = value;
        }
    }
}
