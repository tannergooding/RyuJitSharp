// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public struct FrameInfo
    {
#if TARGET_ARM64
        // Frame type (1-5).
        public int frameType;

        // Distance from established (method body) SP to base of callee save area.
        public int calleeSaveSpOffset;

        // Amount to subtract from SP before saving (prolog) OR
        // to add to SP after restoring (epilog) callee saves.
        public int calleeSaveSpDelta;

        // Distance from established SP to where caller's FP was saved.
        public int offsetSpToSavedFp;
#endif
    }
}
