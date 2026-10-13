// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genStackPointerConstantAdjustmentArm(nint spDelta, regNumber regTmp)
    {
        assert(spDelta < 0);
        assert(unchecked((nuint)(-spDelta)) <= _compiler.eeGetPageSize());

        _ = genInstrWithConstantArm(INS_sub, EA_PTRSIZE, REG_SPBASE_ARM, REG_SPBASE_ARM,
            unchecked(-spDelta), regTmp, INS_FLAGS_DONT_CARE);
    }

    private void genStackPointerConstantAdjustmentWithProbeArm(nint spDelta, regNumber regTmp)
    {
        Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, regTmp, REG_ARM32_SP, 0, INS_FLAGS_DONT_CARE);
        genStackPointerConstantAdjustmentArm(spDelta, regTmp);
    }

    private nint genStackPointerConstantAdjustmentLoopWithProbeArm(nint spDelta, regNumber regTmp)
    {
        unchecked
        {
            assert(spDelta < 0);
            var pageSize = _compiler.eeGetPageSize();
            var remaining = spDelta;

            do
            {
                var oneDelta = -(nint)nuint.Min((nuint)(-remaining), pageSize);
                genStackPointerConstantAdjustmentWithProbeArm(oneDelta, regTmp);
                remaining -= oneDelta;
            }
            while (remaining < 0);

            var lastTouchDelta = (nuint)(-spDelta) % pageSize;
            if ((lastTouchDelta == 0) ||
                (lastTouchDelta + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_ARM > pageSize))
            {
                Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, regTmp, REG_ARM32_SP, 0, INS_FLAGS_DONT_CARE);
                lastTouchDelta = 0;
            }

            return (nint)lastTouchDelta;
        }
    }
}
