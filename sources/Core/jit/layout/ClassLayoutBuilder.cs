// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Drawing;

namespace RyuJitSharp;

public struct ClassLayoutBuilder
{
    public int m_size;

    // Array of CorInfoGCType (as BYTE) that describes the GC layout of the class.
    // For small classes the array is stored inline, avoiding an extra allocation and the pointer size overhead.
    internal nint _anonymous;

    public Compiler m_compiler;

    public ClassLayoutBuilder(Compiler compiler, int size)
    {
        m_compiler = compiler;
        m_size = size;
    }
}
