// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private static regNumber genGetThisArgReg(GenTreeCall call) => REG_ARG_0;

    public static unsafe GenTree? getCallTarget(GenTreeCall call, out CORINFO_METHOD_HANDLE methHnd)
    {
        methHnd = call._callType != CT_INDIRECT ? call._callMethHnd : NO_METHOD_HANDLE;
        assert((call._callType != CT_INDIRECT) || (call.ControlExpr is not null));

        return call.ControlExpr;
    }

    public regNumber getCallIndirectionCellReg(GenTreeCall call)
    {
        var result = REG_NA;
        switch (call.IndirectionCellArgKind)
        {
            case WellKnownArg.None:
            {
                break;
            }

            case WellKnownArg.R2RIndirectionCell:
            {
                result = REG_R2R_INDIRECT_PARAM;
                break;
            }

            case WellKnownArg.VirtualStubCell:
            {
                assert(_compiler.virtualStubParamInfo is not null);
                result = _compiler.virtualStubParamInfo.Reg;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

#if DEBUG
        if (call.IndirectionCellArgKind != WellKnownArg.None)
        {
            var indirCellArg = call.Args.FindWellKnownArg(call.IndirectionCellArgKind);
            assert(indirCellArg is not null);
            assert(indirCellArg.AbiInfo.HasExactlyOneRegisterSegment);
            assert(indirCellArg.AbiInfo.Segments[0].Register == result);
        }
#endif
        return result;
    }

#if DEBUG
    public void genCheckTailCallEpilogRegisters(GenTreeCall call)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm tailcall epilog-register verification is not ported.");
#else
        if (!call.IsFastTailCall)
        {
            return;
        }

#if HAS_MORE_THAN_64_REGISTERS && TARGET_XARCH
        var trashedByEpilog = new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED,
            SRBM_MSK_CALLEE_SAVED);
#elif TARGET_XARCH
        var trashedByEpilog = new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED | SRBM_MSK_CALLEE_SAVED);
#else
        var trashedByEpilog = new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED);
#endif
        if (_compiler.NeedsGSSecurityCookie)
        {
            trashedByEpilog |= genGetGSCookieTempRegs(tailCall: true, call);
        }

        foreach (var arg in call.Args.Args)
        {
            foreach (ref readonly var segment in arg.AbiInfo.Segments)
            {
                if (!segment.IsPassedInRegister)
                {
                    continue;
                }

#if TARGET_ARM
                var segmentMask = new regMaskTP(unchecked((regMask)((ulong)segment.RegisterMask << segment.RegisterMaskBase)));
#else
                var segmentMask = regMaskTP.CreateFromRegNum(segment.Register, segment.Register.SingleTypeMask);
#endif
                if ((trashedByEpilog & segmentMask).IsNonEmpty)
                {
                    if (_compiler.verbose)
                    {
                        jitprintf("Tail call node:\n");
                        _compiler.gtDispTree(call);
                        jitprintf($"Register used: {segment.Register.Name}\n");
                    }
                    assert(false, "Argument to tailcall may be trashed by epilog");
                }
            }
        }
#endif
    }
#endif
}
