// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum insGroupPlaceholderType : byte
{
    IGPT_PROLOG,
    IGPT_EPILOG,
    IGPT_FUNCLET_PROLOG,
    IGPT_FUNCLET_EPILOG,
}

public sealed class insPlaceholderGroupData
{
    public insGroup? igPhNext;
    public BasicBlock? igPhBB;
    public VARSET_TP igPhInitGCrefVars = [];
    public regMaskTP igPhInitGCrefRegs;
    public regMaskTP igPhInitByrefRegs;
    public VARSET_TP igPhPrevGCrefVars = [];
    public regMaskTP igPhPrevGCrefRegs;
    public regMaskTP igPhPrevByrefRegs;
    public insGroupPlaceholderType igPhType;
}
