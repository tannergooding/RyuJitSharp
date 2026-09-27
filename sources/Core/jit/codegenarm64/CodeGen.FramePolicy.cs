// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool genSaveFpLrWithAllCalleeSavedRegisters;
    internal bool genForceFuncletFrameType5;

    public bool IsSaveFpLrWithAllCalleeSavedRegisters => genSaveFpLrWithAllCalleeSavedRegisters;

    public void SetSaveFpLrWithAllCalleeSavedRegisters(bool value)
    {
        JITDUMP($"Setting genSaveFpLrWithAllCalleeSavedRegisters to {dspBool(value)}\n");
        genSaveFpLrWithAllCalleeSavedRegisters = value;

        if (genSaveFpLrWithAllCalleeSavedRegisters)
        {
            // Frame type 5 needs a large outgoing argument area and is otherwise rare.
            // Its nonempty-area requirement is checked when selecting the frame type.
            if ((_compiler.opts.compJitSaveFpLrWithCalleeSavedRegisters == 3) ||
                _compiler.compStressCompile(Compiler.STRESS_GENERIC_VARN, 50))
            {
                genForceFuncletFrameType5 = true;
            }
        }
    }
}
#endif
