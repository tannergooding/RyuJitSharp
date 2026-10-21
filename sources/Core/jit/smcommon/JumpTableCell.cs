// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.InteropServices;

namespace RyuJitSharp;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct JumpTableCell
{
    public SM_STATE_ID srcState;
    public SM_STATE_ID destState;
}
