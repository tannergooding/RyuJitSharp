// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.CorInfoOptions;
using static RyuJitSharp.GENERIC_CONTEXTPARAM_TYPE;

namespace RyuJitSharp;

public partial struct GCInfo
{
    public readonly unsafe void gcInfoBlockHdrSave(GcInfoEncoder gcInfoEncoder, uint methodSize, uint prologSize)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI || JIT32_GCENCODER
        throw new FatalJitException(CORJIT_SKIPPED, "Modern GC encoding requires Windows AMD64.");
#else
        var compiler = Compiler;
#if DEBUG
        if (compiler.verbose)
        {
            jitprintf("*************** In gcInfoBlockHdrSave()\n");
        }
#endif
        var encoder = new GcInfoEncoderWithLogging(gcInfoEncoder, compiler);
        encoder.SetCodeLength(methodSize);

        if (_codeGen.IsFramePointerUsed)
        {
            encoder.SetStackBaseRegister((uint)REG_FPBASE);
        }
        if (compiler.info.compIsVarArgs)
        {
            encoder.SetIsVarArg();
        }
        if (compiler.lvaReportParamTypeArg())
        {
            assert(compiler.info.compTypeCtxtArg != BAD_VAR_NUM);
            var type = compiler.info.compMethodInfo->options & CORINFO_GENERICS_CTXT_MASK;
            var contextType = type switch
            {
                CORINFO_GENERICS_CTXT_FROM_METHODDESC => GENERIC_CONTEXTPARAM_MD,
                CORINFO_GENERICS_CTXT_FROM_METHODTABLE => GENERIC_CONTEXTPARAM_MT,
                _ => throw new FatalJitException(CORJIT_SKIPPED, "Unknown generic context parameter type."),
            };

            var offset = compiler.lvaCachedGenericContextArgOffset();
#if DEBUG
            if (compiler.opts.IsOSR)
            {
                var callerSpOffset = compiler.lvaToCallerSPRelativeOffset(offset, _codeGen.IsFramePointerUsed);
                var patchpoint = compiler.info.compPatchpointInfo;
                assert(patchpoint is not null);
                assert(callerSpOffset == patchpoint->GenericContextArgOffset - (2 * REGSIZE_BYTES));
            }
#endif
            encoder.SetGenericsInstContextStackSlot(offset, contextType);
        }
        else if (compiler.lvaKeepAliveAndReportThis())
        {
            assert(compiler.info.compThisArg != BAD_VAR_NUM);
            var offset = compiler.lvaCachedGenericContextArgOffset();
#if DEBUG
            if (compiler.opts.IsOSR && compiler.info.compPatchpointInfo->HasKeptAliveThis)
            {
                var callerSpOffset = compiler.lvaToCallerSPRelativeOffset(offset, _codeGen.IsFramePointerUsed, true);
                assert(callerSpOffset == compiler.info.compPatchpointInfo->KeptAliveThisOffset -
                    (2 * REGSIZE_BYTES));
            }
#endif
            encoder.SetGenericsInstContextStackSlot(offset, GENERIC_CONTEXTPARAM_THIS);
        }

        if (compiler.NeedsGSSecurityCookie)
        {
            assert(compiler.lvaGSSecurityCookie != BAD_VAR_NUM);
            ref var cookie = ref compiler.lvaGetDesc(compiler.lvaGSSecurityCookie);
            var offset = compiler.lvaToCallerSPRelativeOffset(cookie.StackOffset, cookie.lvFramePointerBased);
            encoder.SetGSCookieStackSlot(offset, prologSize, methodSize);
        }
        else if (compiler.lvaReportParamTypeArg() || compiler.lvaKeepAliveAndReportThis())
        {
            encoder.SetPrologSize(prologSize);
        }

        if (compiler.compHndBBtabCount > 0)
        {
            encoder.SetWantsReportOnlyLeaf();
        }

        encoder.SetSizeOfStackOutgoingAndScratchArea(unchecked((uint)compiler.lvaOutgoingArgSpaceSize.Value));
#endif
    }
}
