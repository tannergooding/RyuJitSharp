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
#if TARGET_AMD64
        return _compiler.lvaToCallerSPRelativeOffset(local.StackOffset, local.lvFramePointerBased) + REGSIZE_BYTES;
#else
        if (doubleAlignOrFramePointerUsed())
        {
            return local.StackOffset - REGSIZE_BYTES;
        }

        return local.StackOffset - genTotalFrameSize;
#endif
    }

    public void psiBegProlog()
    {
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

            var reg1 = REG_NA;
            var reg2 = REG_NA;
            var abiInfo = _compiler.lvaGetParameterAbiInfo(scope.vsdVarNum);
            foreach (var segment in abiInfo.Segments)
            {
                if (!segment.IsPassedInRegister)
                {
                    break;
                }

                if (reg1 == REG_NA)
                {
                    reg1 = segment.Register;
                }
                else
                {
                    reg2 = segment.Register;
                    break;
                }
            }

            // Prolog scopes expose multiple registers only on the SysV x64 ABI.
#if !UNIX_AMD64_ABI
            reg2 = REG_NA;
#endif

            siVarLoc location = default;
            if ((reg1 != REG_NA) && (genIsValidIntReg(reg1) || genIsValidFloatReg(reg1)))
            {
                location.storeVariableInRegisters(reg1, reg2);
            }
            else
            {
                location.storeVariableOnStack(REG_SPBASE, psiGetVarStackOffset(in local));
            }

            getVariableLiveKeeper().psiStartVariableLiveRange(location, scope.vsdVarNum);
        }
    }

    public void psiEndProlog()
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        getVariableLiveKeeper().psiClosePrologVariableRanges();
    }
}
