// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class insGroup
{
    private const InsGroupFlags IGF_OUT_OF_ORDER_MASK = InsGroupFlags.Prolog | InsGroupFlags.Epilog
        | InsGroupFlags.FuncletProlog | InsGroupFlags.FuncletEpilog;

    public bool hadAlignInstr()
    {
        return (igFlags & InsGroupFlags.RemovedAlign) != 0;
    }

    public bool IsBefore(insGroup ig)
    {
        assert(ig is not null);

        // Prolog/epilog extensions are allocated later but ordered within their own regions.
        uint positionOfThis;
        if ((igFlags & IGF_OUT_OF_ORDER_MASK) != 0)
        {
            var nextIG = igNext;
            while (true)
            {
                if (nextIG is null)
                {
                    return false;
                }
                if (((nextIG.igFlags & IGF_OUT_OF_ORDER_MASK) == 0)
                    || ((nextIG.igFlags & InsGroupFlags.OutOfOrderHead) != 0))
                {
                    positionOfThis = unchecked(nextIG.igNum - 1);
                    break;
                }
                if (nextIG == ig)
                {
                    return true;
                }

                nextIG = nextIG.igNext;
            }
        }
        else
        {
            positionOfThis = igNum;
        }

        uint positionOfIG;
        if ((ig.igFlags & IGF_OUT_OF_ORDER_MASK) != 0)
        {
            var nextIG = ig.igNext;
            while (true)
            {
                if (nextIG is null)
                {
                    return true;
                }
                if (((nextIG.igFlags & IGF_OUT_OF_ORDER_MASK) == 0)
                    || ((nextIG.igFlags & InsGroupFlags.OutOfOrderHead) != 0))
                {
                    positionOfIG = unchecked(nextIG.igNum - 1);
                    break;
                }
                if (nextIG == this)
                {
                    return false;
                }

                nextIG = nextIG.igNext;
            }
        }
        else
        {
            positionOfIG = ig.igNum;
        }

        return positionOfThis < positionOfIG;
    }

    public bool IsAfter(insGroup ig)
    {
        return ig.IsBefore(this);
    }
}
