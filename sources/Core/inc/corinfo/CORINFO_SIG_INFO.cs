// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct CORINFO_SIG_INFO
{
    public CorInfoCallConv callConv;

    /// <summary>If the return type is a value class, this is its handle (enums are normalized).</summary>
    public unsafe CORINFO_CLASS_HANDLE retTypeClass;

    /// <summary>Returns the value class as it is in the sig (enums are not converted to primitives).</summary>
    public unsafe CORINFO_CLASS_HANDLE retTypeSigClass;

    private int _bitfield;

    public CorInfoType retType
    {
        readonly get
        {
            return unchecked((CorInfoType)(byte)(_bitfield));
        }

        set
        {
            _bitfield = (_bitfield & ~0xFF) | (byte)(value);
        }
    }

    /// <summary>Used by IL stubs code.</summary>
    public byte flags
    {
        readonly get
        {

            return unchecked((byte)(_bitfield >>> 8));
        }

        set
        {
            _bitfield = (_bitfield & ~(0xFF << 8)) | (value << 8);
        }
    }

    public ushort numArgs
    {
        readonly get
        {

            return unchecked((ushort)(_bitfield >>> 16));
        }

        set
        {
            _bitfield = (_bitfield & ~(0xFFFF << 16)) | (value << 16);
        }
    }

    /// <summary>Information about how type variables are being instantiated in generic code.</summary>
    public CORINFO_SIG_INST sigInst;

    public unsafe CORINFO_ARG_LIST_HANDLE args;

    public unsafe PCCOR_SIGNATURE pSig;

    public int cbSig;

    /// <summary>Used in place of pSig and cbSig to reference a method signature object handle.</summary>
    public unsafe MethodSignatureInfo* methodSignature;

    /// <summary>Passed to getArgClass.</summary>
    public unsafe CORINFO_MODULE_HANDLE scope;

    public mdToken token;

    public readonly CorInfoCallConv getCallConv() => callConv & CORINFO_CALLCONV_MASK;

    public readonly bool hasThis() => (callConv & CORINFO_CALLCONV_HASTHIS) != 0;

    public readonly bool hasExplicitThis() => (callConv & CORINFO_CALLCONV_EXPLICITTHIS) != 0;

    public readonly bool hasImplicitThis() => (callConv & (CORINFO_CALLCONV_HASTHIS | CORINFO_CALLCONV_EXPLICITTHIS)) == CORINFO_CALLCONV_HASTHIS;

    public readonly int totalILArgs() => numArgs + (hasImplicitThis() ? 1 : 0);

    public readonly bool isVarArg() => getCallConv() is CORINFO_CALLCONV_VARARG or CORINFO_CALLCONV_NATIVEVARARG;

    public readonly bool hasTypeArg() => (callConv & CORINFO_CALLCONV_PARAMTYPE) != 0;

    public readonly bool isAsyncCall() => (callConv & CORINFO_CALLCONV_ASYNCCALL) != 0;
}
