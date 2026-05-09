// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>simple stack model for the inlinee's evaluation stack.</summary>
internal partial struct FgStack
{
    private FgSlot slot0;
    private FgSlot slot1;
    private int depth;

    public FgStack()
    {
        slot0 = SLOT_INVALID;
        slot1 = SLOT_INVALID;
    }

    public readonly bool IsStackAtLeastOneDeep => depth >= 1;

    public readonly bool IsStackOneDeep => depth == 1;

    public readonly bool IsStackTwoDeep => depth == 2;

    public readonly FgSlot Slot0 => (depth >= 1) ? slot0 : SLOT_UNKNOWN;

    public readonly FgSlot Slot1 => (depth >= 2) ? slot1 : SLOT_UNKNOWN;

    public static bool IsArgument(FgSlot value) => value >= SLOT_ARGUMENT;

    public static bool IsArrayLen(FgSlot value) => value == SLOT_ARRAYLEN;

    public static bool IsConstant(FgSlot value) => value is SLOT_CONSTANT;

    public static bool IsConstantOrConstArg(FgSlot value, InlineInfo? info) => IsConstant(value) || IsConstArgument(value, info);

    public static bool IsConstArgument(FgSlot value, InlineInfo? info)
    {
        if ((info is null) || !IsArgument(value))
        {
            return false;
        }

        var argNum = value - SLOT_ARGUMENT;

        if (argNum < info.argCnt)
        {
            return info.inlArgInfo[argNum].argIsInvariant;
        }
        return false;
    }

    public static bool IsExactArgument(FgSlot value, InlineInfo? info)
    {
        if ((info is null) || !IsArgument(value))
        {
            return false;
        }

        var argNum = value - SLOT_ARGUMENT;

        if (argNum < info.argCnt)
        {
            return info.inlArgInfo[argNum].argIsExact;
        }
        return false;
    }

    public static int SlotTypeToArgNum(FgSlot value)
    {
        assert(IsArgument(value));
        return value - SLOT_ARGUMENT;
    }

    public void Clear()
    {
        depth = 0;
    }

    public void PushArgument(int arg) => Push(SLOT_ARGUMENT + arg);

    public void PushArrayLen() => Push(SLOT_ARRAYLEN);

    public void PushConstant() => Push(SLOT_CONSTANT);

    public void PushUnknown() => Push(SLOT_UNKNOWN);

    public readonly FgSlot Top(int n = 0)
    {
        var result = SLOT_UNKNOWN;

        if (n == 0)
        {
            if (depth >= 1)
            {
                result = slot0;
            }
        }
        else if (n == 1)
        {
            if (depth >= 2)
            {
                result = slot1;
            }
        }
        else
        {
            unreached();
        }
        return result;
    }

    public void Push(FgSlot slot)
    {
        assert(depth <= 2);

        slot1 = slot0;
        slot0 = slot;

        if (depth < 2)
        {
            depth++;
        }
    }
}
