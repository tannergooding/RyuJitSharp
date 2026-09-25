// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public uint emitCurOffset()
    {
        return emitSpecifiedOffset(unchecked((uint)emitCurIGinsCnt), unchecked((uint)emitCurIGsize));
    }

    public static uint emitSpecifiedOffset(uint insCount, uint igSize)
    {
        // emit.h packs the instruction index and its estimated byte offset separately:
        // final encoding can change instruction sizes without invalidating the index.
        var codePos = unchecked(insCount + (igSize << 16));
        assert(emitGetInsNumFromCodePos(codePos) == insCount);
        assert(emitGetInsOfsFromCodePos(codePos) == igSize);

        return codePos;
    }

    public uint emitCodeOffset(insGroup ig, uint codePos)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Final emitter-location offsets outside AMD64 are not implemented.");
#else
        var number = emitGetInsNumFromCodePos(codePos);
#if DEBUG
        assert(ig.igSelf == ig);
#endif
        uint offset;

        if (number == 0)
        {
            offset = 0;
        }
        else if (number == ig.igInsCnt)
        {
            offset = ig.igSize;
        }
        else if ((ig.igFlags & InsGroupFlags.UpdatedInstructionSize) != 0)
        {
            offset = emitFindOffset(ig, number);
        }
        else
        {
            offset = emitGetInsOfsFromCodePos(codePos);
            assert(offset == emitFindOffset(ig, number));
        }

        return unchecked(ig.igOffs + offset);
#endif
    }

    private static uint emitFindOffset(insGroup ig, uint insNum)
    {
#if DEBUG
        assert(ig.igSelf == ig);
        assert(ig.igInsCnt >= insNum);
#endif
        var offset = 0u;

        for (var index = 0u; index < insNum; index++)
        {
            assert(ig.igData is not null);
            offset = unchecked(offset + ig.igData[(int)index].idCodeSize());
        }

        return offset;
    }
}
