// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe bool fgCanFastTailCall(GenTreeCall callee, out string? failReason)
    {
#if FEATURE_FASTTAILCALL
#if DEBUG
        if (callee.IsTailPrefixedCall)
        {
            assert(impTailCallRetTypeCompatible(false, info.compRetType, info.compMethodInfo->args.retTypeClass,
                info.compCallConv, callee._returnType, callee._retClsHnd, callee.UnmanagedCallConv));
        }
#endif
        assert(!callee.Args.AreArgsComplete);
        callee.Args.AddFinalArgsAndDetermineAbiInfo(this, callee);

        var calleeArgStackSize = callee.Args.OutgoingArgsStackSize;
        var callerArgStackSize = roundUp(lvaParameterStackSize, TARGET_POINTER_SIZE);

#if !DEBUG
        static
#endif
        void ReportFastTailCallDecision(string? reason, out string? result)
        {
            result = reason;
#if DEBUG
            if (JitConfig.JitReportFastTailCallDecisions == 1)
            {
                var methodName = callee._callType is CT_INDIRECT ? "IndirectCall" : eeGetMethodFullName(callee._callMethHnd);
                jitprintf($"[Fast tailcall decision]: Caller: {info.compFullName}\n" +
                    $"[Fast tailcall decision]: Callee: {methodName} -- Decision: ");
                if (reason is null)
                {
                    jitprintf("Will fast tailcall");
                }
                else
                {
                    jitprintf($"Will not fast tailcall ({reason})");
                }
                jitprintf($" (CallerArgStackSize: {callerArgStackSize}, CalleeArgStackSize: {calleeArgStackSize})\n\n");
            }
            else if (reason is null)
            {
                JITDUMP("[Fast tailcall decision]: Will fast tailcall\n");
            }
            else
            {
                JITDUMP($"[Fast tailcall decision]: Will not fast tailcall ({reason})\n");
            }
#endif
        }

#if TARGET_ARM || TARGET_RISCV64 || TARGET_LOONGARCH64
        foreach (var argument in callee.Args.Args)
        {
            if (argument.AbiInfo.IsSplitAcrossRegistersAndStack)
            {
                ReportFastTailCallDecision("Argument splitting in callee is not supported on " + TARGET_READABLE_NAME,
                    out failReason);
                return false;
            }
        }

        for (var localNumber = 0; localNumber < info.compArgsCount; localNumber++)
        {
            var abiInfo = lvaGetParameterABIInfo(localNumber);
            if (abiInfo.IsSplitAcrossRegistersAndStack)
            {
                ReportFastTailCallDecision("Argument splitting in caller is not supported on " + TARGET_READABLE_NAME,
                    out failReason);
                return false;
            }
        }
#endif
#if TARGET_ARM
        if (compIsProfilerHookNeeded)
        {
            ReportFastTailCallDecision("Profiler is not supported on ARM32", out failReason);
            return false;
        }

        // ARM32's only non-parameter volatile register is needed for the cookie check.
        if (NeedsGSSecurityCookie)
        {
            ReportFastTailCallDecision("Not enough registers available due to the GS security cookie check", out failReason);
            return false;
        }
#endif
        if (!opts.compFastTailCalls)
        {
            ReportFastTailCallDecision("Configuration doesn't allow fast tail calls", out failReason);
            return false;
        }

        if (callee.IsStressTailCall)
        {
            ReportFastTailCallDecision("Fast tail calls are not performed under tail call stress", out failReason);
            return false;
        }

#if TARGET_ARM
        if (callee.IsR2RRelativeIndir || callee.HasNonStandardAddedArgs(this))
        {
            ReportFastTailCallDecision("Method with non-standard args passed in callee saved register cannot be tail called",
                out failReason);
            return false;
        }
#endif
        // A vararg caller guarantees space for its fixed arguments, but Windows ARM
        // varargs still require argument shuffling that fast tail calls do not support.
        if (TargetOS.IsWindows && TargetArchitecture.IsArmArch && (info.compIsVarArgs || callee.Args.IsVarArgs))
        {
            ReportFastTailCallDecision("Fast tail calls with varargs not supported on Windows ARM/ARM64", out failReason);
            return false;
        }

        if (compLocallocUsed)
        {
            ReportFastTailCallDecision("Localloc used", out failReason);
            return false;
        }

        if (info.compHasNextCallRetAddr)
        {
            ReportFastTailCallDecision("Uses NextCallReturnAddress intrinsic", out failReason);
            return false;
        }

        if (callee.Args.HasRetBuffer && (info.compRetBuffArg == BAD_VAR_NUM))
        {
            ReportFastTailCallDecision("Callee has RetBuf but caller does not.", out failReason);
            return false;
        }

        // Fast tail calls reuse the incoming argument area. Its GC shape need not
        // match because argument setup is non-interruptible. Wasm uses fresh locals.
#if !TARGET_WASM
        if (calleeArgStackSize > callerArgStackSize)
        {
            ReportFastTailCallDecision("Not enough incoming arg space", out failReason);
            return false;
        }
#endif
        if (fgCallHasMustCopyByrefParameter(callee))
        {
            ReportFastTailCallDecision("Callee has a byref parameter", out failReason);
            return false;
        }

        ReportFastTailCallDecision(null, out failReason);
        return true;
#else
        failReason = "Fast tailcalls are not supported on this platform";
        return false;
#endif
    }

#if FEATURE_FASTTAILCALL
    public bool fgCallHasMustCopyByrefParameter(GenTreeCall call)
    {
#if FEATURE_IMPLICIT_BYREFS
        foreach (var argument in call.Args.Args)
        {
            if (fgCallArgWillPointIntoLocalFrame(call, argument))
            {
                return true;
            }
        }
#endif
        return false;
    }

    public bool fgCallArgWillPointIntoLocalFrame(GenTreeCall call, CallArg argument)
    {
        if (!argument.AbiInfo.IsPassedByReference)
        {
            return false;
        }

        if (opts.OptimizationDisabled)
        {
            return true;
        }

        var local = argument.Node.IsImplicitByrefParameterValuePreMorph(this);
        if (local is null)
        {
            return true;
        }

        var localNumber = local.LclNum;
        ref var variable = ref lvaGetDesc(localNumber);
        if (variable.lvPromoted)
        {
#if DEBUG
            JITDUMP($"Arg [{argument.Node.TreeId:D6}] is promoted implicit byref V{localNumber:D2}, so no tail call\n");
#endif
            return true;
        }

        assert(!variable.lvIsStructField);
#if DEBUG
        JITDUMP($"Arg [{argument.Node.TreeId:D6}] is unpromoted implicit byref V{localNumber:D2}, seeing if we can still tail call\n");
#endif
        // Undone promotion keeps last-use bits for the original fields.
        var deathFlags = variable.lvFieldLclStart != 0
            ? lvaGetDesc(variable.lvFieldLclStart).AllFieldDeathFlags
            : GTF_VAR_DEATH;

        if ((local.Flags & deathFlags) == deathFlags)
        {
            JITDUMP("... yes, arg is a last use\n");
            return false;
        }

        JITDUMP("... no, arg is not a last use\n");
        return true;
    }
#endif
}
