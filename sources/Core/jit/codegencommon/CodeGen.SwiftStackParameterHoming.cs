// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM && SWIFT_SUPPORT
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genHomeSwiftStructStackParameters()
    {
        for (var localNumber = 0; localNumber < _compiler.info.compArgsCount; localNumber++)
        {
            if ((localNumber == _compiler.lvaSwiftSelfArg) || (localNumber == _compiler.lvaSwiftIndirectResultArg))
            {
                continue;
            }

            ref var local = ref _compiler.lvaGetDesc(localNumber);
            if ((local.Type != TYP_STRUCT) || _compiler.lvaIsImplicitByRefLocal(localNumber) || !local.lvOnFrame)
            {
                continue;
            }

            JITDUMP($"Homing Swift parameter stack segments for V{localNumber:D2}: ");
            ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(localNumber);
#if DEBUG
            if (_verbose)
            {
                abiInfo.Dump();
            }
#endif
            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (segment.IsPassedOnStack)
                {
                    genHomeStackSegment(localNumber, in segment, REG_SCRATCH);
                }
            }
        }
    }
}
#endif
