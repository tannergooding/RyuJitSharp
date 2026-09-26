// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private const int StackAllocMaxSize = 0x2000;

    private unsafe bool CanAllocateLclVarOnStack(int lclNum, CORINFO_CLASS_HANDLE clsHnd,
        ObjectAllocationType allocType, nint length, ref int blockSize, out string reason, bool preliminaryCheck = false)
    {
        assert(preliminaryCheck || _analysisDone);

        var compiler = CompilerInstance;
        var enableBoxedValueClasses = true;
        var enableRefClasses = true;
        var enableArrays = true;
        reason = "[ok]";

#if DEBUG
        enableBoxedValueClasses = JitConfig.JitObjectStackAllocationBoxedValueClass != 0;
        enableRefClasses = JitConfig.JitObjectStackAllocationRefClass != 0;
        enableArrays = JitConfig.JitObjectStackAllocationArray != 0;
#endif
        uint classSize;

        if (allocType is ObjectAllocationType.OAT_NEWOBJ_HEAP)
        {
            reason = "[runtime disallows]";
            return false;
        }

        if (allocType is ObjectAllocationType.OAT_NEWARR)
        {
            if (!enableArrays)
            {
                reason = "[disabled by config]";
                return false;
            }

            if ((length < 0) || (length > CORINFO_Array_MaxLength))
            {
                reason = "[invalid array length]";
                return false;
            }

            if (ClassLayoutBuilder.IsArrayTooLarge(compiler, clsHnd, (int)length, StackAllocMaxSize))
            {
                reason = "[array is too large]";
                return false;
            }

            classSize = compiler.typGetArrayLayout(clsHnd, (int)length).Size;
        }
        else if (allocType is ObjectAllocationType.OAT_NEWOBJ)
        {
            if (compiler.info.compCompHnd->isValueClass(clsHnd))
            {
                if (!enableBoxedValueClasses)
                {
                    reason = "[disabled by config]";
                    return false;
                }

                classSize = unchecked((uint)compiler.info.compCompHnd->getClassSize(clsHnd));
            }
            else
            {
                if (!enableRefClasses)
                {
                    reason = "[disabled by config]";
                    return false;
                }

                assert(compiler.info.compCompHnd->canAllocateOnStack(clsHnd));
                classSize = unchecked((uint)compiler.info.compCompHnd->getHeapClassSize(clsHnd));
            }
        }
        else
        {
            assert(false, "Unexpected allocation type");
            return false;
        }

        if (classSize > StackAllocMaxSize)
        {
            reason = "[too large]";
            return false;
        }

        if (preliminaryCheck)
        {
            return true;
        }

        if (CanLclVarEscape(lclNum))
        {
            reason = "[escapes]";
            return false;
        }

        if (!IsLclVarUsed(lclNum))
        {
            reason = "[unused]";
            return false;
        }

        blockSize = checked((int)classSize);
        return true;
    }
}
