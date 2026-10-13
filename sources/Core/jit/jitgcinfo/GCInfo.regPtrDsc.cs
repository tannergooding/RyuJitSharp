// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    internal enum rpdArgType_t : ushort
    {
        rpdARG_POP,
        rpdARG_PUSH,
        rpdARG_KILL,
    }

    internal enum GCtype : uint
    {
        GCT_NONE,
        GCT_GCREF,
        GCT_BYREF,
    }

    internal sealed class regPtrDsc
    {
        public regPtrDsc? rpdNext;
        public uint rpdOffs;

        // The native compiler and call data overlap; their uses are selected by rpdArg/rpdCall.
        public CompilerRegData rpdCompiler;
        public CallRegData rpdCallData;

        public byte rpdCallInstrSize;
        public bool rpdArg;
        internal rpdArgType_t rpdArgType;
        internal GCtype rpdGCtype;
        public bool rpdIsThis;
        public bool rpdCall;

        internal rpdArgType_t rpdArgTypeGet() => rpdArgType;

        internal GCtype rpdGCtypeGet() => rpdGCtype;

        public bool rpdIsCallInstr() => rpdCall && (rpdCallInstrSize != 0);

        public struct CompilerRegData
        {
            public regMask rpdAdd;
            public regMask rpdDel;
        }

        public struct CallRegData
        {
            public uint rpdCallGCrefRegs;
            public uint rpdCallByrefRegs;
            public ushort rpdPtrArg;
        }
    }
}
