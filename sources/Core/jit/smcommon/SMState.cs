// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct SMState
{
    /// <summary>does this state terminate a code sequence?</summary>
    public bool term;

    /// <summary>the length of currently matched opcodes</summary>
    public byte length;

    /// <summary>the ID of the longest matched terminate state</summary>
    public SM_STATE_ID longestTermState;

    /// <summary>previous state</summary>
    public SM_STATE_ID prevState;

    /// <summary>opcode that leads from the previous state to current state</summary>
    public SM_OPCODE opc;

    public short jumpTableByteOffset;
}
