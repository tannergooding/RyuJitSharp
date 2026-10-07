// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genStackPointerConstantAdjustment(nint spDelta, regNumber regTmp)
    {
        assert(spDelta < 0);
        assert(unchecked((nuint)(-spDelta)) <= _compiler.eeGetPageSize());

        _ = genInstrWithConstant(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_SPBASE,
            unchecked(-spDelta), regTmp, INS_FLAGS_DONT_CARE);
    }

    private void genStackPointerConstantAdjustmentWithProbe(nint spDelta, regNumber regTmp)
    {
        Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, regTmp, REG_SP, 0, INS_FLAGS_DONT_CARE);
        genStackPointerConstantAdjustment(spDelta, regTmp);
    }

    private nint genStackPointerConstantAdjustmentLoopWithProbe(nint spDelta, regNumber regTmp)
    {
        unchecked
        {
            assert(spDelta < 0);
            var pageSize = _compiler.eeGetPageSize();
            var remaining = spDelta;

            do
            {
                var oneDelta = -(nint)nuint.Min((nuint)(-remaining), pageSize);
                genStackPointerConstantAdjustmentWithProbe(oneDelta, regTmp);
                remaining -= oneDelta;
            }
            while (remaining < 0);

            var lastTouchDelta = (nuint)(-spDelta) % pageSize;
            if ((lastTouchDelta == 0) ||
                (lastTouchDelta + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES > pageSize))
            {
                Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, regTmp, REG_SP, 0, INS_FLAGS_DONT_CARE);
                lastTouchDelta = 0;
            }

            return (nint)lastTouchDelta;
        }
    }
}
#endif
