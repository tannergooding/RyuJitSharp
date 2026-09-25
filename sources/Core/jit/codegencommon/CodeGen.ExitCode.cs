// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genExitCode(BasicBlock block)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI || EMITTER_STATS
        throw new FatalJitException(CORJIT_SKIPPED, "Method exit generation requires Windows AMD64 without emitter allocation statistics.");
#else
        Emitter.RequireSupportedInstructionRecording();
        // Epilog mappings deliberately allow duplicate locations.
        genIPmappingAdd(IPmappingDscKind.Epilog, default, true);

#if EMIT_GENERATE_GCINFO && DEBUG
        if (!block.HasFlag(BBF_HAS_JMP))
        {
            if (_compiler.compMethodReturnsRetBufAddr)
            {
                assert((GCInfo.gcRegByrefSetCur & new regMaskTP(SRBM_INTRET)) != RBM_NONE);
            }
            else
            {
                ref readonly var retTypeDesc = ref _compiler.compRetTypeDesc;
                var regCount = retTypeDesc.ReturnRegCount;
                for (byte i = 0; i < regCount; i++)
                {
                    var type = retTypeDesc.GetReturnRegType(i);
                    var reg = retTypeDesc.GetAbiReturnReg(i, _compiler.info.compCallConv);
                    var mask = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
                    assert((type == TYP_BYREF) == ((GCInfo.gcRegByrefSetCur & mask) != RBM_NONE));
                    assert((type == TYP_REF) == ((GCInfo.gcRegGCrefSetCur & mask) != RBM_NONE));
                }
            }
        }
#endif

        if (_compiler.NeedsGSSecurityCookie)
        {
            genEmitGSCookieCheck(block.HasFlag(BBF_HAS_JMP));
        }

        genReserveEpilog(block);
#endif
    }
}
