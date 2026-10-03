// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private void unwindGetFuncLocations(
        in FuncInfoDsc func, bool getHotSectionData, out emitLocation? startLoc, out emitLocation? endLoc)
    {
        if (func.funKind == FuncKind.FUNC_ROOT)
        {
            if (getHotSectionData)
            {
                // Null locations represent the beginning/end of the code without exposing the prolog IG.
                startLoc = null;

#if DEBUG
                if ((fgFirstColdBlock is not null) && (JitConfig.JitFakeProcedureSplitting != 0))
                {
                    endLoc = UnwindBlockLocation(fgFirstFuncletBB);
                    return;
                }
#endif

                endLoc = UnwindBlockLocation(fgFirstColdBlock ?? fgFirstFuncletBB);
            }
            else
            {
                assert(fgFirstColdBlock is not null);
                startLoc = UnwindBlockLocation(fgFirstColdBlock);
                endLoc = UnwindBlockLocation(fgFirstFuncletBB);
            }
        }
        else
        {
            var startBlock = func.GetStartBlock(this);
            var lastBlock = func.GetLastBlock(this);
            startLoc = UnwindBlockLocation(startBlock);
            endLoc = lastBlock.IsLast ? null : UnwindBlockLocation(lastBlock.Next);
        }
    }

    private emitLocation? UnwindBlockLocation(BasicBlock? block)
    {
        if (block is null)
        {
            return null;
        }

        return new emitLocation(ehEmitCookie(block));
    }

    private uint unwindGetCurrentOffset(in FuncInfoDsc func)
    {
        var emitter = UnwindEmitter();
        assert(emitter.emitGeneratingPrologOrFuncletProlog());

        var location = func.startLoc;
        var group = location?.GetIG();
        assert(group is null || location.GetValueOrDefault().GetInsOffset() == 0);
        var offset = emitter.emitGetCurrentCodeOffsetFrom(group);

        return offset;
    }

    private Emitter UnwindEmitter()
    {
        noway_assert(codeGen is not null);

        return codeGen.Emitter;
    }
}
