// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void impTryOptimizeAwaitAwaiter(GenTreeCall call, in CORINFO_RESOLVED_TOKEN resolvedToken,
        ref CORINFO_CALL_INFO callInfo, ref CORINFO_METHOD_HANDLE method, ref CORINFO_CONTEXT_HANDLE exactContext,
        ref GenTree? instantiationArgument, NamedIntrinsic intrinsic)
    {
        var awaiterArgument = call.Args.GetUserArgByIndex(0);
        assert(awaiterArgument is not null);
        if (!varTypeIsStruct(awaiterArgument.SignatureType))
        {
            return;
        }

        if (info.compCompHnd->isIntrinsicType(awaiterArgument.SignatureClassHandle))
        {
            // Nested YieldAwaiter may have no reported namespace; intrinsic type identity is sufficient.
            var className = info.compCompHnd->getClassNameFromMetadata(awaiterArgument.SignatureClassHandle, null);
            if (MemoryMarshal.CreateReadOnlySpanFromNullTerminated(className).SequenceEqual("YieldAwaiter"u8))
            {
                // UnsafeAwaitAwaiter specially recognizes YieldAwaiter for more than avoiding a box.
                JITDUMP("Skipping custom awaiter optimization for YieldAwaiter\n");
                return;
            }
        }

#if DEBUG
        JITDUMP($"Optimizing awaiter call [{call.TreeId:D6}] to read its struct awaiter from the continuation\n");
#endif
        CORINFO_LOOKUP newInstantiationLookup;
        CORINFO_CONTEXT_HANDLE newExactContext;
        CORINFO_METHOD_HANDLE newMethod;
        var isUnsafe = intrinsic is NI_System_Runtime_CompilerServices_AsyncHelpers_UnsafeAwaitAwaiter;
        fixed (CORINFO_RESOLVED_TOKEN* token = &resolvedToken)
        {
            newMethod = info.compCompHnd->getAwaitAwaiterInContinuationCall(info.compMethodHnd, token,
                isUnsafe, &newExactContext, &newInstantiationLookup);
        }

        if (newMethod is null)
        {
            JITDUMP("EE returned no method to call; bailing on optimization\n");
            return;
        }

        CORINFO_SIG_INFO newSignature;
        info.compCompHnd->getMethodSig(newMethod, &newSignature);
        GenTree? newInstantiationArgument = null;
        if (newSignature.hasTypeArg())
        {
            newInstantiationArgument = impLookupToTree(newInstantiationLookup, GTF_ICON_METHOD_HDL, newMethod);
            if (newInstantiationArgument is null)
            {
#if DEBUG
                JITDUMP($"Failed to optimize awaiter call [{call.TreeId:D6}] because its replacement lookup could not be created\n");
#endif
                return;
            }
        }

#if FEATURE_READYTORUN
        if (IsAot)
        {
            CORINFO_CONST_LOOKUP entryPoint;
            info.compCompHnd->getFunctionEntryPoint(newMethod, &entryPoint);
            call._entryPoint = entryPoint;
        }
#endif
        method = newMethod;
        exactContext = newExactContext;
        call._callMethHnd = newMethod;
        callInfo.hMethod = newMethod;
        callInfo.methodFlags = info.compCompHnd->getMethodAttribs(newMethod);
        callInfo.sig = newSignature;
        instantiationArgument = newInstantiationArgument;

        var awaiter = awaiterArgument.Node;
        var awaiterType = awaiterArgument.SignatureType;
        var awaiterLayout = awaiterArgument.SignatureLayout;
        assert(awaiterLayout is not null);
        call.Args.Remove(awaiterArgument);
        _ = call.Args.PushFront(NewCallArg.CreateForStruct(awaiter, awaiterType, awaiterLayout)
            .WithWellKnownArg(WellKnownArg.AsyncAwaiter));

        var memberIndex = GetContinuationMemberIndex(ContinuationMember.CustomAwaiterOfLayout(awaiterLayout));
        var offset = new GenTreeVal(GT_CONTINUATION_MEMBER_OFFSET, TYP_INT, memberIndex);
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(offset));
    }
}
