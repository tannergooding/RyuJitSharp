// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public struct TestLabelAndNum
{
    // We have the ability to mark source expressions with "Test Labels."
    // These drive assertions within the JIT, or internal JIT testing.  For example, we could label expressions
    // that should be CSE defs, and other expressions that should uses of those defs, with a shared label.

    public TestLabel _tl;
    public nint _num;
}
#endif
