// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal void raMarkStkVars()
    {
#if TARGET_AMD64
        assert(codeGen is not null);
        for (var localNumber = 0; localNumber < lvaCount; localNumber++)
        {
            ref var local = ref lvaGetDesc(localNumber);

            // Dependently promoted fields use their parent's stack home even when unreferenced.
            if (lvaIsFieldOfDependentlyPromotedStruct(in local))
            {
                noway_assert(!local.lvRegister);
                goto ON_STK;
            }

            if (local.lvRegister)
            {
                goto NOT_STK;
            }
            else if (local.lvRefCnt() == 0)
            {
#if DEBUG
                // Debug lifetime extension gives every in-scope local a reference.
                if (opts.compDbgCode && !local.lvIsParam && local.lvTracked)
                {
                    for (var scopeNumber = 0; scopeNumber < info.compVarScopesCount; scopeNumber++)
                    {
                        noway_assert(info.compVarScopes[scopeNumber].vsdVarNum != localNumber);
                    }
                }
#endif
                local.lvOnFrame = false;
                local.lvMustInit = false;
                goto NOT_STK;
            }

            if (!local.lvOnFrame)
            {
                goto NOT_STK;
            }

        ON_STK:
            noway_assert(local.Type is not TYP_UNDEF and not TYP_VOID and not TYP_UNKNOWN);
#if FEATURE_FIXED_OUT_ARGS
            noway_assert((localNumber == lvaOutgoingArgSpaceVar) || varTypeHasUnknownSize(local.Type) ||
                (lvaLclStackHomeSize(localNumber) != 0));
#else
            noway_assert(varTypeHasUnknownSize(local.Type) || (lvaLclStackHomeSize(localNumber) != 0));
#endif
            local.lvOnFrame = true;

        NOT_STK:
            local.lvFramePointerBased = codeGen.IsFramePointerUsed;

            noway_assert(local.lvIsInReg || local.lvOnFrame || (local.lvRefCnt() == 0));
            noway_assert(!local.lvRegister || !local.lvOnFrame);
#if DEBUG
            if (local.lvIsParam && lvaIsArgAccessedViaVarArgsCookie(localNumber))
            {
                if (!local.lvPromoted && !local.lvIsStructField)
                {
                    noway_assert((local.lvRefCnt() == 0) && !local.lvRegister && !local.lvOnFrame);
                }
            }
#endif
        }
#else
        NYI("Compiler.raMarkStkVars outside AMD64");
        throw new FatalJitException("Compiler.raMarkStkVars outside AMD64.");
#endif
    }
}
