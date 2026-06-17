// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public const int NUM_SM_STATES = 250;

    // We rely on this to map the SM_OPCODE to single-opcode states. For example, in GetWeightForOpcode().
    public const int SM_STATE_ID_START = 1;

    public const int MAX_CODE_SEQUENCE_LENGTH = 7;

    public const SM_OPCODE CODE_SEQUENCE_END = (SM_OPCODE)(SM_COUNT + 1);
}
