// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Target
{
    public static target_ssize_t SignedSizeMaxValue => Configuration.TARGET_64BIT ? long.MaxValue : int.MaxValue;

    public static target_ssize_t CastToSignedSize(long value)
        => Configuration.TARGET_64BIT ? value : unchecked((int)value);

    // Preserve the build's overflow-checking policy as well as the target width.
    public static target_ssize_t AddSignedSize(target_ssize_t left, target_ssize_t right)
        => Configuration.TARGET_64BIT ? left + right : unchecked((int)left) + unchecked((int)right);

    public static target_ssize_t SubtractSignedSize(target_ssize_t left, target_ssize_t right)
        => Configuration.TARGET_64BIT ? left - right : unchecked((int)left) - unchecked((int)right);

    public static bool TryAddSignedSize(target_ssize_t left, target_ssize_t right, out target_ssize_t result)
    {
        if (Configuration.TARGET_64BIT)
        {
            return CheckedOps.TryAdd(left, right, out result);
        }

        var success = CheckedOps.TryAdd(unchecked((int)left), unchecked((int)right), out var result32);
        result = result32;

        return success;
    }
}
