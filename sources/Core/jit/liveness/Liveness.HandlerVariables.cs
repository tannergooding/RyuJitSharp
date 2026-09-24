// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    internal void MarkMustInitAndEHVars(ReadOnlySpan<nint> finallyVars, ReadOnlySpan<nint> exceptVars)
    {
        for (var local = 0; local < _compiler.lvaCount; local++)
        {
            ref var descriptor = ref _compiler.lvaGetDesc(local);
            descriptor._lvLiveInOutOfHandler = false;
            descriptor.lvMustInit = false;
            if (!descriptor.lvTracked)
            {
                continue;
            }

            var dependentField = _compiler.lvaIsFieldOfDependentlyPromotedStruct(in descriptor);
            assert(_compiler.fgFirstBB is not null);
            if (!descriptor.lvIsParam && !descriptor.lvIsParamRegTarget &&
                VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, descriptor._varIndex) &&
                (_compiler.info.compInitMem || varTypeIsGC(descriptor.Type)) && !dependentField)
            {
                descriptor.lvMustInit = true;
            }

            var isFinallyVar = VarSetOps.IsMember(_compiler, finallyVars, descriptor._varIndex);
            if (isFinallyVar || VarSetOps.IsMember(_compiler, exceptVars, descriptor._varIndex))
            {
                descriptor._lvLiveInOutOfHandler = true;
                assert(!descriptor.lvPromoted);

                // GC locals live out of finally require initialization, even fields
                // whose dependent parent otherwise supplies entry initialization.
                if (isFinallyVar && !descriptor.lvIsParam && !descriptor.lvIsParamRegTarget && varTypeIsGC(descriptor.Type))
                {
                    descriptor.lvMustInit = true;
                }
            }
        }
    }
}
