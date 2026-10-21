// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public regNumber getCallArgFloatRegister(regNumber intReg)
    {
        if (!Configuration.TARGET_AMD64 || !Configuration.WINDOWS_AMD64_ABI)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Varargs float register mapping requires Windows AMD64.");
            return default!;
        }

        assert(compFeatureVarArg());
        switch (intReg)
        {
            case REG_X64_RCX:
            {
                return REG_X64_XMM0;
            }

            case REG_X64_RDX:
            {
                return REG_X64_XMM1;
            }

            case REG_X64_R8:
            {
                return REG_X64_XMM2;
            }

            case REG_X64_R9:
            {
                return REG_X64_XMM3;
            }

            default:
            {
                unreached();
                return REG_X64_NA;
            }
        }
    }
}
