// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public regNumber getCallArgIntRegister(regNumber floatReg)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Varargs register mapping requires Windows AMD64.");
#else
        assert(compFeatureVarArg());
        switch (floatReg)
        {
            case REG_XMM0:
            {
                return REG_RCX;
            }

            case REG_XMM1:
            {
                return REG_RDX;
            }

            case REG_XMM2:
            {
                return REG_R8;
            }

            case REG_XMM3:
            {
                return REG_R9;
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
