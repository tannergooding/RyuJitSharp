// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Emitter
{
    public enum insSize : uint
    {
        ISZ_16BIT,
        ISZ_32BIT,
        // IT followed by an unconditional branch extends the conditional branch range.
        ISZ_48BIT,
    }
}
#endif
