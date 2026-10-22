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
        if (!Configuration.TARGET_AMD64 || !Configuration.WINDOWS_AMD64_ABI ||
            Configuration.JIT32_GCENCODER)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.ModernGCEncodingRequiresWindowsAMD64);
            return;
        }

        var compiler = Compiler;
        if (Configuration.DEBUG && compiler.verbose)
        {
            jitprintf("*************** In gcInfoBlockHdrSave()\n");
        }
        var encoder = new GcInfoEncoderWithLogging(gcInfoEncoder, compiler);
        encoder.SetCodeLength(methodSize);

        if (_codeGen.IsFramePointerUsed)
        {
            encoder.SetStackBaseRegister((uint)TargetRuntimeMetadata.FramePointerBaseRegister);
        }
        if (compiler.info.compIsVarArgs)
        {
            encoder.SetIsVarArg();
        }
        if (compiler.lvaReportParamTypeArg())
        {
            assert(compiler.info.compTypeCtxtArg != BAD_VAR_NUM);
            var type = compiler.info.compMethodInfo->options & CORINFO_GENERICS_CTXT_MASK;
            GENERIC_CONTEXTPARAM_TYPE contextType;
            switch (type)
            {
                case CORINFO_GENERICS_CTXT_FROM_METHODDESC:
                {
                    contextType = GENERIC_CONTEXTPARAM_MD;
                    break;
                }

                case CORINFO_GENERICS_CTXT_FROM_METHODTABLE:
                {
                    contextType = GENERIC_CONTEXTPARAM_MT;
                    break;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnknownGenericContextParameterType);
                    return;
                }
            }

            var offset = compiler.lvaCachedGenericContextArgOffset();
            if (Configuration.DEBUG && compiler.opts.IsOSR)
            {
                var callerSpOffset = compiler.lvaToCallerSPRelativeOffset(offset, _codeGen.IsFramePointerUsed);
                var patchpoint = compiler.info.compPatchpointInfo;
                assert(patchpoint is not null);
                assert(callerSpOffset == patchpoint->GenericContextArgOffset - (2 * Configuration.REGSIZE_BYTES));
            }
            encoder.SetGenericsInstContextStackSlot(offset, contextType);
        }
        else if (compiler.lvaKeepAliveAndReportThis())
        {
            assert(compiler.info.compThisArg != BAD_VAR_NUM);
            var offset = compiler.lvaCachedGenericContextArgOffset();
            if (Configuration.DEBUG && compiler.opts.IsOSR && compiler.info.compPatchpointInfo->HasKeptAliveThis)
            {
                var callerSpOffset = compiler.lvaToCallerSPRelativeOffset(offset, _codeGen.IsFramePointerUsed, true);
                assert(callerSpOffset == compiler.info.compPatchpointInfo->KeptAliveThisOffset -
                    (2 * Configuration.REGSIZE_BYTES));
            }
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
        if (Configuration.DISPLAY_SIZES)
        {
            if (_codeGen.Interruptible)
            {
                Compiler.genMethodICnt = unchecked(Compiler.genMethodICnt + 1);
            }
            else
            {
                Compiler.genMethodNCnt = unchecked(Compiler.genMethodNCnt + 1);
            }
        }
    }
}
