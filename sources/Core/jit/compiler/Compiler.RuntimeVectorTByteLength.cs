// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;

namespace RyuJitSharp;

public partial class Compiler
{
    public uint getRuntimeVectorTByteLength()
    {
        throw new NotImplementedException("Compiler.getRuntimeVectorTByteLength is not yet ported.");
    }
}
#endif
