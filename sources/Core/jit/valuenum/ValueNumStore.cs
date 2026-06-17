// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    /// <summary>We will reserve "negative one" to represent "not a value number", for maps that might start uninitialized.</summary>
    public const ValueNum NoVN = -1;

    private const int VNFOA_IllegalGenTreeOpShift = 0;
    private const int VNFOA_CommutativeShift = 1;
    private const int VNFOA_ArityShift = 2;
    private const int VNFOA_ArityBits = 3;
    private const int VNFOA_KnownNonNullShift = 5;

    /// <summary>Max arity we can represent.</summary>
    private const int VNFOA_MaxArity = (1 << VNFOA_ArityBits) - 1;
    private const int VNFOA_ArityMask = (int)(VNFOA_Arity4 | VNFOA_Arity2 | VNFOA_Arity1);

    public unsafe CORINFO_CLASS_HANDLE GetObjectType(ValueNum vn, out bool isExact, out bool isNonNull)
    {
        // TODO: Port ValueNumStore.GetObjectType

        isNonNull = false;
        isExact = false;

        return null;
    }

    internal static VNFOpAttrib GetOpAttribsForArity(genTreeOps oper, GenTreeOperKind kind)
    {
        var result = (oper is GT_SELECT) ? 3 : (((int)(kind & GTK_UNOP) >> 1) | ((int)(kind & GTK_BINOP) >> 1));
        result <<= VNFOA_ArityShift;
        return (VNFOpAttrib)(result & VNFOA_ArityMask);
    }

    internal static VNFOpAttrib GetOpAttribsForFunc(int arity, bool commute, bool knownNonNull)
    {
        var result = ((commute ? 1 : 0) << VNFOA_CommutativeShift);
        result |= (knownNonNull ? 1 : 0) << VNFOA_KnownNonNullShift;
        result |= ((arity & ~(arity >> 31)) << VNFOA_ArityShift) & VNFOA_ArityMask;
        return (VNFOpAttrib)(result);
    }

    internal static VNFOpAttrib GetOpAttribsForGenTree(genTreeOps oper, bool commute, bool illegalAsVNFunc, GenTreeOperKind kind)
    {
        var result = (int)(GetOpAttribsForArity(oper, kind));
        result |= (commute ? 1 : 0) << VNFOA_CommutativeShift;
        result |= (illegalAsVNFunc ? 1 : 0) << VNFOA_IllegalGenTreeOpShift;
        return (VNFOpAttrib)(result);
    }

    [Conditional("DEBUG")]
    public static void ValidateValueNumStoreStatics()
    {
        // TODO: Port ValueNumStore.ValidateValueNumStoreStatics
    }
}
