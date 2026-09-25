// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Emitter
{
    public sealed class instrDescDebugInfo
    {
        public uint idNum;
        public nuint idSize;
        public uint idVarRefOffs;
        public uint idVarRefOffs2;
        public nint idMemCookie;
        public GenTreeFlags idFlags = GTF_EMPTY;
        public bool idFinallyCall;
        public bool idCatchRet;
        public StrongBox<CORINFO_SIG_INFO>? idCallSig;
        public BasicBlock? idTargetBlock;
    }
}
