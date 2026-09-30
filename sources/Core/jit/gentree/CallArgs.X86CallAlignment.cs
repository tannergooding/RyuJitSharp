// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if UNIX_X86_ABI
namespace RyuJitSharp;

public partial struct CallArgs
{
    public readonly uint GetStkSizeBytes() => unchecked((uint)_stkSizeBytes);

    public void SetStkSizeBytes(uint bytes)
    {
        _stkSizeBytes = unchecked((int)bytes);
    }

    public readonly uint GetStkAlign() => unchecked((uint)_padStkAlign);

    public void ComputeStackAlignment(uint stackLevel)
    {
        _padStkAlign = unchecked((int)((STACK_ALIGN - (stackLevel % STACK_ALIGN)) % STACK_ALIGN));
    }
}
#endif
