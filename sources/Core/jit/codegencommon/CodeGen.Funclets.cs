// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genUpdateCurrentFunclet(BasicBlock block)
    {
        assert(_compiler.bbIsFuncletBeg(block));
        _compiler.funSetCurrentFunc(_compiler.funGetFuncIdx(block));
        ref var current = ref _compiler.funCurrentFunc();
        assert(current.funKind != FuncKind.FUNC_ROOT);
        assert(current.GetStartBlock(_compiler) == block);
    }
}
