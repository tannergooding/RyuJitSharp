// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private int psiGetVarStackOffset(in LclVarDsc local)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog stack-variable locations require AMD64.");
#else
        return _compiler.lvaToCallerSPRelativeOffset(local.StackOffset, local.lvFramePointerBased) + REGSIZE_BYTES;
#endif
    }

    public void psiBegProlog()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog variable scopes require Windows AMD64.");
#else
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        _compiler.compResetScopeLists();
        while (true)
        {
            ref var scope = ref _compiler.compGetNextEnterScope(0);
            if (Unsafe.IsNullRef(in scope))
            {
                break;
            }

            ref var local = ref _compiler.lvaGetDesc(scope.vsdVarNum);
            if (!local.lvIsParam)
            {
                continue;
            }

            var reg = REG_NA;
            var abiInfo = _compiler.lvaGetParameterAbiInfo(scope.vsdVarNum);
            foreach (var segment in abiInfo.Segments)
            {
                if (segment.IsPassedInRegister)
                {
                    reg = segment.Register;
                }
                break;
            }

            siVarLoc location = default;
            if ((reg != REG_NA) && (genIsValidIntReg(reg) || genIsValidFloatReg(reg)))
            {
                location.storeVariableInRegisters(reg, REG_NA);
            }
            else
            {
                location.storeVariableOnStack(REG_SPBASE, psiGetVarStackOffset(in local));
            }
            getVariableLiveKeeper().psiStartVariableLiveRange(location, scope.vsdVarNum);
        }
#endif
    }

    public void psiEndProlog()
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        getVariableLiveKeeper().psiClosePrologVariableRanges();
    }
}
