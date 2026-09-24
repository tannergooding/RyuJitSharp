// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
    public static var_types tmpNormalizeType(var_types type)
    {
        type = type.ActualType;

#if FEATURE_SIMD
        if (type is TYP_SIMD12)
        {
            type = TYP_SIMD16;
        }
#endif

        return type;
    }

    public void tmpPreAllocateTemps(var_types type, uint count)
    {
        assert(type == tmpNormalizeType(type));
        noway_assert(HasComputedTmpSize);

        var size = type.Size;
        var slot = tmpSlot(type);

        for (uint i = 0; i < count; i++)
        {
            if (tmpCount == int.MaxValue)
            {
                IMPL_LIMITATION("too many spill temps");
            }

            tmpCount++;

            if (size != SIZE_UNKNOWN)
            {
                tmpSize = unchecked(tmpSize + size);
            }

#if TARGET_ARM
            if (type is TYP_DOUBLE)
            {
                tmpSize = unchecked(tmpSize + TARGET_POINTER_SIZE);
            }
#endif

            var temp = new TempDsc(-tmpCount, size, type) {
                tdNext = tmpFree[slot],
            };

#if DEBUG
            if (Compiler.verbose)
            {
                jitprintf($"pre-allocated temp #{-temp.tdTempNum}, slot {slot}, size = {temp.tdTempSize}\n");
            }
#endif

            tmpFree[slot] = temp;
        }
    }

    public TempDsc tmpGetTemp(var_types type)
    {
        type = tmpNormalizeType(type);
        var slot = tmpSlot(type);

        TempDsc? previous = null;
        var temp = tmpFree[slot];

        while (temp is not null)
        {
            if (temp.tdTempType == type)
            {
                if (previous is null)
                {
                    tmpFree[slot] = temp.tdNext;
                }
                else
                {
                    previous.tdNext = temp.tdNext;
                }
                break;
            }

            previous = temp;
            temp = temp.tdNext;
        }

        noway_assert(temp is not null);

#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf($"reused temp #{-temp.tdTempNum}, slot {slot}, size = {temp.tdTempSize}\n");
        }
        tmpGetCount++;
#endif

        temp.tdNext = tmpUsed[slot];
        tmpUsed[slot] = temp;

        return temp;
    }

    public void tmpRlsTemp(TempDsc temp)
    {
        assert(temp is not null);
        var slot = tmpSlot(temp.tdTempType);

#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf($"release temp #{-temp.tdTempNum}, slot {slot}, size = {temp.tdTempSize}\n");
        }
        assert(tmpGetCount != 0);
        tmpGetCount--;
#endif

        TempDsc? previous = null;
        var current = tmpUsed[slot];

        while (current is not null)
        {
            if (ReferenceEquals(current, temp))
            {
                if (previous is null)
                {
                    tmpUsed[slot] = current.tdNext;
                }
                else
                {
                    previous.tdNext = current.tdNext;
                }
                break;
            }

            previous = current;
            current = current.tdNext;
        }

        assert(current is not null);

        temp.tdNext = tmpFree[slot];
        tmpFree[slot] = temp;
    }

    public readonly bool tmpIsUnknownSizeTemp(int tnum)
    {
        return varTypeHasUnknownSize(tmpGetNum(tnum).tdTempType);
    }
}
