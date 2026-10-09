// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool compUsesUnknownSizeFrame;

#if FEATURE_SIMD && TARGET_ARM64
    public UnknownSizeFrame unkSizeFrame;

    // This frame is the first allocation in the alloca area, addressed downward from x19.
    // Masks occupy VL/8 each and precede vectors, with their block padded to VL.
    // Placing masks first adds ceil(nMask / 8), rather than 8 * nVector, to vector
    // addressing indices and reduces pressure on the immediate encoding range.
    public struct UnknownSizeFrame
    {
        public uint nVector;
        public uint nMask;

#if DEBUG
        public bool isFinalized;
#endif

        public readonly uint MaskBlockSizeInVectors()
        {
            assert(roundUp(0u, 8u) == 0);
            return roundUp(nMask, 8u) / 8;
        }

        public readonly uint VectorBlockSize() => nVector;

        public readonly uint FrameSizeInVectors() => unchecked(MaskBlockSizeInVectors() + VectorBlockSize());

        public uint AllocMask()
        {
#if DEBUG
            assert(!isFinalized);
#endif
            var index = nMask;
            nMask = unchecked(nMask + 1);
            return index;
        }

        public uint AllocVector()
        {
#if DEBUG
            assert(!isFinalized);
#endif
            var index = nVector;
            nVector = unchecked(nVector + 1);
            return index;
        }

        public readonly int GetOffset(uint index, bool isMask = false)
        {
#if DEBUG
            assert(isFinalized);
#endif
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
            assert(BitOperations.IsPow2(vectorLength) && (vectorLength >= (uint)MIN_SVE_REGSIZE_BYTES)
                && (vectorLength <= (uint)MAX_SVE_REGSIZE_BYTES));
            var scale = local.Type is TYP_MASK ? vectorLength / 8 : vectorLength;
            return unchecked(GetAddressingOffset(in local) * unchecked((int)scale));
        }

        [SuppressMessage("Style", "IDE0251:Make member readonly",
            Justification = "Finalization mutates native debug-only state.")]
        public void FinalizeLayout()
        {
#if DEBUG
            isFinalized = true;
#endif
        }
    }

    public void lvaInitUnknownSizeFrame()
    {
        compUsesUnknownSizeFrame = false;
#if DEBUG
        unkSizeFrame.isFinalized = false;
#endif
        unkSizeFrame.nMask = 0;
        unkSizeFrame.nVector = 0;
    }

    public void lvaAllocUnknownSizeLocal(int varNum)
    {
        ref var local = ref lvaGetDesc(varNum);
        assert(varTypeHasUnknownSize(local.Type));
        local.UnknownSizeFrameIndex = local.Type switch
        {
            TYP_SIMD => unchecked((int)unkSizeFrame.AllocVector()),
            TYP_MASK => unchecked((int)unkSizeFrame.AllocMask()),
            _ => throw new UnreachableException(),
        };

        compUsesUnknownSizeFrame = true;
        // The scalable area precedes ordinary localloc space and needs its frame-pointer policy.
        compLocallocUsed = true;
    }

    public void lvaAllocateUnknownSizeTemp(TempDsc temp)
    {
        assert(varTypeHasUnknownSize(temp.tdTempType));
        var offset = temp.tdTempType switch
        {
            TYP_SIMD => unchecked((int)unkSizeFrame.AllocVector()),
            TYP_MASK => unchecked((int)unkSizeFrame.AllocMask()),
            _ => throw new UnreachableException(),
        };
        assert(offset >= 0);
        temp.tdTempOffs = offset;
    }
#endif
}
