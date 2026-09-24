// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    private sealed class CallDsc
    {
        public CallDsc? cdNext;
        public nint cdBlock;
        public uint cdOffs;

#if !JIT32_GCENCODER
        public ushort cdCallInstrSize;
#endif

        public ushort cdArgCnt;

        // cdArgCnt selects between the native union's masks and its argument table.
        public uint cdArgMask;
        public uint cdByrefArgMask;
        public uint[]? cdArgTable;

        public regMask cdGCrefRegs;
        public regMask cdByrefRegs;
    }
}
