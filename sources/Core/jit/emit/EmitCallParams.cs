// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Runtime.CompilerServices;
#endif

namespace RyuJitSharp;

public unsafe struct EmitCallParams
{
    public EmitCallType callType = EmitCallType.EC_COUNT;
    public CORINFO_METHOD_HANDLE methHnd = NO_METHOD_HANDLE;
#if DEBUG
    public StrongBox<CORINFO_SIG_INFO>? sigInfo;
#endif
    public void* addr;
    public nint argSize;
    public emitAttr retSize = EA_PTRSIZE;
    public emitAttr secondRetSize = EA_UNKNOWN;
    public bool hasAsyncRet;
    public VARSET_TP ptrVars = BitVecOps.UninitVal();
    public regMaskTP gcrefRegs;
    public regMaskTP byrefRegs;
    public regNumber ireg = REG_NA;
    public regNumber xreg = REG_NA;
    public uint xmul;
    public nint disp;
    public bool isJump;
    public bool noSafePoint;
    public GenTreeCall? returnValueCall;

    public EmitCallParams()
    {
    }
}
