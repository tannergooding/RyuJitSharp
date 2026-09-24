// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public partial struct RegSet
{
    private readonly CodeGen _codeGen;
    private SpillDsc?[] _rsSpillDesc;
    private SpillDsc? _rsSpillFree;
    private bool _rsNeededSpillReg;

    public regMaskTP rsMaskResvd;

#if SWIFT_SUPPORT
    private regMaskTP _rsAllCalleeSavedMask;
    private regMaskTP _rsIntCalleeSavedMask;
#endif

#if TARGET_ARMARCH || TARGET_LOONGARCH64
    private regMaskTP _rsMaskCalleeSaved;
#endif

#if TARGET_ARM
    public regMaskTP rsMaskPreSpillRegArg;
    public regMaskTP rsMaskPreSpillAlign;
#endif

#if DEBUG
    private bool _rsModifiedRegsMaskInitialized;
#endif

    public RegSet(CodeGen codeGen)
    {
        this = default;
        _codeGen = codeGen;
        _rsSpillDesc = new SpillDsc?[(int)REG_COUNT];
        rsSpillInit();
        rsMaskResvd = RBM_NONE;

#if SWIFT_SUPPORT
#if TARGET_AMD64
        _rsIntCalleeSavedMask = new regMaskTP(SRBM_INT_CALLEE_SAVED);
        _rsAllCalleeSavedMask = new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED, SRBM_MSK_CALLEE_SAVED);
#else
        NYI("RegSet Swift callee-saved register masks outside AMD64");
        fatal(CORJIT_IMPLLIMITATION);
#endif
#endif

#if TARGET_ARMARCH || TARGET_LOONGARCH64
        _rsMaskCalleeSaved = RBM_NONE;
#endif

#if TARGET_ARM
        rsMaskPreSpillRegArg = RBM_NONE;
        rsMaskPreSpillAlign = RBM_NONE;
#endif

#if DEBUG
        _rsModifiedRegsMaskInitialized = false;
#endif
    }

    public readonly Compiler Compiler => _codeGen.Compiler;

    public readonly ref GCInfo GCInfo => ref _codeGen.GCInfo;

    private void rsSpillInit()
    {
        Array.Clear(_rsSpillDesc);
        _rsNeededSpillReg = false;
        _rsSpillFree = null;
    }

    public void tmpInit()
    {
        tmpCount = 0;
        tmpSize = -1;
#if DEBUG
        tmpGetCount = 0;
#endif
        tmpFree = default;
        tmpUsed = default;
    }

    private int tmpCount;
    private int tmpSize;

#if DEBUG
    /// <summary>Temps which haven't been released yet</summary>
    /// <remarks>Used by RegSet::rsSpillChk()</remarks>
    private int tmpGetCount;
#endif

    private InlineArrayRegSetTempSlotCount<TempDsc?> tmpFree;
    private InlineArrayRegSetTempSlotCount<TempDsc?> tmpUsed;

    public readonly bool HasComputedTmpSize => tmpSize != -1;

    public readonly int tmpTotalSize
    {
        get
        {
            assert(Debugger.IsAttached || HasComputedTmpSize);
            return tmpSize;
        }
    }

    private static int tmpSlot(var_types type)
    {
        int slot;

        switch (type)
        {
#if FEATURE_SIMD && TARGET_ARM64
            // Special slots are allocated for TYP_SIMD and TYP_MASK, because they
            // have unknown size and therefore can't share slots with other types.
            case TYP_SIMD:
            {
                slot = TEMP_SLOT_COUNT - 1;
                break;
            }

            case TYP_MASK:
            {
                slot = TEMP_SLOT_COUNT - 2;
                break;
            }
#endif
            default:
            {
                assert(!varTypeHasUnknownSize(type));
                var size = type.Size;

                noway_assert(size >= sizeof(int));
                noway_assert(size <= TEMP_MAX_SIZE);
                assert((size % sizeof(int)) == 0);

                slot = (size / sizeof(int)) - 1;
                break;
            }
        }

        assert((slot >= 0) && (slot < TEMP_SLOT_COUNT));
        return slot;
    }

    public void tmpBeginPreAllocateTemps()
    {
        tmpSize = 0;
    }

    /// <summary>Given a temp number, find the corresponding temp.</summary>
    /// <param name="tnum"></param>
    /// <param name="usageType"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>When looking for temps on the "free" list, this can only be used after code generation. (This is simply because we have an assert to that effect in tmpListBeg(); we could relax that, or hoist the assert to the appropriate callers.)</para>
    ///   <para>When looking for temps on the "used" list, this can be used any time.</para>
    /// </remarks>
    public readonly TempDsc? tmpFindNum(int tnum, TEMP_USAGE_TYPE usageType = TEMP_USAGE_FREE)
    {
        // temp numbers are negative
        assert(tnum < 0);

        for (var tmpDsc = tmpListBeg(usageType); tmpDsc is not null; tmpDsc = tmpListNxt(tmpDsc, usageType))
        {
            if (tmpDsc.tdTempNum == tnum)
            {
                return tmpDsc;
            }
        }
        return null;
    }

#if DEBUG
    public readonly bool tmpGetAllFree()
    {
        // The 'tmpGetCount' should equal the number of things in the 'tmpUsed' lists. This is a convenient place to assert that.
        var usedCount = 0;

        for (var tempDsc = tmpListBeg(TEMP_USAGE_USED); tempDsc is not null; tempDsc = tmpListNxt(tempDsc, TEMP_USAGE_USED))
        {
            usedCount++;
        }
        assert(usedCount == tmpGetCount);

        if (tmpGetCount != 0)
        {
            return false;
        }

        ReadOnlySpan<TempDsc?> tmpList = tmpUsed;

        for (var i = 0; i < tmpList.Length; i++)
        {
            if (tmpList[i] is not null)
            {
                return false;
            }
        }
        return true;
    }
#endif

    /// <summary>Given a temp number, get the corresponding temp.</summary>
    /// <param name="tnum"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>This looks for temps in the free list and the used list, meaning it can only be used after code generation.</para>
    ///   <para>It will assert that the temp is found. This should be called for a temp that is known to exist.</para>
    /// </remarks>
    public readonly TempDsc tmpGetNum(int tnum)
    {
        var tmpDsc = tmpFindNum(tnum, TEMP_USAGE_FREE)
                  ?? tmpFindNum(tnum, TEMP_USAGE_USED);

        assert(tmpDsc is not null);
        return tmpDsc;
    }

    public readonly TempDsc? tmpListBeg(TEMP_USAGE_TYPE usageType = TEMP_USAGE_FREE)
    {
        // Return the first temp in the slot for the smallest size
        var tmpLists = (usageType is TEMP_USAGE_FREE) ? (ReadOnlySpan<TempDsc?>)(tmpFree) : tmpUsed;

        for (var i = 0; i < tmpLists.Length - 1; i++)
        {
            var slot = tmpLists[i];

            if (slot is not null)
            {
                return slot;
            }
        }
        return tmpLists[^1];
    }

    public readonly TempDsc? tmpListNxt(TempDsc curTemp, TEMP_USAGE_TYPE usageType = TEMP_USAGE_FREE)
    {
        assert(curTemp is not null);
        var temp = curTemp.tdNext;

        if (temp is null)
        {
            // If there are no more temps in the list, check if there are more
            // slots (for bigger sized temps) to walk. This is only possible if
            // the temps have a known size.

            var tmpLists = (usageType is TEMP_USAGE_FREE) ? (ReadOnlySpan<TempDsc?>)(tmpFree) : tmpUsed;

            var slot = tmpSlot(curTemp.tdTempType) + 1;

            while ((slot < tmpLists.Length) && (temp is null))
            {
                temp = tmpLists[slot];
                slot++;
            }
            assert((temp is not null) || (slot is TEMP_SLOT_COUNT));
        }
        return temp;
    }
}
