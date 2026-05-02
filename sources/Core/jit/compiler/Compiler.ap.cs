// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Data structures for assertion prop</summary>
    public BitVecTraits? apTraits;

    public unsafe ASSERT_TP apFull;

    public unsafe ASSERT_TP apLocal;

    public unsafe ASSERT_TP apLocalPostorder;

    public unsafe ASSERT_TP apLocalIfTrue;
}
