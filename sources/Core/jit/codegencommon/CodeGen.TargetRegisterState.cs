// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class CodeGen
{
#if !HAS_FIXED_REGISTER_SET
    public void SetStackPointerReg(int funcletIndex, regNumber reg)
    {
        assert(funcletIndex < _compiler.compFuncInfoCount);
        assert(reg != REG_NA);
        _compiler.compFuncInfos[funcletIndex].funStackPointerReg = reg;
    }

    public void SetFramePointerReg(int funcletIndex, regNumber reg)
    {
        assert(funcletIndex < _compiler.compFuncInfoCount);
        assert(reg != REG_NA);
        _compiler.compFuncInfos[funcletIndex].funFramePointerReg = reg;
    }
#endif
}
