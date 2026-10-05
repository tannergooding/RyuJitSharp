// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public BasicBlock? genGetThrowHelper(SpecialCodeKind codeKind)
    {
        BasicBlock? exceptionRaisingBlock = null;
        if (_compiler.fgUseThrowHelperBlocks())
        {
            var currentBlock = _compiler.compCurBB;
            assert(currentBlock is not null);

            var add = _compiler.fgGetExcptnTarget(codeKind, currentBlock);
            assert(add is not null);
            assert(add.acdUsed);
            exceptionRaisingBlock = add.acdDstBlk;
#if !FEATURE_FIXED_OUT_ARGS
            assert(add.acdStkLvlInit || IsFramePointerUsed);
#endif

            noway_assert(exceptionRaisingBlock is not null);
        }

        return exceptionRaisingBlock;
    }
}
#endif
