// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    /// <summary>We will reserve "negative one" to represent "not a value number", for maps that might start uninitialized.</summary>
    public const ValueNum NoVN = -1;

    // Chunk zero reserves SRC_Null, SRC_Void and SRC_EmptyExcSet in this order.
    public static ValueNum VNForNull() => 0;

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
        isNonNull = false;
        isExact = false;
        if (TypeOfVN(vn) is not TYP_REF)
        {
            return null;
        }

        if (IsVNObjHandle(vn))
        {
            isNonNull = true;
            isExact = true;
            return _compiler.info.compCompHnd->getObjectType((CORINFO_OBJECT_HANDLE)CoercedConstantValue<nuint>(vn));
        }

        var app = new VNFuncApp();
        if (!GetVNFunc(vn, ref app))
        {
            return null;
        }

        if (app.Func is VNF_CastClass or VNF_IsInstanceOf or VNF_JitNew)
        {
            if (IsVNTypeHandle(app.GetArg(0), out var handle))
            {
                isNonNull = app.Func is VNF_JitNew;
                isExact = isNonNull;
                return handle;
            }
        }

        if (app.Func is VNF_ObjGetType)
        {
            isNonNull = true;
            // RuntimeType need not be exact, for example under NativeAOT.
            return _compiler.info.compCompHnd->getBuiltinClass(CLASSID_RUNTIME_TYPE);
        }

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
#if DEBUG
        var attributes = new VNFOpAttrib[(int)VNF_COUNT];
        for (var oper = GT_NONE; oper < GT_COUNT; oper++)
        {
            var arity = oper.IsUnary ? 1 : oper.IsBinary ? 2 : (oper is GT_SELECT) ? 3 : 0;
            attributes[(int)oper] = (VNFOpAttrib)((arity << VNFOA_ArityShift) & VNFOA_ArityMask);
            if (oper.IsCommutative)
            {
                attributes[(int)oper] |= VNFOA_Commutative;
            }
        }

        VNFuncExtensions.InitializeValidationAttributes(attributes);

        ReadOnlySpan<genTreeOps> illegalOperators = [
            GT_IND, GT_NULLCHECK, GT_QMARK, GT_COLON, GT_LOCKADD, GT_XADD, GT_XCHG,
            GT_CMPXCHG, GT_LCLHEAP, GT_BOX, GT_XORR, GT_XAND, GT_STORE_LCL_VAR,
            GT_STORE_LCL_FLD, GT_STOREIND, GT_STORE_BLK, GT_COMMA, GT_ARR_ADDR,
            GT_BOUNDS_CHECK, GT_BLK, GT_INIT_VAL, GT_MDARR_LENGTH, GT_MDARR_LOWER_BOUND,
            GT_BITCAST, GT_NOP, GT_JTRUE, GT_RETURN, GT_RETURN_SUSPEND, GT_PATCHPOINT,
            GT_PATCHPOINT_FORCED, GT_SWITCH, GT_RETFILT, GT_CKFINITE, GT_SWIFT_ERROR_RET,
        ];

        foreach (var oper in illegalOperators)
        {
            attributes[(int)oper] |= VNFOA_IllegalGenTreeOp;
        }

        for (var func = VNF_NONE; func < VNF_COUNT; func++)
        {
            assert(attributes[(int)func] == VNFuncExtensions.GetAttributes(func));
        }
#endif
    }

#if DEBUG
    internal static void SetValidationAttributes(Span<VNFOpAttrib> attributes, VNFunc func,
        int arity, bool commute, bool knownNonNull)
    {
        if (commute)
        {
            attributes[(int)func] |= VNFOA_Commutative;
        }
        if (knownNonNull)
        {
            attributes[(int)func] |= VNFOA_KnownNonNull;
        }
        if (arity > 0)
        {
            attributes[(int)func] |= (VNFOpAttrib)((arity << VNFOA_ArityShift) & VNFOA_ArityMask);
        }
    }
#endif
}
