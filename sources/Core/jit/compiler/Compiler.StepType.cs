// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const StepType ST_None = StepType.ST_None;
    public const StepType ST_FinallyReturn = StepType.ST_FinallyReturn;
    public const StepType ST_Catch = StepType.ST_Catch;
    public const StepType ST_Try = StepType.ST_Try;

    public enum StepType
    {
        // No step type; step is null.
        ST_None,

        // The step block is the BBJ_CALLFINALLYRET block of a BBJ_CALLFINALLY/BBJ_CALLFINALLYRET pair.
        // That is, is step.GetFinallyContinuation() is where a finally will return to.
        ST_FinallyReturn,

        // The step block is a catch return.
        ST_Catch,

        // The step block is in a "try", created as the target for a finally return or the target for a catch return.
        ST_Try
    }
}
