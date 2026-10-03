// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if UNIX_AMD64_ABI && FEATURE_SIMD
    public void genClearStackVec3ArgUpperBits()
    {
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genClearStackVec3ArgUpperBits()\n");
        }
#endif
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        for (var varNum = 0; varNum < _compiler.info.compArgsCount; varNum++)
        {
            ref var local = ref _compiler.lvaGetDesc(varNum);
            assert(local.lvIsParam);
            if (local.Type != TYP_SIMD12)
            {
                continue;
            }

            if (!local.lvIsRegArg)
            {
                Emitter.emitIns_S_I(INS_mov, EA_4BYTE, varNum, sizeof(float) * 3, 0);
            }
            else
            {
                ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(varNum);
                assert((abiInfo.NumSegments == 2) && !abiInfo.HasAnyStackSegment);
                genSimd12UpperClear(abiInfo.Segments[1].Register);
            }
        }
    }
#endif
}
