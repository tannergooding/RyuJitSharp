// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using static RyuJitSharp.CorJitResult;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool compUsesUnknownSizeFrame;

    public UnknownSizeFrame unkSizeFrame;

    // This frame is the first allocation in the alloca area, addressed downward from x19.
    // Masks occupy VL/8 each and precede vectors, with their block padded to VL.
    // Placing masks first adds ceil(nMask / 8), rather than 8 * nVector, to vector
    // addressing indices and reduces pressure on the immediate encoding range.
    public struct UnknownSizeFrame
    {
        // Architectural SVE bounds from the pinned targetarm64.h, independent of the build target.
        private const uint MinSveRegisterSizeBytes = 16;
        private const uint MaxSveRegisterSizeBytes = 256;

        public uint nVector;
        public uint nMask;

        public bool isFinalized;

        public readonly uint MaskBlockSizeInVectors()
        {
            assert(roundUp(0u, 8u) == 0);
            return roundUp(nMask, 8u) / 8;
        }

        public readonly uint VectorBlockSize() => nVector;

        public readonly uint FrameSizeInVectors() => unchecked(MaskBlockSizeInVectors() + VectorBlockSize());

        public uint AllocMask()
        {
            assert(!isFinalized);
            var index = nMask;
            nMask = unchecked(nMask + 1);
            return index;
        }

        public uint AllocVector()
        {
            assert(!isFinalized);
            var index = nVector;
            nVector = unchecked(nVector + 1);
            return index;
        }

        public readonly int GetOffset(uint index, bool isMask = false)
        {
            assert(isFinalized);
            uint offset;
            if (isMask)
            {
                assert(index < nMask);
                offset = index;
            }
            else
            {
                assert(index < nVector);
                offset = unchecked(MaskBlockSizeInVectors() + index);
            }

            assert(offset != uint.MaxValue);
            // Indices start one slot below the base; mask offsets use VL/8, vector offsets VL.
            return unchecked(-(int)(offset + 1));
        }

        public readonly int GetAddressingOffset(in LclVarDsc local)
        {
            return GetOffset(unchecked((uint)local.UnknownSizeFrameIndex), local.Type is TYP_MASK);
        }

        public readonly int GetAddressingOffset(TempDsc temp)
        {
            assert(temp.tdTempOffs >= 0);
            assert(varTypeHasUnknownSize(temp.tdTempType));
            return GetOffset(unchecked((uint)temp.tdTempOffs), temp.tdTempType is TYP_MASK);
        }

        // JIT mode knows the runtime VL, so scalable frame offsets can be expressed in bytes.
        public readonly int GetExactOffset(in LclVarDsc local, uint vectorLength)
        {
            assert(BitOperations.IsPow2(vectorLength) && (vectorLength >= MinSveRegisterSizeBytes)
                && (vectorLength <= MaxSveRegisterSizeBytes));
            var scale = local.Type is TYP_MASK ? vectorLength / 8 : vectorLength;
            return unchecked(GetAddressingOffset(in local) * unchecked((int)scale));
        }

        [SuppressMessage("Style", "IDE0251:Make member readonly",
            Justification = "Finalization mutates diagnostic state only in Debug builds.")]
        public void FinalizeLayout()
        {
            if (Configuration.DEBUG)
            {
                isFinalized = true;
            }
        }
    }

    public void lvaInitUnknownSizeFrame()
    {
        if (!Configuration.FEATURE_SIMD || !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_IMPLLIMITATION, "Unknown-size frame initialization requires SIMD-enabled ARM64.");
            return;
        }

        compUsesUnknownSizeFrame = false;
        if (Configuration.DEBUG)
        {
            unkSizeFrame.isFinalized = false;
        }
        unkSizeFrame.nMask = 0;
        unkSizeFrame.nVector = 0;
    }

    public void lvaAllocUnknownSizeLocal(int varNum)
    {
        if (!Configuration.FEATURE_SIMD || !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_IMPLLIMITATION, "Unknown-size local allocation requires SIMD-enabled ARM64.");
            return;
        }

        ref var local = ref lvaGetDesc(varNum);
        assert(varTypeHasUnknownSize(local.Type));
        local.UnknownSizeFrameIndex = local.Type switch {
            TYP_SIMD_ARM64 => unchecked((int)unkSizeFrame.AllocVector()),
            TYP_MASK => unchecked((int)unkSizeFrame.AllocMask()),
            _ => throw new UnreachableException(),
        };

        compUsesUnknownSizeFrame = true;
        // The scalable area precedes ordinary localloc space and needs its frame-pointer policy.
        compLocallocUsed = true;
    }

    public void lvaAllocateUnknownSizeTemp(TempDsc temp)
    {
        if (!Configuration.FEATURE_SIMD || !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_IMPLLIMITATION, "Unknown-size temporary allocation requires SIMD-enabled ARM64.");
            return;
        }

        assert(varTypeHasUnknownSize(temp.tdTempType));
        var offset = temp.tdTempType switch {
            TYP_SIMD_ARM64 => unchecked((int)unkSizeFrame.AllocVector()),
            TYP_MASK => unchecked((int)unkSizeFrame.AllocMask()),
            _ => throw new UnreachableException(),
        };
        assert(offset >= 0);
        temp.tdTempOffs = offset;
    }
}
