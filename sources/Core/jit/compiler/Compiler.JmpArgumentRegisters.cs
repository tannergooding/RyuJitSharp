// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public regNumber getCallArgFloatRegister(regNumber intReg)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Varargs float register mapping requires Windows AMD64.");
#else
        assert(compFeatureVarArg());
        switch (intReg)
        {
            case REG_RCX:
            {
                return REG_XMM0;
            }

            case REG_RDX:
            {
                return REG_XMM1;
            }

            case REG_R8:
            {
                return REG_XMM2;
            }

            case REG_R9:
            {
                return REG_XMM3;
            }

            default:
            {
                unreached();
                return REG_NA;
            }
        }
#endif
    }
}
