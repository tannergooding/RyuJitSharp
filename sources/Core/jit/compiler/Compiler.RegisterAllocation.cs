// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal void raMarkStkVars()
    {
        if (!Configuration.TARGET_AMD64 && !Configuration.TARGET_ARM64 && !Configuration.TARGET_RISCV64 &&
            !Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_WASM)
        {
            NYI("Compiler.raMarkStkVars outside AMD64/ARM64");
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.CompilerRaMarkStkVarsOutsideAMD64ARM64);
            return;
        }

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
                // Debug lifetime extension gives every in-scope local a reference.
                if (Configuration.DEBUG && opts.compDbgCode && !local.lvIsParam && local.lvTracked)
                {
                    for (var scopeNumber = 0; scopeNumber < info.compVarScopesCount; scopeNumber++)
                    {
                        noway_assert(info.compVarScopes[scopeNumber].vsdVarNum != localNumber);
                    }
                }
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
            noway_assert((Configuration.FEATURE_FIXED_OUT_ARGS && (localNumber == lvaOutgoingArgSpaceVar)) ||
                varTypeHasUnknownSize(local.Type) ||
                (lvaLclStackHomeSize(localNumber) != 0));
            local.lvOnFrame = true;

        NOT_STK:
            local.lvFramePointerBased = codeGen.IsFramePointerUsed;

            noway_assert(local.lvIsInReg || local.lvOnFrame || (local.lvRefCnt() == 0));
            noway_assert(!local.lvRegister || !local.lvOnFrame);
            if (Configuration.DEBUG && local.lvIsParam && lvaIsArgAccessedViaVarArgsCookie(localNumber))
            {
                if (!local.lvPromoted && !local.lvIsStructField)
                {
                    noway_assert((local.lvRefCnt() == 0) && !local.lvRegister && !local.lvOnFrame);
                }
            }
        }
    }
}
