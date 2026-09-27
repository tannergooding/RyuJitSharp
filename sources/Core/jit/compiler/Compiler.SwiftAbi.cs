// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
#if SWIFT_SUPPORT
    /// <remarks>Boxed records keep returned references stable when the cache grows.</remarks>
    public unsafe ref readonly CORINFO_SWIFT_LOWERING GetSwiftLowering(CORINFO_CLASS_HANDLE hClass)
    {
        var cache = _swiftLoweringCache ??= [];
        var key = new Pointer<CORINFO_CLASS_STRUCT_>(hClass);

        if (!cache.TryGetValue(key, out var lowering))
        {
            lowering = new StrongBox<CORINFO_SWIFT_LOWERING>();

            fixed (CORINFO_SWIFT_LOWERING* pLowering = &lowering.Value)
            {
                info.compCompHnd->getSwiftLowering(hClass, pLowering);
            }

            cache[key] = lowering;
        }

        return ref lowering.Value;
    }

    private unsafe bool lvaInitSpecialSwiftParam(CORINFO_ARG_LIST_HANDLE argHnd, int lclNum, CorInfoType type,
        CORINFO_CLASS_HANDLE typeHnd)
    {
        var argIsByrefOrPtr = type is CORINFO_TYPE_BYREF or CORINFO_TYPE_PTR;

        if (argIsByrefOrPtr)
        {
            assert(typeHnd == NO_CLASS_HANDLE);
            var clsHnd = info.compCompHnd->getArgClass(&info.compMethodInfo->args, argHnd);
            type = info.compCompHnd->getChildType(clsHnd, &typeHnd);
        }

        if ((type is not CORINFO_TYPE_VALUECLASS) || !info.compCompHnd->isIntrinsicType(typeHnd))
        {
            return false;
        }

        var className = getClassNameFromMetadata(typeHnd, out var namespaceName);
        if (namespaceName is not "System.Runtime.InteropServices.Swift")
        {
            return false;
        }

        if (className is "SwiftSelf")
        {
            if (argIsByrefOrPtr)
            {
                BADCODE("Expected SwiftSelf struct, got pointer/reference");
            }

            if (lvaSwiftSelfArg != BAD_VAR_NUM)
            {
                BADCODE("Duplicate SwiftSelf parameter");
            }

            lvaSwiftSelfArg = lclNum;
            return true;
        }

        if (className is "SwiftIndirectResult")
        {
            if (argIsByrefOrPtr)
            {
                BADCODE("Expected SwiftIndirectResult struct, got pointer/reference");
            }

            if (info.compRetType != TYP_VOID)
            {
                BADCODE("Functions with SwiftIndirectResult parameters must return void");
            }

            if (lvaSwiftIndirectResultArg != BAD_VAR_NUM)
            {
                BADCODE("Duplicate SwiftIndirectResult parameter");
            }

            lvaSwiftIndirectResultArg = lclNum;
            return true;
        }

        if (className is "SwiftError")
        {
            if (!argIsByrefOrPtr)
            {
                BADCODE("Expected SwiftError pointer/reference, got struct");
            }

            if (lvaSwiftErrorArg != BAD_VAR_NUM)
            {
                BADCODE("Duplicate SwiftError* parameter");
            }

            lvaSwiftErrorArg = lclNum;
            lvaSwiftErrorLocal = lvaGrabTempWithImplicitUse(false, "SwiftError pseudolocal");
            lvaSetStruct(lvaSwiftErrorLocal, typeHnd, false);
            return true;
        }

        return false;
    }

    private void impAppendSwiftErrorStore(GenTree swiftErrorNode)
    {
        assert(swiftErrorNode is not null);

        var errorRegNode = new GenTree(GT_SWIFT_ERROR, TYP_I_IMPL) {
            HasOrderingSideEffect = true
        };
        errorRegNode.Flags |= GTF_CALL | GTF_GLOB_REF;

        var swiftErrorStore = gtNewStoreIndNode(swiftErrorNode.Type, swiftErrorNode, errorRegNode);
        _ = impAppendTree(swiftErrorStore, CHECK_SPILL_ALL, impCurStmtDI, checkConsumedDebugInfo: false);
    }
#endif
}
