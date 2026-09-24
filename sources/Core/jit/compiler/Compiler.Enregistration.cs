// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool IsEHVarARegCandidate(in LclVarDsc descriptor)
    {
        return lvaEnregEHVars && descriptor.lvSingleDefRegCandidate && (descriptor.lvRefCnt() > 1);
    }

    public void lvSetVarsDoNotEnreg()
    {
        for (var localNumber = 0; localNumber < lvaCount; localNumber++)
        {
            ref var descriptor = ref lvaGetDesc(localNumber);
            if (descriptor.lvDoNotEnregister)
            {
                continue;
            }

            if (descriptor.lvTracked && descriptor.IsLiveInOutOfHandler && !IsEHVarARegCandidate(in descriptor))
            {
                lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LiveInOutOfHandler);
            }
            else if (varTypeIsStruct(descriptor.Type) && !descriptor.lvPromoted && !descriptor.IsEnregisterableType)
            {
                lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.NotRegSizeStruct);
            }
        }
    }
}
